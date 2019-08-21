public sealed class Pepper
{
	internal readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	public readonly ByteCodeChunk byteCode = new ByteCodeChunk();
	internal string source;
	internal Buffer<CompileError> registerErrors = new Buffer<CompileError>();

	public bool DebugMode
	{
		get { return virtualMachine.debugMode; }
		set { virtualMachine.debugMode = value; }
	}

	public Buffer<CompileError> CompileSource(string source)
	{
		if (registerErrors.count > 0)
			return registerErrors;

		this.source = source;
		return compiler.Compile(source, byteCode);
	}

	public Buffer<CompileError> CompileExpression(string source)
	{
		this.source = source;
		return compiler.CompileExpression(source, byteCode);
	}

	public Option<RuntimeError> RunLastFunction()
	{
		if (byteCode.functions.count == 0)
		{
			return Option.Some(new RuntimeError(
				0,
				new Slice(),
				"No function defined"
			));
		}

		virtualMachine.Load(byteCode);
		virtualMachine.PushFunction(byteCode.functions.count - 1);
		return virtualMachine.CallTopFunction();
	}

	public ValueData Pop()
	{
		return virtualMachine.valueStack.buffer[virtualMachine.valueStack.count - 1];
	}
}