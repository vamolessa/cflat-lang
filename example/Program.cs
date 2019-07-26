using System.Text;

public sealed class Program
{
	private static readonly bool DEBUG = true;

	private static int a;
	public static ref int Set()
	{
		System.Console.WriteLine("SET");
		return ref a;
	}

	public static int Other()
	{
		System.Console.WriteLine("OTHER");
		return 1;
	}

	public static void Main(string[] args)
	{
		var b = Set() += Other();
		System.Console.WriteLine(b);
		return;
		/*
				LangParseRules.InitRules();

				var source = System.IO.File.ReadAllText("script.txt");
				var tokenizer = new Tokenizer();
				var compiler = new LangCompiler();

				var compileResult = compiler.Compile(source, tokenizer);
				if (!compileResult.isOk)
				{
					var error = CompilerHelper.FormatError(source, compileResult.error, 2);
					System.Console.WriteLine("COMPILE ERROR");
					System.Console.WriteLine(error);
					return;
				}

				if (DEBUG)
				{
					var sb = new StringBuilder();
					compileResult.ok.Disassemble(source, "script", sb);
					System.Console.WriteLine(sb);
				}

				var vm = new VirtualMachine();
				var runResult = vm.Run(source, compileResult.ok);
				if (!runResult.isOk)
				{
					var error = VirtualMachineHelper.FormatError(source, runResult.error, 2);
					System.Console.WriteLine("RUNTIME ERROR");
					System.Console.WriteLine(error);
				}
			*/
	}
}
