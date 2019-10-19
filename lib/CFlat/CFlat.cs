public sealed class CFlat
{
	internal readonly struct Source
	{
		public readonly string name;
		public readonly string content;

		public Source(string name, string content)
		{
			this.name = name;
			this.content = content;
		}
	}

	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	internal ByteCodeChunk chunk = new ByteCodeChunk();
	internal Buffer<Source> sources = new Buffer<Source>();
	internal Buffer<CompileError> compileErrors = new Buffer<CompileError>();

	public void Clear()
	{
		chunk = new ByteCodeChunk();
		sources.count = 0;
		compileErrors.count = 0;
	}

	public Buffer<CompileError> CompileSource(string sourceName, string source, Mode mode)
	{
		if (compileErrors.count > 0)
			return compileErrors;

		sources.PushBack(new Source(sourceName, source));
		chunk.sourceStartIndexes.PushBack(chunk.bytes.count);

		var errors = compiler.Compile(chunk, mode, source);
		if (errors.count > 0)
			compileErrors = errors;
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source, Mode mode)
	{
		if (compileErrors.count > 0)
			return compileErrors;

		sources.PushBack(new Source("expression", source));
		chunk.sourceStartIndexes.PushBack(chunk.bytes.count);

		var errors = compiler.CompileExpression(chunk, mode, source);
		if (errors.count > 0)
			compileErrors = errors;
		return errors;
	}

	public bool Load()
	{
		if (compileErrors.count > 0)
			return false;

		virtualMachine.Load(chunk);
		return true;
	}

	public Option<RuntimeError> GetError()
	{
		return virtualMachine.error;
	}
}