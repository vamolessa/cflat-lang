using System.Diagnostics;

public static class Interpreter
{
	public const int TabSize = 8;

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

	public static Int DoubleAndRound(VirtualMachine vm, Float f)
	{
		return (int)System.MathF.Round(f.value * 2.0f);
	}

	public static Tuple<Int, Bool> TupleTestFunction(VirtualMachine vm, Tuple<Int, Bool> tuple)
	{
		tuple.e0.value += 1;
		tuple.e1.value = !tuple.e1.value;
		return tuple;
	}

	public static void RunSource(string sourceName, string source, bool printDisassembled)
	{
		var cflat = new CFlat();

		cflat.AddFunction<Class<Stopwatch>>(nameof(StartStopwatch), StartStopwatch);
		cflat.AddFunction<Class<Stopwatch>, Float>(nameof(StopStopwatch), StopStopwatch);
		cflat.AddFunction<Float, Int>(nameof(DoubleAndRound), DoubleAndRound);
		cflat.AddFunction<Tuple<Int, Bool>, Tuple<Int, Bool>>(nameof(TupleTestFunction), TupleTestFunction);

		var compileErrors = cflat.CompileSource(sourceName, source, Mode.Release);
		if (compileErrors.count > 0)
		{
			var error = FormattingHelper.FormatCompileError(source, compileErrors, 2, TabSize);
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
			var error = FormattingHelper.FormatRuntimeError(source, runtimeError.value, 2, TabSize);
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