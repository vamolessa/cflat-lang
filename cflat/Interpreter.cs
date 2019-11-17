using cflat;
using Stopwatch = System.Diagnostics.Stopwatch;

public static class Interpreter
{
	public static Class<Stopwatch> StartStopwatch()
	{
		var sw = new Stopwatch();
		sw.Start();
		return sw;
	}

	public static Float StopStopwatch(Class<Stopwatch> sw)
	{
		sw.value.Stop();
		return (float)sw.value.Elapsed.TotalSeconds;
	}

	public static void RunSource(string sourceName, string source, bool printDisassembled)
	{
		var debugger = new Debugger((breakpoint, vars) =>
		{
			Debugger.Break();
		});
		debugger.AddBreakpoint(new Debugger.Breakpoint(0, new Slice(14, 13)));
		debugger.AddBreakpoint(new Debugger.Breakpoint(0, new Slice(30, 7)));

		var cflat = new CFlat();
		cflat.AddDebugHook(debugger.DebugHook);

		cflat.AddFunction<Class<Stopwatch>>(nameof(StartStopwatch), StartStopwatch);
		cflat.AddFunction<Class<Stopwatch>, Float>(nameof(StopStopwatch), StopStopwatch);

		var compileErrors = cflat.CompileSource(sourceName, source, Mode.Debug, Option.None);
		if (compileErrors.count > 0)
		{
			var errorMessage = cflat.GetFormattedCompileErrors();
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

		var main = cflat.GetFunction<Empty, Unit>("main");
		if (main.isSome)
			System.Console.WriteLine("RESULT: {0}", main.value.Call(cflat, new Empty()));
		else
			System.Console.WriteLine("NOT FOUNDED");

		var runtimeError = cflat.GetRuntimeError();
		if (runtimeError.isSome)
		{
			var errorMessage = cflat.GetFormattedRuntimeError();
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
