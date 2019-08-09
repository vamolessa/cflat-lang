using System.Collections.Generic;

public sealed class Pepper
{
	public readonly VirtualMachine virtualMachine = new VirtualMachine();
	internal readonly CompilerController compiler = new CompilerController();
	internal ByteCodeChunk byteCode = new ByteCodeChunk();
	internal string source;

	public List<CompileError> CompileSource(string source)
	{
		this.source = source;
		return compiler.Compile(source, byteCode);
	}

	public List<CompileError> CompileExpression(string source)
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

		return virtualMachine.RunFunction(byteCode, byteCode.functions.count - 1);
	}
}