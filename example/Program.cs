
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace interpreter_tools
{
	public sealed class Program
	{
		public static void Main(string[] args)
		{
			var source = File.ReadAllText("example/script.lisp");

			var parser = new LispParser();

			/*
			var tokens = Tokenizer.Tokenize(parser.scanners, source);
			if (tokens.IsOk)
			{
				System.Console.WriteLine("HERE COMES TOKENS");
				foreach (var t in tokens.ok)
					System.Console.WriteLine(source.Substring(t.index, t.length));
				System.Console.WriteLine("---");
			}
			*/

			var parseResult = parser.Parse(source);
			if (parseResult.IsOk)
			{
				System.Console.WriteLine("END SUCCESS");
				//PrintAst(parseResult.ok);

				System.Console.WriteLine("\nNOW INTERPRETING...\n");

				var environment = new Dictionary<string, Expression>();
				var evalResult = LispInterpreter.Eval(parseResult.ok, environment);
				if (evalResult.IsOk)
					System.Console.WriteLine("SUCCESS EVAL. RETURN\n{0}", evalResult.ok.underlying.ToString());
				else
					System.Console.WriteLine("DEU RUIM EVAL. ERROR\n{0}", evalResult.error);
			}
			else
			{
				System.Console.WriteLine("END DEU RUIM: error\n{0}", parseResult.error);
			}
		}

		private static void PrintAst(Expression e)
		{
			var sb = new StringBuilder();
			PrintAstRecursive(e, sb, 0);
			System.Console.WriteLine(sb);
		}

		private static void PrintAstRecursive(Expression e, StringBuilder sb, int indentationLevel)
		{
			if (e is IdentifierExpression)
			{
				var i = e as IdentifierExpression;
				Indent(sb, indentationLevel);
				sb.AppendLine(i.name);
			}
			else if (e is ValueExpression)
			{
				var v = e as ValueExpression;
				Indent(sb, indentationLevel);
				sb.Append("[");
				sb.Append(v.underlying.GetType());
				sb.Append("] ");
				sb.Append(v.underlying);
				sb.AppendLine();
			}
			else if (e is ListExpression)
			{
				var l = e as ListExpression;
				Indent(sb, indentationLevel);
				sb.AppendLine("(");
				foreach (var c in l.expressions)
					PrintAstRecursive(c, sb, indentationLevel + 1);
				Indent(sb, indentationLevel);
				sb.AppendLine(")");
			}
		}

		private static void Indent(StringBuilder sb, int indentationLevel)
		{
			for (var i = 0; i < indentationLevel; i++)
				sb.Append("  ");
		}
	}
}
