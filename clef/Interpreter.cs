public static class Interpreter
{
	public const int TabSize = 8;

	public static Return StartStopwatch<C>(ref C context) where C : IContext
	{
		var body = context.BodyOfObject<System.Diagnostics.Stopwatch>();
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();
		return body.Return(sw);
	}

	public static Return StopStopwatch<C>(ref C context) where C : IContext
	{
		var sw = context.ArgObject<System.Diagnostics.Stopwatch>();
		var body = context.BodyOfFloat();
		sw.Stop();
		return body.Return((float)sw.Elapsed.TotalSeconds);
	}

	public static void RunSource(string source, bool printDisassembled)
	{
		var clef = new Clef();

		clef.AddSearchingAssembly(typeof(System.Console));
		clef.AddSearchingAssembly(typeof(Interpreter));

		clef.AddFunction(StartStopwatch, StartStopwatch);
		clef.AddFunction(StopStopwatch, StopStopwatch);

		var compileErrors = clef.CompileSource(source, Mode.Debug);
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
			ConsoleHelper.Write(clef.Disassemble());
			ConsoleHelper.LineBreak();
		}

		var main = clef.GetFunction<Empty, Unit>("main");
		if (main.isSome)
			System.Console.WriteLine("RESULT: {0}", main.value.Call(clef, new Empty()));
		else
			System.Console.WriteLine("NOT FOUNDED");

		var runtimeError = clef.GetError();
		if (runtimeError.isSome)
		{
			var error = VirtualMachineHelper.FormatError(source, runtimeError.value, 2, TabSize);
			ConsoleHelper.Error("RUNTIME ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();
			ConsoleHelper.Error(clef.TraceCallStack());

			System.Environment.ExitCode = 70;
		}
		else
		{
			System.Environment.ExitCode = 0;
		}

		ConsoleHelper.LineBreak();
	}
}