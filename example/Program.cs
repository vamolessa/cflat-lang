using System.Collections.Generic;
using System.Text;

namespace interpreter_tools
{
	public sealed class Program
	{
		public static void Main(string[] args)
		{
			var source = "(print 12 )";

			var parser = new LispParser();
			var result = parser.Parse(source);
			if (result.IsSuccess)
			{
				System.Console.WriteLine("END SUCCESS");
				PrintAst(result.parsed);

				System.Console.WriteLine("\nNOW INTERPRETING...");

				var environment = new Dictionary<string, Expression>();
				LispInterpreter.Eval(result.parsed, environment);
			}
			else
			{
				System.Console.WriteLine("END DEU RUIM: error '{0}'", result.errorMessage);
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
