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

public struct LocalVariable
{
	public Slice slice;
	public int depth;
	public ValueType type;
	public bool isMutable;
	public bool isUsed;

	public LocalVariable(Slice slice, int depth, ValueType type, bool isMutable, bool isUsed)
	{
		this.slice = slice;
		this.depth = depth;
		this.type = type;
		this.isMutable = isMutable;
		this.isUsed = isUsed;
	}
}

public sealed class Compiler
{
	public readonly struct Scope
	{
		public readonly int localVarStartIndex;

		public Scope(int localVarStartIndex)
		{
			this.localVarStartIndex = localVarStartIndex;
		}
	}

	public readonly List<CompileError> errors = new List<CompileError>();
	public Token previousToken;
	public Token currentToken;

	public ITokenizer tokenizer;
	public ParseFunction onParseWithPrecedence;

	public bool panicMode;
	public ByteCodeChunk chunk;
	public Buffer<ValueType> typeStack = new Buffer<ValueType>(256);
	public Buffer<LocalVariable> localVariables = new Buffer<LocalVariable>(256);
	public int scopeDepth;

	public void Reset(ITokenizer tokenizer, ParseRule[] parseRules, ParseFunction onParseWithPrecedence)
	{
		errors.Clear();
		this.tokenizer = tokenizer;
		this.onParseWithPrecedence = onParseWithPrecedence;

		previousToken = new Token(Token.EndKind, new Slice());
		currentToken = new Token(Token.EndKind, new Slice());

		panicMode = false;
		chunk = new ByteCodeChunk();
		typeStack.count = 0;
		localVariables.count = 0;
		scopeDepth = 0;
	}

	public Compiler AddSoftError(Slice slice, string format, params object[] args)
	{
		if (!panicMode)
			errors.Add(new CompileError(slice, string.Format(format, args)));
		return this;
	}

	public Compiler AddHardError(Slice slice, string format, params object[] args)
	{
		if (!panicMode)
		{
			panicMode = true;
			if (args == null || args.Length == 0)
				errors.Add(new CompileError(slice, format));
			else
				errors.Add(new CompileError(slice, string.Format(format, args)));
		}
		return this;
	}

	public Scope BeginScope()
	{
		scopeDepth += 1;
		return new Scope(localVariables.count);
	}

	public void EndScope(Scope scope)
	{
		scopeDepth -= 1;

		for (var i = scope.localVarStartIndex; i < localVariables.count; i++)
		{
			var variable = localVariables.buffer[i];
			if (!variable.isUsed)
				AddSoftError(variable.slice, "Unused variable");
		}

		var localCount = localVariables.count - scope.localVarStartIndex;
		if (localCount > 0)
		{
			this.EmitInstruction(Instruction.PopMultiple);
			this.EmitByte((byte)localCount);

			localVariables.count -= localCount;
			typeStack.count -= localCount;
		}
	}

	public ByteCodeChunk.FunctionDefinitionBuilder BeginFunctionDeclaration()
	{
		return chunk.BeginAddFunction();
	}

	public void EndFunctionDeclaration(Slice slice, ByteCodeChunk.FunctionDefinitionBuilder builder)
	{
		if (chunk.functions.count >= ushort.MaxValue)
		{
			chunk.functionsParams.count -= builder.parameterCount;
			AddSoftError(slice, "Too many function declarations");
			return;
		}

		var name = tokenizer.Source.Substring(slice.index, slice.length);
		chunk.EndAddFunction(name, builder);
	}

	public int ResolveToFunctionIndex()
	{
		var source = tokenizer.Source;

		for (var i = 0; i < chunk.functions.count; i++)
		{
			var f = chunk.functions.buffer[i];
			if (CompilerHelper.AreEqual(source, previousToken.slice, f.name))
				return i;
		}

		return -1;
	}

	public ValueType GetFunctionParamType(in FunctionDefinition function, int paramIndex)
	{
		return chunk.functionsParams.buffer[function.parameters.index + paramIndex];
	}

	public int DeclareLocalVariable(Slice slice, bool mutable)
	{
		if (localVariables.count >= localVariables.buffer.Length)
		{
			AddSoftError(slice, "Too many local variables in function");
			return -1;
		}

		var type = ValueType.Unit;
		if (typeStack.count > 0)
			type = typeStack.buffer[typeStack.count - 1];

		localVariables.PushBack(new LocalVariable(slice, scopeDepth, type, mutable, false));
		return localVariables.count - 1;
	}

	public int ResolveToLocalVariableIndex()
	{
		var source = tokenizer.Source;

		for (var i = 0; i < localVariables.count; i++)
		{
			var local = localVariables.buffer[i];
			if (CompilerHelper.AreEqual(source, previousToken.slice, local.slice))
				return i;
		}

		return -1;
	}
}