using System.Collections.Generic;
using System.IO;

namespace interpreter_tools
{
	public sealed class Program
	{
		public static void Main(string[] args)
		{
			var source = File.ReadAllText("script.txt");

			var tokenizer = new LangTokenizer();
			var parser = new LangParser();

			var parseResult = parser.parser.Parse(source, tokenizer.scanners, parser.Expression);
			if (parseResult.isOk)
			{
				System.Console.WriteLine("END SUCCESS");
				System.Console.WriteLine("\nNOW INTERPRETING...\n");

				var environment = new Dictionary<string, Expression>();
				var evalResult = LangInterpreter.Eval(parseResult.ok, environment);
				if (evalResult.isOk)
					System.Console.WriteLine("SUCCESS EVAL. RETURN\n{0}", evalResult.ok.value.ToString());
				else
					System.Console.WriteLine("DEU RUIM EVAL. ERROR\n{0}", evalResult.error);
			}
			else
			{
				System.Console.WriteLine("END DEU RUIM: error\n{0}", parseResult.error);
			}
		}
	}
}
