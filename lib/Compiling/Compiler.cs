using System.Collections.Generic;

public readonly struct CompileError
{
	public readonly Slice slice;
	public readonly string message;

	public CompileError(Slice slice, string message)
	{
		this.slice = slice;
		this.message = message;
	}
}

public sealed class Compiler
{
	public readonly struct LocalVariable
	{
		public readonly Slice slice;
		public readonly int depth;
		public readonly ValueType type;
		public readonly bool isMutable;
		public readonly bool isUsed;

		public LocalVariable(Slice slice, int depth, ValueType type, bool isMutable, bool isUsed)
		{
			this.slice = slice;
			this.depth = depth;
			this.type = type;
			this.isMutable = isMutable;
			this.isUsed = isUsed;
		}
	}

	private readonly struct Function
	{
		public readonly int index;
		public readonly Buffer<ValueType> argTypes;
		public readonly ValueType returnType;
		public readonly int instructionIndex;

		public Function(int index, Buffer<ValueType> argTypes, ValueType returnType, int instructionIndex)
		{
			this.index = index;
			this.argTypes = argTypes;
			this.returnType = returnType;
			this.instructionIndex = instructionIndex;
		}
	}

	private readonly struct LoopBreak
	{
		public readonly int nesting;
		public readonly int jump;

		public LoopBreak(int nesting, int jump)
		{
			this.nesting = nesting;
			this.jump = jump;
		}
	}

	public readonly List<CompileError> errors = new List<CompileError>();
	public Token previousToken;
	private Token currentToken;

	internal ITokenizer tokenizer;
	private ParseRule[] parseRules;
	private ParseFunction onParseWithPrecedence;
	private bool panicMode;
	private ByteCodeChunk chunk;
	private Buffer<ValueType> typeStack = new Buffer<ValueType>(256);
	private Buffer<LocalVariable> localVariables = new Buffer<LocalVariable>(256);
	private Buffer<Function> functions = new Buffer<Function>(64);
	private int scopeDepth;
	private int loopNesting;
	private Buffer<LoopBreak> loopBreaks = new Buffer<LoopBreak>(16);

	public void Begin(ITokenizer tokenizer, ParseRule[] parseRules, ParseFunction onParseWithPrecedence)
	{
		errors.Clear();
		this.tokenizer = tokenizer;
		this.parseRules = parseRules;
		this.onParseWithPrecedence = onParseWithPrecedence;
		previousToken = new Token(Token.EndKind, new Slice());
		currentToken = new Token(Token.EndKind, new Slice());
		panicMode = false;
		chunk = new ByteCodeChunk();
		typeStack.count = 0;
		localVariables.count = 0;
		functions.count = 0;
		scopeDepth = 0;
		loopNesting = 0;
		loopBreaks.count = 0;
	}

	public Compiler AddSoftError(Slice slice, string format, params object[] args)
	{
		errors.Add(new CompileError(slice, string.Format(format, args)));
		return this;
	}

	public Compiler AddHardError(Slice slice, string format, params object[] args)
	{
		if (panicMode)
			return this;

		panicMode = true;
		errors.Add(new CompileError(slice, string.Format(format, args)));

		return this;
	}

	public ByteCodeChunk GetByteCodeChunk()
	{
		var c = chunk;
		chunk = null;
		return c;
	}

	public void BeginScope()
	{
		scopeDepth += 1;
	}

	public void EndScope(bool leftValueOnStack)
	{
		scopeDepth -= 1;

		var localCount = 0;
		while (
			localVariables.count > 0 &&
			localVariables.buffer[localVariables.count - 1].depth > scopeDepth
		)
		{
			var variable = localVariables.buffer[localVariables.count - 1];
			if (!variable.isUsed)
				AddSoftError(variable.slice, "Unused variable");

			localVariables.count -= 1;
			localCount += 1;
		}

		if (localCount > 0)
		{
			if (leftValueOnStack)
			{
				EmitInstruction(Instruction.CopyTo);
				EmitByte((byte)localCount);
				typeStack.buffer[typeStack.count - 1 - localCount] = typeStack.buffer[typeStack.count - 1];
			}

			EmitInstruction(Instruction.PopMultiple);
			EmitByte((byte)localCount);
			typeStack.count -= localCount;
		}
	}

	public void BeginLoop()
	{
		loopNesting += 1;
	}

	public void EndLoop()
	{
		loopNesting -= 1;

		for (var i = loopBreaks.count - 1; i >= 0; i--)
		{
			var loopBreak = loopBreaks.buffer[i];
			if (loopBreak.nesting == loopNesting)
			{
				PatchJump(loopBreak.jump);
				loopBreaks.SwapRemove(i);
			}
		}
	}

	public bool BreakLoop(int nesting, int jump)
	{
		if (loopNesting < nesting)
			return false;

		loopBreaks.PushBack(new LoopBreak(loopNesting - nesting, jump));
		return true;
	}

	public int DeclareLocalVariable(Slice slice, bool mutable)
	{
		if (localVariables.count >= localVariables.buffer.Length)
		{
			AddSoftError(previousToken.slice, "Too many local variables in function");
			return -1;
		}

		var type = ValueType.Nil;
		if (typeStack.count > 0)
			type = typeStack.buffer[typeStack.count - 1];

		localVariables.PushBack(new LocalVariable(slice, scopeDepth, type, mutable, false));
		return localVariables.count - 1;
	}

	public int ResolveToLocalVariableIndex()
	{
		var source = tokenizer.Source;

		for (var i = localVariables.count - 1; i >= 0; i--)
		{
			var local = localVariables.buffer[i];
			if (CompilerHelper.AreEqual(source, previousToken.slice, local.slice))
				return i;
		}

		return -1;
	}

	public void UseVariable(int index)
	{
		var variable = localVariables.buffer[index];
		if (!variable.isUsed)
			localVariables.buffer[index] = new LocalVariable(
				variable.slice,
				variable.depth,
				variable.type,
				variable.isMutable,
				true
			);
	}

	public LocalVariable GetLocalVariable(int index)
	{
		return localVariables.buffer[index];
	}

	public int GetTokenPrecedence(int tokenKind)
	{
		return parseRules[tokenKind].precedence;
	}

	public void ParseWithPrecedence(int precedence)
	{
		Next();
		if (previousToken.kind == Token.EndKind)
			return;

		var prefixRule = parseRules[previousToken.kind].prefixRule;
		if (prefixRule == null)
		{
			AddHardError(previousToken.slice, "Expected expression");
			return;
		}
		prefixRule(this, precedence);

		while (
			currentToken.kind != Token.EndKind &&
			precedence <= parseRules[currentToken.kind].precedence
		)
		{
			Next();
			var infixRule = parseRules[previousToken.kind].infixRule;
			infixRule(this, precedence);
		}

		onParseWithPrecedence(this, precedence);
	}

	public void Next()
	{
		previousToken = currentToken;

		while (true)
		{
			currentToken = tokenizer.Next();
			if (currentToken.kind != Token.ErrorKind)
				break;

			AddHardError(currentToken.slice, "Invalid char");
		}
	}

	public bool Check(int tokenKind)
	{
		return currentToken.kind == tokenKind;
	}

	public bool Match(int tokenKind)
	{
		if (currentToken.kind != tokenKind)
			return false;

		Next();
		return true;
	}

	public void Consume(int tokenKind, string errorMessage)
	{
		if (currentToken.kind == tokenKind)
			Next();
		else
			AddHardError(currentToken.slice, errorMessage);
	}

	public void PushType(ValueType type)
	{
		typeStack.PushBack(type);
	}

	public ValueType PopType()
	{
		return typeStack.PopLast();
	}

	public Compiler EmitByte(byte value)
	{
		chunk.WriteByte(value, previousToken.slice);
		return this;
	}

	public Compiler EmitInstruction(Instruction instruction)
	{
		EmitByte((byte)instruction);
		return this;
	}

	public Compiler EmitLoadLiteral(ValueData value, ValueType type)
	{
		var index = chunk.AddValueLiteral(value, type);
		EmitInstruction(Instruction.LoadLiteral);
		EmitByte((byte)index);

		return this;
	}

	public Compiler EmitLoadStringLiteral(string value)
	{
		var index = chunk.AddStringLiteral(value);
		EmitInstruction(Instruction.LoadLiteral);
		EmitByte((byte)index);

		return this;
	}

	public Compiler RemoveLastEmittedByte()
	{
		chunk.bytes.count -= 1;
		return this;
	}

	public int BeginEmitBackwardJump()
	{
		return chunk.bytes.count;
	}

	public void EndEmitBackwardJump(Instruction instruction, int jumpIndex)
	{
		EmitInstruction(instruction);

		var offset = chunk.bytes.count - jumpIndex + 2;
		if (offset > ushort.MaxValue)
		{
			AddSoftError(previousToken.slice, "Too much code to jump over");
			return;
		}

		BytesHelper.ShortToBytes((ushort)offset, out var b0, out var b1);
		EmitByte(b0);
		EmitByte(b1);
	}

	public int BeginEmitForwardJump(Instruction instruction)
	{
		EmitInstruction(instruction);
		EmitByte(0);
		EmitByte(0);

		return chunk.bytes.count - 2;
	}

	public void EndEmitForwardJump(int jumpIndex)
	{
		if (!PatchJump(jumpIndex))
			AddSoftError(previousToken.slice, "Too much code to jump over");
	}

	private bool PatchJump(int jumpIndex)
	{
		var offset = chunk.bytes.count - jumpIndex - 2;
		if (offset > ushort.MaxValue)
			return false;

		BytesHelper.ShortToBytes(
			(ushort)offset,
			out chunk.bytes.buffer[jumpIndex],
			out chunk.bytes.buffer[jumpIndex + 1]
		);
		return true;
	}
}