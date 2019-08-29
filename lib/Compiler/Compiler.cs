public sealed class Compiler
{
	public Mode mode;
	public readonly Parser parser;
	public Buffer<CompileError> errors = new Buffer<CompileError>();

	public bool isInPanicMode;
	public ByteCodeChunk chunk;
	public Buffer<ValueType> typeStack = new Buffer<ValueType>(256);

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
		this.parser = new Parser(tokenizer, AddTokenizerError);
		Reset(null, null, Mode.Release);
	}

	public void Reset(string source, ByteCodeChunk chunk, Mode mode)
	{
		parser.tokenizer.Reset(source);
		parser.Reset();

		errors.count = 0;
		this.mode = mode;

		isInPanicMode = false;
		this.chunk = chunk;
		typeStack.count = 0;
		localVariables.count = 0;
		scopeDepth = 0;
	}

	public Compiler AddSoftError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
			errors.PushBack(new CompileError(slice, string.Format(format, args)));
		return this;
	}

	public Compiler AddHardError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
		{
			isInPanicMode = true;
			if (args == null || args.Length == 0)
				errors.PushBack(new CompileError(slice, format));
			else
				errors.PushBack(new CompileError(slice, string.Format(format, args)));
		}
		return this;
	}
}