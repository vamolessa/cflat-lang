public interface ITokenizer
{
	string Source { get; }
	void Reset(Scanner[] scanners, string source);
	Token Next();
}

public sealed class Tokenizer : ITokenizer
{
	private Scanner[] scanners;
	private string source;
	private int nextIndex;

	public string Source
	{
		get { return source; }
	}

	public void Reset(Scanner[] scanners, string source)
	{
		this.scanners = scanners;
		this.source = source;
		nextIndex = 0;
	}

	public Token Next()
	{
		while (nextIndex < source.Length)
		{
			var tokenLength = 0;
			var tokenKind = TokenKind.Error;
			foreach (var scanner in scanners)
			{
				var length = scanner.Scan(source, nextIndex);
				if (tokenLength >= length)
					continue;

				tokenLength = length;
				tokenKind = scanner.tokenKind;
			}

			if (tokenKind == TokenKind.End)
			{
				nextIndex += tokenLength;
				continue;
			}

			if (tokenLength == 0)
				tokenLength = 1;

			var token = new Token(tokenKind, new Slice(nextIndex, tokenLength));
			nextIndex += tokenLength;
			return token;
		}

		return new Token(TokenKind.End, new Slice(source.Length, 0));
	}
}
