public static class Interpreter
{
	public const int TabSize = 8;

	public static void TestFunction(VirtualMachine vm)
	{
		var x = vm.GetAt(0).asInt;
		var y = vm.GetAt(1).asInt;
		System.Console.WriteLine("HELLO FROM C# {0}, {1}", x, y);
		vm.PushUnit();
	}

	public static void RunSource(string source, bool printDisassembled)
	{
		var pepper = new Pepper();

		pepper.AddFunction(
			"testFunction",
			TestFunction,
			new ValueType(TypeKind.Unit),
			new ValueType(TypeKind.Int),
			new ValueType(TypeKind.Int)
		);

		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.Count > 0)
		{
			var error = CompilerHelper.FormatError(source, compileErrors, 2, TabSize);
			ConsoleHelper.Error("COMPILER ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();

			System.Environment.ExitCode = 65;
			return;
		}

		if (printDisassembled)
		{
			ConsoleHelper.Write(pepper.Disassemble());
			ConsoleHelper.LineBreak();
		}

		var runError = pepper.RunLastFunction();
		if (runError.isSome)
		{
			var error = VirtualMachineHelper.FormatError(source, runError.value, 2, TabSize);
			ConsoleHelper.Error("RUNTIME ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();
			ConsoleHelper.Error(pepper.TraceCallStack());

			System.Environment.ExitCode = 70;
		}
		else
		{
			System.Environment.ExitCode = 0;
		}

		ConsoleHelper.LineBreak();
	}
}