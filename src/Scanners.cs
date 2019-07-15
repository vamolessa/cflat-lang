public abstract class Scanner
{
	public int tokenKind;

	public abstract int Scan(string input, int index);

	public Scanner WithToken(int tokenKind)
	{
		this.tokenKind = tokenKind;
		return this;
	}

	public Scanner Ignore()
	{
		this.tokenKind = -1;
		return this;
	}
}

public sealed class WhiteSpaceScanner : Scanner
{
	public override int Scan(string input, int index)
	{
		var startIndex = index;
		while (char.IsWhiteSpace(input, index))
			index += 1;
		return index - startIndex;
	}
}

public sealed class CharScanner : Scanner
{
	public readonly char ch;

	public CharScanner(char ch)
	{
		this.ch = ch;
	}

	public override int Scan(string input, int index)
	{
		return input[index] == ch ? 1 : 0;
	}
}

public sealed class CommentScanner : Scanner
{
	public readonly string start;

	public CommentScanner(string start)
	{
		this.start = start;
	}

	public override int Scan(string input, int index)
	{
		if (input.Length - index < start.Length)
			return 0;

		for (var i = 0; i < start.Length; i++)
		{
			if (start[i] != input[index + i])
				return 0;
		}

		var startIndex = index;
		index += start.Length;
		while (index < input.Length && input[index] != '\n')
			index += 1;

		return index - startIndex + 1;
	}
}

public sealed class ExactScanner : Scanner
{
	public readonly string str;

	public ExactScanner(string str)
	{
		this.str = str;
	}

	public override int Scan(string input, int index)
	{
		var count = str.Length;
		if (input.Length - index < str.Length)
			return 0;

		for (var i = 0; i < str.Length; i++)
		{
			if (str[i] != input[index + i])
				return 0;
		}

		return str.Length;
	}
}

public sealed class EnclosedScanner : Scanner
{
	public readonly char delimiter;

	public EnclosedScanner(char delimiter)
	{
		this.delimiter = delimiter;
	}

	public override int Scan(string input, int index)
	{
		if (input[index] != delimiter)
			return 0;

		for (var i = index + 1; i < input.Length; i++)
		{
			if (input[i] == delimiter && input[i - 1] != '\\')
				return i - index + 1;
		}

		return 0;
	}
}

public sealed class IntegerNumberScanner : Scanner
{
	public override int Scan(string input, int index)
	{
		var firstCh = input[index];
		if (!char.IsDigit(firstCh) || firstCh == '+' || firstCh == '-')
			return 0;

		for (var i = index + 1; i < input.Length; i++)
		{
			if (!char.IsDigit(input, i))
				return i - index;
		}

		return input.Length - index;
	}
}

public sealed class RealNumberScanner : Scanner
{
	public override int Scan(string input, int index)
	{
		var firstCh = input[index];
		if (!char.IsDigit(firstCh) || firstCh == '+' || firstCh == '-')
			return 0;

		for (var i = index + 1; i < input.Length; i++)
		{
			if (!char.IsDigit(input, i))
				return i - index;
		}

		return input.Length - index;
	}
}


public sealed class IdentifierScanner : Scanner
{
	public readonly string extraChars;

	public IdentifierScanner(string additionalChars)
	{
		this.extraChars = additionalChars;
	}

	public override int Scan(string input, int index)
	{
		var firstCh = input[index];
		if (!char.IsLetter(firstCh) && extraChars.IndexOf(firstCh) < 0)
			return 0;

		for (var i = index + 1; i < input.Length; i++)
		{
			var ch = input[i];
			if (!char.IsLetterOrDigit(ch) && extraChars.IndexOf(ch) < 0)
				return i - index;
		}

		return input.Length - index;
	}
}