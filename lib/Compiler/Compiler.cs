public enum Mode
{
	Release,
	Debug
}

internal sealed class Compiler
{
	public Mode mode;
	public readonly Parser parser;
	public Buffer<CompileError> errors = new Buffer<CompileError>();
	public int currentSourceIndex;

	public bool isInPanicMode;
	public ByteCodeChunk chunk;

	public Buffer<LocalVariable> localVariables = new Buffer<LocalVariable>(256);
	public int scopeDepth;

	public Buffer<ValueType> functionReturnTypeStack = new Buffer<ValueType>(4);

	public Buffer<LoopBreak> loopBreaks = new Buffer<LoopBreak>(4);
	public Buffer<Slice> loopNesting = new Buffer<Slice>(4);

	public Compiler()
	{
		void AddTokenizerError(Slice slice, string message, object[] args)
		{
			AddHardError(slice, message, args);
		}

		var tokenizer = new Tokenizer(TokenScanners.scanners);
		parser = new Parser(tokenizer, AddTokenizerError);
		Reset(null, Mode.Release, null, 0);
	}

	public void Reset(ByteCodeChunk chunk, Mode mode, string source, int sourceIndex)
	{
		this.mode = mode;
		parser.tokenizer.Reset(source);
		parser.Reset();

		errors.count = 0;
		currentSourceIndex = sourceIndex;

		isInPanicMode = false;
		this.chunk = chunk;
		localVariables.count = 0;
		scopeDepth = 0;
	}

	public Compiler AddSoftError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
			errors.PushBack(new CompileError(currentSourceIndex, slice, string.Format(format, args)));
		return this;
	}

	public Compiler AddHardError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
		{
			isInPanicMode = true;
			if (args == null || args.Length == 0)
				errors.PushBack(new CompileError(currentSourceIndex, slice, format));
			else
				errors.PushBack(new CompileError(currentSourceIndex, slice, string.Format(format, args)));
		}
		return this;
	}
}
