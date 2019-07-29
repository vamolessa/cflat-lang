using System.Collections.Generic;
using System.Text;

public readonly struct LineAndColumn
{
	public readonly int line;
	public readonly int column;

	public LineAndColumn(int line, int column)
	{
		this.line = line;
		this.column = column;
	}

	public override string ToString()
	{
		return string.Format("line {0} column: {1}", line, column);
	}
}

public static class CompilerHelper
{
	public static bool AreEqual(string source, Token a, Token b)
	{
		if (a.length != b.length)
			return false;

		for (var i = 0; i < a.length; i++)
		{
			if (source[a.index + i] != source[b.index + i])
				return false;
		}

		return true;
	}

	public static int GetInt(Compiler compiler)
	{
		var source = compiler.tokenizer.Source;
		var sub = source.Substring(
			compiler.previousToken.index,
			compiler.previousToken.length
		);
		return int.Parse(sub);
	}

	public static float GetFloat(Compiler compiler)
	{
		var source = compiler.tokenizer.Source;
		var sub = source.Substring(
			compiler.previousToken.index,
			compiler.previousToken.length
		);
		return float.Parse(sub);
	}

	public static string GetString(Compiler compiler)
	{
		var source = compiler.tokenizer.Source;
		return source.Substring(
			compiler.previousToken.index + 1,
			compiler.previousToken.length - 2
		);
	}

	public static LineAndColumn GetLineAndColumn(string source, int index)
	{
		var line = 1;
		var lastNewLineIndex = 0;

		for (var i = 0; i < index; i++)
		{
			if (source[i] == '\n')
			{
				lastNewLineIndex = i;
				line += 1;
			}
		}

		return new LineAndColumn(line, index - lastNewLineIndex + 1);
	}

	public static string GetLines(string source, int startLine, int endLine)
	{
		var startLineIndex = 0;
		var lineCount = 0;

		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] != '\n')
				continue;

			if (lineCount == endLine)
				return source.Substring(startLineIndex, i - startLineIndex);

			lineCount += 1;

			if (lineCount == startLine)
				startLineIndex = i + 1;
		}

		if (lineCount >= startLine && lineCount <= endLine)
			return source.Substring(startLineIndex);

		return "";
	}

	public static int LengthUntilIndex(string source, int index, int tabSize)
	{
		var count = 0;
		for (var i = 0; i < index; i++)
			count += source[i] == '\t' ? tabSize : 1;
		return count;
	}

	public static string FormatError(string source, List<CompileError> errors, int contextSize, int tabSize)
	{
		if (errors == null)
			return "";

		var sb = new StringBuilder();

		foreach (var e in errors)
		{
			var position = GetLineAndColumn(source, e.token.index);
			var lines = GetLines(
				source,
				System.Math.Max(position.line - 1 - contextSize, 0),
				System.Math.Max(position.line - 1, 0)
			);
			var line = GetLines(
				source,
				System.Math.Max(position.line - 1, 0),
				System.Math.Max(position.line - 1, 0)
			);

			sb.Append(e.message);
			sb.Append(" (line: ");
			sb.Append(position.line);
			sb.Append(", column: ");
			sb.Append(position.column);
			sb.AppendLine(")");

			sb.AppendLine(lines);
			sb.Append(' ', LengthUntilIndex(source, position.column - 2, tabSize));
			sb.Append('^', e.token.length > 0 ? e.token.length : 1);
			sb.Append(" here ");
			sb.Append(e.token.index);
			sb.Append("\n\n");
		}

		return sb.ToString();
	}
}
