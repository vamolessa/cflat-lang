public static class Interpreter
{
	public const int TabSize = 8;

	public static Return TestTupleFunction<C>(ref C context) where C : IContext
	{
		var t = context.ArgTuple<Tuple<Int, Bool>>();
		var body = context.BodyOfTuple<Tuple<Int, Bool>>();
		System.Console.WriteLine("HELLO FROM C# TUPLE {0}, {1}", t.e0.value, t.e1.value);
		t.e0.value += 1;
		t.e1.value = !t.e1.value;
		return body.Return(t);
	}

	public struct ThisIsATuple : ITuple
	{
		public int n;
		public bool b;

		public void Marshal<M>(ref M marshaler) where M : IMarshaler
		{
			marshaler.Marshal(ref n, null);
			marshaler.Marshal(ref b, null);
		}
	}

	public static Return TestTupleAgainFunction<C>(ref C context) where C : IContext
	{
		var t = context.ArgTuple<ThisIsATuple>();
		var body = context.BodyOfTuple<ThisIsATuple>();
		System.Console.WriteLine("HELLO FROM C# TUPLE 2 {0}, {1}", t.n, t.b);
		t.n += 1;
		t.b = !t.b;
		return body.Return(t);
	}

	public static void RunSource(string source, bool printDisassembled)
	{
		var pepper = new Pepper();
		pepper.DebugMode = true;

		pepper.AddStruct<Point>();
		// pepper.AddFunction(TestFunction, TestFunction);
		// pepper.AddFunction(OtherFunction, OtherFunction);
		// pepper.AddFunction(CallingFunction, CallingFunction);
		// pepper.AddFunction(TestTupleFunction, TestTupleFunction);
		// pepper.AddFunction(TestTupleAgainFunction, TestTupleAgainFunction);

		//var compileErrors = pepper.CompileSource(source);
		var compileErrors = pepper.CompileExpression(source);
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

		pepper.CallFunction(string.Empty).GetStruct<Point>(out var p);
		System.Console.WriteLine("{0} {1} {2}", p.x, p.y, p.z);

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