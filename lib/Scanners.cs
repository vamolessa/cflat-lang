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
		while (index < input.Length && char.IsWhiteSpace(input, index))
			index += 1;
		return index - startIndex;
	}
}

public sealed class ExactScanner : Scanner
{
	public readonly string match;

	public ExactScanner(string match)
	{
		this.match = match;
	}

	public override int Scan(string input, int index)
	{
		return ScannerHelper.StartsWith(input, index, match) ?
			match.Length :
			0;
	}
}

public sealed class EnclosedScanner : Scanner
{
	public readonly string beginMatch;
	public readonly string endMatch;

	public EnclosedScanner(string beginMatch, string endMatch)
	{
		this.beginMatch = beginMatch;
		this.endMatch = endMatch;
	}

	public override int Scan(string input, int index)
	{
		if (!ScannerHelper.StartsWith(input, index, beginMatch))
			return 0;

		for (var i = index + beginMatch.Length; i < input.Length; i++)
		{
			if (ScannerHelper.StartsWith(input, i, endMatch) && input[i - 1] != '\\')
				return i - index + endMatch.Length;
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