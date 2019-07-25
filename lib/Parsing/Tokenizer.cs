using System.Collections.Generic;

public readonly struct Token
{
	public static readonly int EndKind = -1;
	public static readonly int ErrorKind = -2;

	public readonly int kind;
	public readonly int index;
	public readonly int length;

	public Token(int kind, int index, int length)
	{
		this.kind = kind;
		this.index = index;
		this.length = length;
	}

	public bool IsValid()
	{
		return kind >= 0;
	}
}

public interface ITokenizer
{
	Token Next();
}

public sealed class Tokenizer : ITokenizer
{
	private Scanner[] scanners;
	private string source;
	private int nextIndex;

	public void Begin(Scanner[] scanners, string source)
	{
		this.scanners = scanners;
		this.source = source;
		nextIndex = 0;
	}

	public Token Next()
	{
		if (nextIndex >= source.Length)
			return new Token(Token.EndKind, source.Length, 0);

		var tokenLength = 0;
		var tokenKind = Token.ErrorKind;
		foreach (var scanner in scanners)
		{
			var length = scanner.Scan(source, nextIndex);
			if (tokenLength >= length)
				continue;

			tokenLength = length;
			tokenKind = scanner.tokenKind;
		}

		if (tokenKind == Token.ErrorKind)
			tokenLength = 1;

		var token = new Token(tokenKind, nextIndex, tokenLength);
		nextIndex += tokenLength;

		return token;
	}

	public static void Tokenize(string source, Scanner[] scanners, List<Token> tokens, List<int> errorIndexes)
	{
		var lastErrorIndex = int.MinValue;

		for (var index = 0; index < source.Length;)
		{
			var tokenLength = 0;
			var tokenKind = -1;
			foreach (var scanner in scanners)
			{
				var length = scanner.Scan(source, index);
				if (length <= tokenLength)
					continue;

				tokenLength = length;
				tokenKind = scanner.tokenKind;
			}

			if (tokenLength == 0)
			{
				if (index > lastErrorIndex + 1)
					errorIndexes.Add(index);

				lastErrorIndex = index;
				index += 1;
				continue;
			}

			if (tokenKind >= 0)
				tokens.Add(new Token(tokenKind, index, tokenLength));
			index += tokenLength;
		}

		tokens.Add(new Token(Token.EndKind, 0, 0));
	}
}
