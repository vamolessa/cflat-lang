public sealed class CFlat
{
	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	internal readonly Linking linking = new Linking();
	internal string source;
	internal Buffer<CompileError> registerErrors = new Buffer<CompileError>();

	public Buffer<CompileError> CompileSource(string source, Mode mode)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.Compile(linking, mode, source);
		if (errors.count == 0)
			virtualMachine.Load(linking);
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source, Mode mode)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.CompileExpression( linking, mode, source);
		if (errors.count == 0)
			virtualMachine.Load(linking);
		return errors;
	}

	public Option<RuntimeError> GetError()
	{
		return virtualMachine.error;
	}
}