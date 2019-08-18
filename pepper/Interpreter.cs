public static class Interpreter
{
	public const int TabSize = 8;

	public struct Point : IMarshalable
	{
		public int x;
		public int y;
		public int z;

		void IMarshalable.Read(ref StackReader reader)
		{
			x = reader.ReadInt();
			y = reader.ReadInt();
			z = reader.ReadInt();
		}

		void IMarshalable.Push(VirtualMachine vm)
		{
			vm.PushInt(x);
			vm.PushInt(y);
			vm.PushInt(z);
		}

		void IMarshalable.Pop(VirtualMachine vm)
		{
			z = vm.PopInt();
			y = vm.PopInt();
			x = vm.PopInt();
		}
	}

	public static void TestFunction(VirtualMachine vm)
	{
		var reader = vm.ReadArgs();
		var p = reader.ReadStruct<Point>();
		System.Console.WriteLine("HELLO FROM C# {0}, {1}, {2}", p.x, p.y, p.z);
		vm.PushString("HEEEY YEAHAH!");
	}

	struct FunctionDefinition : System.IDisposable
	{
		public FunctionDefinition([System.Runtime.CompilerServices.CallerMemberName] string functionName = "")
		{
		}

		void System.IDisposable.Dispose()
		{
		}
	}

/*
	public static void OtherFunction<F>(VirtualMachine vm, F f)
	{
		f.Arg(out int x);
		f.Arg(out Point p);
		f.ReturnsInt();
	}
*/

	public static void RunSource(string source, bool printDisassembled)
	{
		var pepper = new Pepper();

		pepper.AddFunction(
			"testFunction",
			TestFunction,
			new ValueType(TypeKind.String),
			new ValueType(TypeKind.Int),
			new ValueType(TypeKind.Int),
			new ValueType(TypeKind.Int)
		);

		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.Count > 0)
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

		var runError = pepper.RunLastFunction();
		if (runError.isSome)
		{
			var error = VirtualMachineHelper.FormatError(source, runError.value, 2, TabSize);
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