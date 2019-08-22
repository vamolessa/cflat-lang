public static class Interpreter
{
	public const int TabSize = 8;

	public struct Point : IMarshalable
	{
		public int x;
		public int y;
		public int z;

		public void Marshal<M>(ref M marshaler) where M : IMarshaler
		{
			marshaler.Marshal(ref x, nameof(x));
			marshaler.Marshal(ref y, nameof(y));
			marshaler.Marshal(ref z, nameof(z));
		}
	}

	public static Return TestFunction<C>(ref C context) where C : IContext
	{
		var p = context.ArgStruct<Point>();
		var body = context.BodyOfStruct<Point>();
		System.Console.WriteLine("HELLO FROM C# {0}, {1}, {2}", p.x, p.y, p.z);
		p.x += 1;
		p.y += 1;
		p.z += 1;
		return body.Return(p);
	}

	public sealed class SomeTestClass { }

	public static Return OtherFunction<C>(ref C context) where C : IContext
	{
		var body = context.BodyOfObject<SomeTestClass>();
		return body.Return(new SomeTestClass());
	}

	public static Return CallingFunction<C>(ref C context) where C : IContext
	{
		var body = context.Body();
		var success = body.Call("some_function").WithInt(6).GetInt(out var n);
		System.Console.WriteLine("CALLED FUNCTION success:{0} return:{1}", success, n);
		return body.Return();
	}

	public static void RunSource(string source, bool printDisassembled)
	{
		var pepper = new Pepper();
		//pepper.DebugMode = true;

		pepper.AddFunction(TestFunction, TestFunction);
		pepper.AddFunction(OtherFunction, OtherFunction);
		pepper.AddFunction(CallingFunction, CallingFunction);

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

		pepper.CallFunction("main").Get();
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