using System.Diagnostics;

[DebuggerTypeProxy(typeof(ByteCodeChunkDebugView))]
public sealed class ByteCodeChunk
{
	public struct FunctionTypeBuilder
	{
		public ByteCodeChunk chunk;
		public int parameterCount;
		public ValueType returnType;

		public FunctionTypeBuilder(ByteCodeChunk chunk)
		{
			this.chunk = chunk;
			this.parameterCount = 0;
			this.returnType = ValueType.Unit;
		}

		public void AddParam(ValueType type)
		{
			chunk.functionTypeParams.PushBack(type);
			parameterCount += 1;
		}
	}

	public struct StructTypeBuilder
	{
		public ByteCodeChunk chunk;
		public int fieldCount;

		public StructTypeBuilder(ByteCodeChunk chunk)
		{
			this.chunk = chunk;
			this.fieldCount = 0;
		}

		public void AddField(string name, ValueType type)
		{
			chunk.structTypeFields.PushBack(new StructTypeField(name, type));
			fieldCount += 1;
		}
	}

	public Buffer<byte> bytes = new Buffer<byte>(256);
	public Buffer<Slice> slices = new Buffer<Slice>(256);
	public Buffer<ValueData> literalData = new Buffer<ValueData>(64);
	public Buffer<ValueType> literalTypes = new Buffer<ValueType>(64);
	public Buffer<string> stringLiterals = new Buffer<string>(16);
	public Buffer<FunctionType> functionTypes = new Buffer<FunctionType>(16);
	public Buffer<ValueType> functionTypeParams = new Buffer<ValueType>(16);
	public Buffer<Function> functions = new Buffer<Function>(8);
	public Buffer<StructType> structTypes = new Buffer<StructType>(8);
	public Buffer<StructTypeField> structTypeFields = new Buffer<StructTypeField>(32);

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

	public FunctionTypeBuilder BeginAddFunctionType()
	{
		return new FunctionTypeBuilder(this);
	}

	public int EndAddFunctionType(FunctionTypeBuilder builder)
	{
		var parametersIndex = functionTypeParams.count - builder.parameterCount;

		for (var i = 0; i < functionTypes.count; i++)
		{
			var function = functionTypes.buffer[i];
			if (function.returnType != builder.returnType || function.parameters.length != builder.parameterCount)
				continue;

			var match = true;
			for (var j = 0; j < builder.parameterCount; j++)
			{
				var a = functionTypeParams.buffer[function.parameters.index + j];
				var b = functionTypeParams.buffer[parametersIndex + j];
				if (a != b)
				{
					match = false;
					break;
				}
			}

			if (match)
			{
				functionTypeParams.count = parametersIndex;
				return i;
			}
		}

		functionTypes.PushBack(new FunctionType(
			new Slice(
				parametersIndex,
				builder.parameterCount
			),
			builder.returnType
		));

		return functionTypes.count - 1;
	}

	public void AddFunction(string name, int typeIndex)
	{
		functions.PushBack(new Function(name, bytes.count, typeIndex));
	}

	public StructTypeBuilder BeginAddStructType()
	{
		return new StructTypeBuilder(this);
	}

	public int EndAddStructType(StructTypeBuilder builder, string name)
	{
		var fieldsIndex = structTypeFields.count - builder.fieldCount;

		var size = 0;
		for (var i = 0; i < builder.fieldCount; i++)
		{
			var field = structTypeFields.buffer[fieldsIndex + 1];
			size += GetTypeSize(field.type);
		}

		if (size == 0)
			size = 1;

		structTypes.PushBack(new StructType(
			name,
			new Slice(
				fieldsIndex,
				builder.fieldCount
			),
			size
		));

		return structTypes.count - 1;
	}

	public int GetTypeSize(ValueType type)
	{
		var kind = ValueTypeHelper.GetKind(type);

		if (kind == ValueType.Struct)
		{
			var index = ValueTypeHelper.GetIndex(type);
			return structTypes.buffer[index].size;
		}
		else
		{
			return 1;
		}
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