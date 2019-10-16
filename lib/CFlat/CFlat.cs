public sealed class CFlat
{
	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	internal readonly ByteCodeChunk chunk = new ByteCodeChunk();
	internal string source;
	internal Buffer<CompileError> registerErrors = new Buffer<CompileError>();

	public Buffer<CompileError> CompileSource(string source, Mode mode)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.Compile(chunk, mode, source);
		if (errors.count == 0)
			virtualMachine.Load(chunk);
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source, Mode mode)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.CompileExpression(chunk, mode, source);
		if (errors.count == 0)
			virtualMachine.Load(chunk);
		return errors;
	}

	public Option<RuntimeError> GetError()
	{
		return virtualMachine.error;
	}
}