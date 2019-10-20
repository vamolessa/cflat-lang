public static class Interpreter
{
	public const int TabSize = 8;

	public static Return StartStopwatch<C>(ref C context) where C : IContext
	{
		var body = context.Body<Class<System.Diagnostics.Stopwatch>>();
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();
		return body.Return(sw);
	}

	public static Return StopStopwatch<C>(ref C context) where C : IContext
	{
		var sw = context.Arg<Class<System.Diagnostics.Stopwatch>>().value;
		var body = context.Body<Float>();
		sw.Stop();
		return body.Return((float)sw.Elapsed.TotalSeconds);
	}

	public static void RunSource(string sourceName, string source, bool printDisassembled)
	{
		var cflat = new CFlat();

		cflat.AddFunction(StartStopwatch, StartStopwatch);
		cflat.AddFunction(StopStopwatch, StopStopwatch);

		var compileErrors = cflat.CompileSource(sourceName, source, Mode.Release);
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
			ConsoleHelper.Write(cflat.Disassemble());
			ConsoleHelper.LineBreak();
		}

		cflat.Load();
		var main = cflat.GetFunction<Empty, Unit>("main");
		if (main.isSome)
			System.Console.WriteLine("RESULT: {0}", main.value.Call(cflat, new Empty()));
		else
			System.Console.WriteLine("NOT FOUNDED");

		var runtimeError = cflat.GetError();
		if (runtimeError.isSome)
		{
			var error = VirtualMachineHelper.FormatError(source, runtimeError.value, 2, TabSize);
			ConsoleHelper.Error("RUNTIME ERROR\n");
			ConsoleHelper.Error(error);
			ConsoleHelper.LineBreak();
			ConsoleHelper.Error(cflat.TraceCallStack());

			System.Environment.ExitCode = 70;
		}
		else
		{
			System.Environment.ExitCode = 0;
		}

		ConsoleHelper.LineBreak();
	}
}