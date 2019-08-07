using System.Text;

public sealed class Program
{
	private const int TabSize = 8;

	public static void Main(string[] args)
	{
		var source = System.IO.File.ReadAllText("script.txt");
		var compiler = new Compiler();

		var compileResult = compiler.Compile(source);
		if (!compileResult.isOk)
		{
			var error = CompilerHelper.FormatError(source, compileResult.error, 2, TabSize);
			System.Console.WriteLine("COMPILE ERROR");
			System.Console.WriteLine(error);
			return;
		}

		var sb = new StringBuilder();
		compileResult.ok.Disassemble(source, "script", sb);
		System.Console.WriteLine(sb);

		var vm = new VirtualMachine();
		var runResult = vm.Run(compileResult.ok, "main");
		if (!runResult.isOk)
		{
			var error = VirtualMachineHelper.FormatError(source, runResult.error, 2, TabSize);
			System.Console.WriteLine("RUNTIME ERROR");
			System.Console.WriteLine(error);
			System.Console.WriteLine(VirtualMachineHelper.TraceCallStack(vm, source));
		}
	}
}
