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
	void Begin(Scanner[] scanners, string source);
	Token Next();
	T Convert<T>(Token token, System.Func<string, Token, T> converter);
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
		while (nextIndex < source.Length)
		{
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

			if (tokenKind == Token.EndKind)
			{
				nextIndex += tokenLength;
				continue;
			}

			if (tokenLength == 0)
				tokenLength = 1;

			var token = new Token(tokenKind, nextIndex, tokenLength);
			nextIndex += tokenLength;
			return token;
		}

		return new Token(Token.EndKind, source.Length, 0);
	}

	public T Convert<T>(Token token, System.Func<string, Token, T> converter)
	{
		return converter(source, token);
	}
}
