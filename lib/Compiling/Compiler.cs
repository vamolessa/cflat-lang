using System.Collections.Generic;

public readonly struct CompileError
{
	public readonly int sourceIndex;
	public readonly string message;

	public CompileError(int sourceIndex, string message)
	{
		this.sourceIndex = sourceIndex;
		this.message = message;
	}
}

public sealed class Compiler
{
	public readonly struct Convertible
	{
		public readonly string source;
		public readonly Token token;

		public Convertible(string source, Token token)
		{
			this.source = source;
			this.token = token;
		}
	}

	public readonly List<CompileError> errors = new List<CompileError>();
	public Token previousToken;
	private Token currentToken;

	internal ITokenizer tokenizer;
	private ParseRule[] parseRules;
	private bool panicMode;
	private ByteCodeChunk chunk;
	private Buffer<int> typeStack = new Buffer<int>(256);

	public void Begin(ITokenizer tokenizer, ParseRule[] parseRules)
	{
		errors.Clear();
		this.tokenizer = tokenizer;
		this.parseRules = parseRules;
		previousToken = new Token(Token.EndKind, 0, 0);
		currentToken = new Token(Token.EndKind, 0, 0);
		panicMode = false;
		chunk = new ByteCodeChunk();
		typeStack.count = 0;
	}

	public void AddSoftError(int sourceIndex, string message)
	{
		errors.Add(new CompileError(sourceIndex, message));
	}

	public void AddHardError(int sourceIndex, string message)
	{
		if (panicMode)
			return;

		panicMode = true;
		errors.Add(new CompileError(sourceIndex, message));
	}

	public ByteCodeChunk GetByteCodeChunk()
	{
		var c = chunk;
		chunk = null;
		return c;
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
			AddHardError(previousToken.index, "Expected expression");
			return;
		}
		prefixRule(this);

		while (
			currentToken.kind != Token.EndKind &&
			precedence <= parseRules[currentToken.kind].precedence
		)
		{
			Next();
			var infixRule = parseRules[previousToken.kind].infixRule;
			infixRule(this);
		}
	}

	public void Next()
	{
		previousToken = currentToken;

		while (true)
		{
			currentToken = tokenizer.Next();
			if (currentToken.kind != Token.ErrorKind)
				break;

			AddHardError(currentToken.index, "Invalid char");
		}
	}

	public void Consume(int tokenKind, string errorMessage)
	{
		if (currentToken.kind == tokenKind)
			Next();
		else
			AddHardError(currentToken.index, errorMessage);
	}

	public void PushType(int type)
	{
		typeStack.PushBack(type);
	}

	public int PopType()
	{
		return typeStack.PopLast();
	}

	public Convertible Convert()
	{
		return new Convertible(tokenizer.Source, previousToken);
	}

	public void EmitByte(byte value)
	{
		chunk.WriteByte(value, previousToken.index);
	}

	public void EmitInstruction(Instruction instruction)
	{
		EmitByte((byte)instruction);
	}

	public void EmitLoadConstant(Value value)
	{
		var index = System.Array.IndexOf(chunk.constants.buffer, value);
		if (index < 0)
			index = chunk.AddConstant(value);

		EmitInstruction(Instruction.LoadConstant);
		EmitByte((byte)index);
	}

	public void EmitLoadStringLiteral(string value)
	{
		var stringIndex = System.Array.IndexOf(chunk.stringLiterals.buffer, value);

		var constantIndex = stringIndex < 0 ?
			chunk.AddStringLiteral(value) :
			System.Array.IndexOf(
				chunk.constants.buffer,
				new Value(Value.Type.Object, new Value.Data(stringIndex))
			);

		EmitInstruction(Instruction.LoadConstant);
		EmitByte((byte)constantIndex);
	}
}