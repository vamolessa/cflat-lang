using System.Text;

namespace cflat
{
	public readonly struct LineAndColumn
	{
		public readonly ushort lineIndex;
		public readonly ushort columnIndex;
		public readonly ushort formattedColumnIndex;

		public LineAndColumn(ushort lineIndex, ushort columnIndex, ushort formattedColumnIndex)
		{
			this.lineIndex = lineIndex;
			this.columnIndex = columnIndex;
			this.formattedColumnIndex = formattedColumnIndex;
		}
	}

	public readonly struct CompileError
	{
		public readonly int sourceIndex;
		public readonly Slice slice;
		public readonly string message;

		public CompileError(int sourceIndex, Slice slice, string message)
		{
			this.sourceIndex = sourceIndex;
			this.slice = slice;
			this.message = message;
		}
	}

	public readonly struct RuntimeError
	{
		public readonly int instructionIndex;
		public readonly Slice slice;
		public readonly string message;

		public RuntimeError(int instructionIndex, Slice slice, string message)
		{
			this.instructionIndex = instructionIndex;
			this.slice = slice;
			this.message = message;
		}
	}

	public static class FormattingHelper
	{
		private const int FormattedTabSize = 4;

		public static Slice Trim(string source, Slice slice)
		{
			var startIndex = (int)slice.index;
			var endIndex = slice.index + slice.length - 1;

			while (startIndex < endIndex && char.IsWhiteSpace(source, startIndex))
				startIndex += 1;

			while (endIndex > startIndex && char.IsWhiteSpace(source, endIndex))
				endIndex -= 1;

			return new Slice(startIndex, endIndex - startIndex + 1);
		}

		public static LineAndColumn GetLineAndColumn(string source, int index)
		{
			ushort line = 0;
			ushort column = 0;
			ushort formattedColumn = 0;

			for (var i = 0; i < index; i++)
			{
				column += 1;

				switch (source[i])
				{
				case '\t':
					formattedColumn += FormattedTabSize;
					break;
				case '\r':
					break;
				case '\n':
					line += 1;
					column = 0;
					formattedColumn = 0;
					break;
				default:
					formattedColumn += 1;
					break;
				}
			}

			return new LineAndColumn(line, column, formattedColumn);
		}

		public static Slice GetLineSlice(string source, ushort lineIndex)
		{
			var lineStartIndex = 0;

			while (lineIndex > 0)
			{
				while (
					lineStartIndex < source.Length &&
					source[lineStartIndex++] != '\n'
				)
				{ }

				lineIndex -= 1;
			}

			if (lineStartIndex >= source.Length)
				return new Slice();

			var lineEndIndex = lineStartIndex;
			while (
				lineEndIndex < source.Length &&
				source[lineEndIndex] != '\n'
			)
				lineEndIndex += 1;

			return new Slice(lineStartIndex, lineEndIndex - lineStartIndex);
		}

		public static void AddHighlightSlice(string sourceName, string source, Slice slice, StringBuilder sb)
		{
			var startPosition = GetLineAndColumn(source, slice.index);
			var endPosition = GetLineAndColumn(source, slice.index + slice.length);

			if (slice.length == 0)
				slice = new Slice(slice.index, (ushort)1);

			sb.AppendLine();
			sb.Append("  ");
			sb.Append(sourceName);
			sb.Append(':');
			sb.Append(startPosition.lineIndex + 1);
			sb.Append(':');
			sb.Append(startPosition.columnIndex + 1);
			sb.AppendLine();

			sb.AppendLine("    |");
			for (var i = startPosition.lineIndex; i <= endPosition.lineIndex; i++)
			{
				var lineSlice = GetLineSlice(source, i);

				sb.AppendFormat("{0,4}", i + 1);
				sb.Append("| ");
				sb.Append(source, lineSlice.index, lineSlice.length);
				sb.AppendLine();

				sb.Append("    | ");

				if (i == startPosition.lineIndex)
				{
					sb.Append(' ', startPosition.formattedColumnIndex);
					var endColumnIndex = startPosition.lineIndex == endPosition.lineIndex ?
						endPosition.formattedColumnIndex :
						GetLineAndColumn(source, lineSlice.index + lineSlice.length).formattedColumnIndex;

					sb.Append('^', endColumnIndex - startPosition.formattedColumnIndex);
				}
				else if (i == endPosition.lineIndex)
				{
					var endColumnIndex = GetLineAndColumn(source, slice.index + slice.length).formattedColumnIndex;
					sb.Append('^', endColumnIndex);
				}
				else
				{
					var endColumnIndex = GetLineAndColumn(source, lineSlice.index + lineSlice.length).formattedColumnIndex;
					sb.Append('^', endColumnIndex);
				}
				sb.AppendLine();
			}

			sb.Replace("\t", new string(' ', FormattedTabSize));
			sb.AppendLine();
		}
	}
}