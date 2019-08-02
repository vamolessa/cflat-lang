using System.Diagnostics;

[DebuggerTypeProxy(typeof(ByteCodeChunkDebugView))]
public sealed class ByteCodeChunk
{
	public struct FunctionDefinitionBuilder
	{
		public int parameterCount;
		public ValueType returnType;
		internal ByteCodeChunk chunk;

		public void AddParam(ValueType type)
		{
			chunk.functionsParams.PushBack(type);
			parameterCount += 1;
		}
	}

	public Buffer<byte> bytes = new Buffer<byte>(256);
	public Buffer<Slice> slices = new Buffer<Slice>(256);
	public Buffer<ValueData> literalData = new Buffer<ValueData>(64);
	public Buffer<ValueType> literalTypes = new Buffer<ValueType>(64);
	public Buffer<string> stringLiterals = new Buffer<string>(16);
	public Buffer<FunctionDefinition> functions = new Buffer<FunctionDefinition>(8);
	public Buffer<ValueType> functionsParams = new Buffer<ValueType>(16);

	public void WriteByte(byte value, Slice slice)
	{
		bytes.PushBack(value);
		slices.PushBack(slice);
	}

	public int AddValueLiteral(ValueData value, ValueType type)
	{
		var index = FindValueIndex(value, type);
		if (index < 0)
		{
			index = literalData.count;
			literalData.PushBack(value);
			literalTypes.PushBack(type);
		}

		return index;
	}

	public int AddStringLiteral(string literal)
	{
		var stringIndex = System.Array.IndexOf(stringLiterals.buffer, literal);
		if (stringIndex < 0)
		{
			stringIndex = stringLiterals.count;
			stringLiterals.PushBack(literal);
		}

		return AddValueLiteral(new ValueData(stringIndex), ValueType.String);
	}

	public FunctionDefinitionBuilder BeginAddFunction()
	{
		functions.PushBack(new FunctionDefinition(
			"",
			bytes.count,
			new Slice(functionsParams.count, 0),
			ValueType.Unit
		));

		return new FunctionDefinitionBuilder
		{
			chunk = this,
			returnType = ValueType.Unit
		};
	}

	public void EndAddFunction(string name, FunctionDefinitionBuilder builder)
	{
		var function = functions.buffer[functions.count - 1];
		functions.buffer[functions.count - 1] = new FunctionDefinition(
			name,
			function.codeIndex,
			new Slice(function.parameters.index, builder.parameterCount),
			builder.returnType
		);
	}

	private int FindValueIndex(ValueData value, ValueType type)
	{
		for (var i = 0; i < literalData.count; i++)
		{
			if (type != literalTypes.buffer[i])
				continue;

			var v = literalData.buffer[i];
			switch (type)
			{
			case ValueType.Bool:
				if (v.asBool == value.asBool)
					return i;
				break;
			case ValueType.Int:
				if (v.asInt == value.asInt)
					return i;
				break;
			case ValueType.Float:
				if (v.asFloat == value.asFloat)
					return i;
				break;
			default: break;
			}
		}

		return -1;
	}
}