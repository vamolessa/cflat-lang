public readonly struct Slice
{
	public readonly int index;
	public readonly int length;

	public Slice(int index, int length)
	{
		this.index = index;
		this.length = length;
	}
}

public readonly struct Token
{
	public static readonly int EndKind = -1;
	public static readonly int ErrorKind = -2;

	public readonly int kind;
	public readonly Slice slice;

	public Token(int kind, Slice slice)
	{
		this.kind = kind;
		this.slice = slice;
	}

	public bool IsValid()
	{
		return kind >= 0;
	}
}

public interface ITokenizer
{
	string Source { get; }
	void Begin(Scanner[] scanners, string source);
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

			var token = new Token(tokenKind, new Slice(nextIndex, tokenLength));
			nextIndex += tokenLength;
			return token;
		}

		return new Token(Token.EndKind, new Slice(source.Length, 0));
	}
}
