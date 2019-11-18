using System.IO;
using Stopwatch = System.Diagnostics.Stopwatch;
using cflat;

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

	public static void RunSource(string sourcePath, string sourceContent, bool printDisassembled)
	{
		var filename = Path.GetFileNameWithoutExtension(sourcePath);
		var source = new Source(new Uri(filename), sourceContent);

		var debugger = new Debugger((breakpoint, vars) =>
		{
			Debugger.Break();
		});
		debugger.AddBreakpoint(new SourcePosition(source.uri, 3));

		var cflat = new CFlat();
		cflat.SetDebugger(debugger);

		cflat.AddFunction<Class<Stopwatch>>(nameof(StartStopwatch), StartStopwatch);
		cflat.AddFunction<Class<Stopwatch>, Float>(nameof(StopStopwatch), StopStopwatch);

		var compileErrors = cflat.CompileSource(source, Mode.Debug, Option.None);
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
