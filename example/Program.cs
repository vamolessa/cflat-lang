using System.Collections.Generic;
using System.IO;

namespace interpreter_tools
{
	public sealed class Program
	{
		public static void Main(string[] args)
		{
			var chunk = new ByteCodeChunk();
			var const1 = chunk.AddConstant(new Value(1));
			var const4 = chunk.AddConstant(new Value(4));

			chunk.WriteInstruction(Instruction.LoadConstant, new LineAndColumn(123, 0));
			chunk.WriteConstantIndex(const1, new LineAndColumn(123, 0));
			chunk.WriteInstruction(Instruction.LoadConstant, new LineAndColumn(123, 0));
			chunk.WriteConstantIndex(const4, new LineAndColumn(123, 0));
			chunk.WriteInstruction(Instruction.Add, new LineAndColumn(123, 0));
			chunk.WriteInstruction(Instruction.Return, new LineAndColumn(123, 0));

			var vm = new VirtualMachine();
			vm.Load(chunk);
			vm.Run(true);
		}

		public static void Main2(string[] args)
		{
			var source = File.ReadAllText("script.txt");

			var parser = new LangParser();

			var parseResult = parser.parser.Parse(source, LangScanners.scanners, parser.Expression);
			if (parseResult.isOk)
			{
				System.Console.WriteLine("END SUCCESS");
				System.Console.WriteLine("\nNOW INTERPRETING...\n");

				var environment = new Dictionary<string, object>();
				var evalResult = LangInterpreter.Eval(parseResult.ok, environment);
				if (evalResult.isOk)
					System.Console.WriteLine("SUCCESS EVAL. RETURN\n{0}", evalResult.ok.ToString());
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
