using System.Collections.Generic;

public sealed class Pepper
{
	internal CompilerController compiler = new CompilerController();
	internal VirtualMachine virtualMachine = new VirtualMachine();
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

	public Option<RuntimeError> Run()
	{
		return virtualMachine.RunLastFunction(byteCode);
	}
}