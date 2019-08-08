using System.Text;

public static class Interpreter
{
	public const int TabSize = 8;

	public static void RunSource(string source, bool printDisassembled)
	{
		var compiler = new CompilerController();

		var compileResult = compiler.Compile(source);
		if (!compileResult.isOk)
		{
			var error = CompilerHelper.FormatError(source, compileResult.error, 2, TabSize);
			ConsoleHelper.Error("COMPILER ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();
			return;
		}

		if (printDisassembled)
		{
			var sb = new StringBuilder();
			compileResult.ok.Disassemble(source, "script", sb);
			ConsoleHelper.Write(sb.ToString());
			ConsoleHelper.LineBreak();
		}

		var vm = new VirtualMachine();
		var runResult = vm.RunLastFunction(compileResult.ok);
		if (!runResult.isOk)
		{
			var error = VirtualMachineHelper.FormatError(source, runResult.error, 2, TabSize);
			ConsoleHelper.Error("RUNTIME ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();
			ConsoleHelper.Error(VirtualMachineHelper.TraceCallStack(vm, source));
		}

		ConsoleHelper.LineBreak();
	}
}