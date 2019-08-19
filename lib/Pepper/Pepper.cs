public sealed class Pepper
{
	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	public readonly ByteCodeChunk byteCode = new ByteCodeChunk();
	internal string source;
	internal Buffer<RuntimeError> errors = new Buffer<RuntimeError>();

	public Buffer<CompileError> CompileSource(string source)
	{
		this.source = source;
		return compiler.Compile(source, byteCode);
	}

	public Buffer<CompileError> CompileExpression(string source)
	{
		this.source = source;
		return compiler.CompileExpression(source, byteCode);
	}

	public Buffer<RuntimeError> RunLastFunction()
	{
		if (byteCode.functions.count == 0)
		{
			errors.PushBack(new RuntimeError(
				0,
				new Slice(),
				"No function defined"
			));
		}

		if (errors.count > 0)
			return errors;

		var error = virtualMachine.RunFunction(byteCode, byteCode.functions.count - 1);
		if (error.isSome)
			errors.PushBack(error.value);

		return errors;
	}

	public RuntimeContext GetContext()
	{
		return new RuntimeContext(virtualMachine);
	}
}