using System.Collections.Generic;
using System.IO;

namespace interpreter_tools
{
	public sealed class Program
	{
		public static void Main(string[] args)
		{
			var source = File.ReadAllText("script.mn");

			var tokenizer = new ExampleTokenizer();
			var parser = new ExampleParser(tokenizer);

			/*
			var tokens = tokenizer.Tokenize(source);
			if (tokens.IsOk)
			{
				System.Console.WriteLine("HERE COMES TOKENS");
				foreach (var t in tokens.ok)
					System.Console.WriteLine(source.Substring(t.index, t.length));
				System.Console.WriteLine("---");
			}
			*/

			var parseResult = parser.Parse(source);
			if (parseResult.isOk)
			{
				System.Console.WriteLine("END SUCCESS");
				//PrintAst(parseResult.ok);

				System.Console.WriteLine("\nNOW INTERPRETING...\n");

				var environment = new Dictionary<string, Expression>();
				var evalResult = ExampleInterpreter.Eval(parseResult.ok, environment);
				if (evalResult.isOk)
					System.Console.WriteLine("SUCCESS EVAL. RETURN\n{0}", evalResult.ok.underlying.ToString());
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
