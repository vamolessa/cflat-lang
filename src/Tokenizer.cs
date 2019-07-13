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
	public readonly struct Result
	{
		public readonly List<Token> tokens;
		public readonly int errorIndex;

		public bool IsSuccess
		{
			get { return tokens != null && errorIndex < 0; }
		}

		public Result(List<Token> tokens)
		{
			this.tokens = tokens;
			this.errorIndex = -1;
		}

		public Result(int errorIndex)
		{
			this.tokens = null;
			this.errorIndex = errorIndex;
		}
	}

	public static Result Tokenize(string source, Scanner[] scanners)
	{
		var tokens = new List<Token>();

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
				return new Result(index);

			if (tokenKind >= 0)
				tokens.Add(new Token(tokenKind, index, tokenLength));
			index += tokenLength;
		}

		return new Result(tokens);
	}
}
