public sealed class ByteCodeChunk
{
	public Buffer<byte> bytes = new Buffer<byte>(256);
	public Buffer<Token> tokens = new Buffer<Token>(256);
	public Buffer<ValueData> literalData = new Buffer<ValueData>(64);
	public Buffer<ValueType> literalTypes = new Buffer<ValueType>(64);
	public Buffer<string> stringLiterals = new Buffer<string>(16);

	public int AddValueLiteral(ValueData data, ValueType type)
	{
		var index = literalData.count;
		literalData.PushBack(data);
		literalTypes.PushBack(type);
		return index;
	}

	public int AddStringLiteral(string literal)
	{
		var stringIndex = stringLiterals.count;
		stringLiterals.PushBack(literal);
		return AddValueLiteral(new ValueData(stringIndex), ValueType.String);
	}

	public void WriteByte(byte value, Token token)
	{
		bytes.PushBack(value);
		tokens.PushBack(token);
	}
}