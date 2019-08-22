public static class Interpreter
{
	public const int TabSize = 8;

	public static void RunSource(string source, bool printDisassembled)
	{
		var pepper = new Pepper();
		pepper.DebugMode = true;

		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.count > 0)
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

		pepper.CallFunction(string.Empty).Get();
		var runtimeError = pepper.GetError();
		if (runtimeError.isSome)
		{
			var error = VirtualMachineHelper.FormatError(source, runtimeError.value, 2, TabSize);
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