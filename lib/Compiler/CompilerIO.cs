namespace cflat
{
	internal sealed class CompilerIO
	{
		private readonly struct StateFrame
		{
			public readonly string sourceContent;
			public readonly int sourceIndex;

			public readonly int tokenizerIndex;
			public readonly Token previousToken;
			public readonly Token currentToken;

			public StateFrame(string sourceContent, int sourceIndex, int tokenizerIndex, Token previousToken, Token currentToken)
			{
				this.sourceContent = sourceContent;
				this.sourceIndex = sourceIndex;

				this.tokenizerIndex = tokenizerIndex;
				this.previousToken = previousToken;
				this.currentToken = currentToken;
			}
		}

		public Mode mode;
		public readonly Parser parser;
		public ByteCodeChunk chunk;
		public Buffer<CompileError> errors = new Buffer<CompileError>();

		public int sourceIndex;
		public bool isInPanicMode;
		public int functionsStartIndex;
		public int structTypesStartIndex;

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
		}

		public void Reset(Mode mode, ByteCodeChunk chunk)
		{
			this.mode = mode;
			this.chunk = chunk;
			errors.count = 0;
			stateFrameStack.count = 0;

			functionsStartIndex = 0;
			structTypesStartIndex = 0;
		}

		private void RestoreState(StateFrame state)
		{
			parser.tokenizer.Reset(state.sourceContent, state.tokenizerIndex);
			parser.Reset(state.previousToken, state.currentToken);
			sourceIndex = state.sourceIndex;

			isInPanicMode = false;

			localVariables.count = 0;
			scopeDepth = 0;

			functionReturnTypeStack.count = 0;
			loopBreaks.count = 0;
			loopNesting.count = 0;
		}

		public void BeginSource(string source, int sourceIndex)
		{
			stateFrameStack.PushBack(new StateFrame(
				parser.tokenizer.source,
				this.sourceIndex,

				parser.tokenizer.nextIndex,
				parser.previousToken,
				parser.currentToken
			));

			RestoreState(new StateFrame(
				source,
				sourceIndex,
				0,
				new Token(TokenKind.End, new Slice()),
				new Token(TokenKind.End, new Slice())
			));
		}

		public void EndSource()
		{
			RestoreState(stateFrameStack.PopLast());
			functionsStartIndex = chunk.functions.count;
			structTypesStartIndex = chunk.structTypes.count;
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
}