using System.Text;

public sealed class Program
{
	private static readonly bool DEBUG = true;

	public static void Main(string[] args)
	{
		LangParseRules.InitRules();

		var source = System.IO.File.ReadAllText("script.txt");
		var tokenizer = new Tokenizer();
		var compiler = new LangCompiler();

		var result = compiler.Compile(source, tokenizer);
		if (!result.isOk)
		{
			var error = CompilerHelper.FormatError(source, result.error, 2);
			System.Console.WriteLine(error);
			return;
		}

		if (DEBUG)
		{
			var sb = new StringBuilder();
			result.ok.Disassemble(source, "script", sb);
			System.Console.WriteLine(sb);
		}

		var vm = new VirtualMachine();
		vm.Run(source, result.ok);
	}
}
