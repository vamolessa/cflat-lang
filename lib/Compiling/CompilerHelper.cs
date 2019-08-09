using System.Collections.Generic;
using System.Globalization;
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
	public static bool AreEqual(string source, Slice a, Slice b)
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

	public static bool AreEqual(string source, Slice slice, string other)
	{
		if (slice.length != other.Length)
			return false;

		for (var i = 0; i < slice.length; i++)
		{
			if (source[slice.index + i] != other[i])
				return false;
		}

		return true;
	}

	public static string GetSlice(Compiler compiler, Slice slice)
	{
		return compiler.parser.tokenizer.source.Substring(slice.index, slice.length);
	}

	public static string GetPreviousSlice(Compiler compiler)
	{
		var slice = compiler.parser.previousToken.slice;
		return compiler.parser.tokenizer.source.Substring(slice.index, slice.length);
	}

	public static int GetInt(Compiler compiler)
	{
		var sub = GetPreviousSlice(compiler);
		int.TryParse(sub, out var value);
		return value;
	}

	public static float GetFloat(Compiler compiler)
	{
		var sub = GetPreviousSlice(compiler);
		float.TryParse(
			sub,
			NumberStyles.Float,
			CultureInfo.InvariantCulture.NumberFormat,
			out var value);
		return value;
	}

	public static string GetString(Compiler compiler)
	{
		var slice = new Slice(
			compiler.parser.previousToken.slice.index + 1,
			compiler.parser.previousToken.slice.length - 2
		);
		return GetSlice(compiler, slice);
	}

	public static LineAndColumn GetLineAndColumn(string source, int index, int tabSize)
	{
		var line = 1;
		var column = 1;

		for (var i = 0; i < index; i++)
		{
			column += source[i] == '\t' ? tabSize : 1;

			if (source[i] == '\n')
			{
				line += 1;
				column = 1;
			}
		}

		return new LineAndColumn(line, column);
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

	public static string FormatError(string source, List<CompileError> errors, int contextSize, int tabSize)
	{
		if (errors == null)
			return "";

		var sb = new StringBuilder();

		foreach (var e in errors)
		{
			var position = GetLineAndColumn(source, e.slice.index, tabSize);
			var lines = GetLines(
				source,
				System.Math.Max(position.line - contextSize, 0),
				System.Math.Max(position.line - 1, 0)
			);

			sb.Append(e.message);
			sb.Append(" (line: ");
			sb.Append(position.line);
			sb.Append(", column: ");
			sb.Append(position.column);
			sb.AppendLine(")");

			sb.AppendLine(lines);
			sb.Append(' ', position.column - 1);
			sb.Append('^', e.slice.length > 0 ? e.slice.length : 1);
			sb.Append(" here\n\n");
		}

		return sb.ToString();
	}
}
