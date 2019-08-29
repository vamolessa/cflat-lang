public sealed class Pepper
{
	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	internal readonly ByteCodeChunk byteCode = new ByteCodeChunk();
	internal string source;
	internal Buffer<CompileError> registerErrors = new Buffer<CompileError>();

	public Buffer<CompileError> CompileSource(string source, Mode mode)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.Compile(source, byteCode, mode);
		if (errors.count == 0)
			virtualMachine.Load(byteCode);
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source, Mode mode)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.CompileExpression(source, byteCode, mode);
		if (errors.count == 0)
			virtualMachine.Load(byteCode);
		return errors;
	}

	public Option<RuntimeError> GetError()
	{
		return virtualMachine.error;
	}
}