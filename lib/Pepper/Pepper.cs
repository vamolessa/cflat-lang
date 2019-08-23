public sealed class Pepper
{
	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	internal readonly ByteCodeChunk byteCode = new ByteCodeChunk();
	internal string source;
	internal Buffer<CompileError> registerErrors = new Buffer<CompileError>();

	public Buffer<CompileError> CompileSource(string source)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.Compile(source, byteCode);
		if (errors.count == 0)
			virtualMachine.Load(byteCode);
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		var errors = compiler.CompileExpression(source, byteCode);
		if (errors.count == 0)
			virtualMachine.Load(byteCode);
		return errors;
	}

	public FunctionCall CallFunction(string functionName)
	{
		if (compiler.compiler.errors.count > 0 || registerErrors.count > 0)
		{
			virtualMachine.Error("Has compile errors");
			return new FunctionCall(virtualMachine, ushort.MaxValue);
		}

		return virtualMachine.CallFunction(functionName);
	}

	public Option<RuntimeError> GetError()
	{
		return virtualMachine.error;
	}
}