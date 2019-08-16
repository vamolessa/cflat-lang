public static class Interpreter
{
	public const int TabSize = 8;

	public struct Point : IMarshalable
	{
		public int x;
		public int y;
		public int z;

		int IMarshalable.Size { get { return 3; } }

		void IMarshalable.Read(ref Marshal marshal)
		{
			marshal.Read(out x);
			marshal.Read(out y);
			marshal.Read(out z);
		}

		void IMarshalable.Write(ref Marshal marshal)
		{
			marshal.Write(x);
			marshal.Write(y);
			marshal.Write(z);
		}
	}

	public static void TestFunction(VirtualMachine vm)
	{
		vm.MarshalArgs().Read(out Point p);
		System.Console.WriteLine("HELLO FROM C# {0}, {1}, {2}", p.x, p.y, p.z);
		vm.Marshal().Push("hey!!");
	}

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