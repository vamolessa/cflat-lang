using System.Diagnostics;

public static class Interpreter
{
	public const byte TabSize = 8;

	public static Class<Stopwatch> StartStopwatch(VirtualMachine vm)
	{
		var sw = new Stopwatch();
		sw.Start();
		return sw;
	}

	public static Float StopStopwatch(VirtualMachine vm, Class<Stopwatch> sw)
	{
		sw.value.Stop();
		return (float)sw.value.Elapsed.TotalSeconds;
	}

	public static void RunSource(string sourceName, string source, bool printDisassembled)
	{
		var cflat = new CFlat();

		cflat.AddFunction<Class<Stopwatch>>(nameof(StartStopwatch), StartStopwatch);
		cflat.AddFunction<Class<Stopwatch>, Float>(nameof(StopStopwatch), StopStopwatch);

		var compileErrors = cflat.CompileSource(sourceName, source, Mode.Debug);
		if (compileErrors.count > 0)
		{
			var errorMessage = cflat.GetFormattedCompileErrors(TabSize);
			ConsoleHelper.Error("COMPILER ERROR\n");
			ConsoleHelper.Error(errorMessage);
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
			var errorMessage = cflat.GetFormattedRuntimeError(TabSize);
			ConsoleHelper.Error("RUNTIME ERROR\n");
			ConsoleHelper.Error(errorMessage);
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