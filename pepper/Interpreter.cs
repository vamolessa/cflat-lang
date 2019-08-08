using System.Text;

public static class Interpreter
{
	public const int TabSize = 8;

	public static int TestFunction(VirtualMachine vm)
	{
		System.Console.WriteLine("HELLO FROM C#");
		return 0;
	}

	public static void RunSource(string source, bool printDisassembled)
	{
		var compiler = new CompilerController();

		var chunk = new ByteCodeChunk();
		var builder = chunk.BeginAddFunctionType();
		var functionType = chunk.EndAddFunctionType(builder);
		chunk.nativeFunctions.PushBack(new NativeFunction("testFunction", functionType, TestFunction));

		var compileResult = compiler.Compile(source, chunk);
		if (!compileResult.isOk)
		{
			var error = CompilerHelper.FormatError(source, compileResult.error, 2, TabSize);
			ConsoleHelper.Error("COMPILER ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();

			System.Environment.ExitCode = 65;
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

			System.Environment.ExitCode = 70;
		}
		else
		{
			System.Environment.ExitCode = 0;
		}

		ConsoleHelper.LineBreak();
	}
}