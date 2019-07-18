using System.Collections.Generic;

public readonly struct Token
{
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
	public static Result<List<Token>, List<int>> Tokenize(Scanner[] scanners, string source)
	{
		var tokens = new List<Token>();
		var errorIndexes = new List<int>();
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

		if (errorIndexes.Count > 0)
			return Result.Error(errorIndexes);
		return Result.Ok(tokens);
	}
}
