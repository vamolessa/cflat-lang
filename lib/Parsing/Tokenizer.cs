using System.Collections.Generic;

public readonly struct Token
{
	public static readonly Token EndToken = new Token(-1, 0, 0);

	public readonly int kind;
	public readonly int index;
	public readonly int length;

	public Token(int kind, int index, int length)
	{
		this.kind = kind;
		this.index = index;
		this.length = length;
	}
}

public static class Tokenizer
{
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

		tokens.Add(Token.EndToken);
	}
}
