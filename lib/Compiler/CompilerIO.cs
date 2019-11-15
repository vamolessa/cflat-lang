internal sealed class CompilerIO
{
	private readonly struct StateFrame
	{
		public readonly int sourceIndex;
		public readonly bool isInPanicMode;
		public readonly ushort localVariablesIndex;
		public readonly int scopeDepth;
		public readonly ushort functionReturnTypeStackIndex;
		public readonly ushort loopBreaksIndex;
		public readonly ushort loopNestingIndex;
	}

	public Mode mode;
	public readonly Parser parser;
	public ByteCodeChunk chunk;
	public Buffer<CompileError> errors = new Buffer<CompileError>();

	public int sourceIndex;
	public bool isInPanicMode;

	public Buffer<LocalVariable> localVariables = new Buffer<LocalVariable>(256);
	public int scopeDepth;

	public Buffer<ValueType> functionReturnTypeStack = new Buffer<ValueType>(4);

	public Buffer<LoopBreak> loopBreaks = new Buffer<LoopBreak>(4);
	public Buffer<Slice> loopNesting = new Buffer<Slice>(4);

	private Buffer<StateFrame> stateFrameStack = new Buffer<StateFrame>();

	public CompilerIO()
	{
		void AddTokenizerError(Slice slice, string message, object[] args)
		{
			AddHardError(slice, message, args);
		}

		var tokenizer = new Tokenizer(TokenScanners.scanners);
		parser = new Parser(tokenizer, AddTokenizerError);
		Reset(null, Mode.Release);
	}

	public void Reset(ByteCodeChunk chunk, Mode mode)
	{
		this.mode = mode;
		this.sourceIndex = -1;

		errors.count = 0;

		isInPanicMode = false;
		this.chunk = chunk;
		localVariables.count = 0;
		scopeDepth = 0;

		stateFrameStack.count = 0;
	}
	
	public void PushSource(string source)
	{
		// SAVE STATE

		parser.tokenizer.Reset(source, 0);
		parser.Reset(new Token(TokenKind.End, new Slice()), new Token(TokenKind.End, new Slice()));
	}

	public void PopSource()
	{
		// RESTORE STATE
	}

	public CompilerIO AddSoftError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
			errors.PushBack(new CompileError(sourceIndex, slice, string.Format(format, args)));
		return this;
	}

	public CompilerIO AddHardError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
		{
			isInPanicMode = true;
			if (args == null || args.Length == 0)
				errors.PushBack(new CompileError(sourceIndex, slice, format));
			else
				errors.PushBack(new CompileError(sourceIndex, slice, string.Format(format, args)));
		}
		return this;
	}
}
