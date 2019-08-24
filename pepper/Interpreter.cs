public static class Interpreter
{
	public const int TabSize = 8;

	// public sealed class StartThing : INativeFunction
	// {
	// 	public SomeCall<Tuple<Int>, Int> someFunction = new SomeCall<Tuple<Int>, Int>("some_function");

	// 	public Return Call<C>(ref C context) where C : IContext
	// 	{
	// 		var body = context.BodyOfObject<System.Diagnostics.Stopwatch>();
	// 		var sw = new System.Diagnostics.Stopwatch();
	// 		sw.Start();
	// 		return body.Return(sw);
	// 	}
	// }

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
		var pepper = new Pepper();

		pepper.AddFunction(StartStopwatch, StartStopwatch);
		pepper.AddFunction(StopStopwatch, StopStopwatch);

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

		//pepper.CallFunction("main").Get();
		var main = pepper.GetFunction<Unit>("main");
		if (main.isSome)
			main.value.Call(pepper);
		else
			System.Console.WriteLine("NOT FOUNDED");

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