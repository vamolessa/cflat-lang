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
			this.returnType = new ValueType(TypeKind.Unit);
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
	public Buffer<TypeKind> literalKinds = new Buffer<TypeKind>(64);
	public Buffer<string> stringLiterals = new Buffer<string>(16);
	public Buffer<FunctionType> functionTypes = new Buffer<FunctionType>(16);
	public Buffer<ValueType> functionTypeParams = new Buffer<ValueType>(16);
	public Buffer<Function> functions = new Buffer<Function>(8);
	public Buffer<NativeFunction> nativeFunctions = new Buffer<NativeFunction>(8);
	public Buffer<StructType> structTypes = new Buffer<StructType>(8);
	public Buffer<StructTypeField> structTypeFields = new Buffer<StructTypeField>(32);

	public void WriteByte(byte value, Slice slice)
	{
		bytes.PushBack(value);
		slices.PushBack(slice);
	}

	public int AddValueLiteral(ValueData value, TypeKind kind)
	{
		var index = FindValueIndex(value, kind);
		if (index < 0)
		{
			index = literalData.count;
			literalData.PushBack(value);
			literalKinds.PushBack(kind);
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

		return AddValueLiteral(new ValueData(stringIndex), TypeKind.String);
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
			if (!function.returnType.IsEqualTo(builder.returnType) || function.parameters.length != builder.parameterCount)
				continue;

			var match = true;
			for (var j = 0; j < builder.parameterCount; j++)
			{
				var a = functionTypeParams.buffer[function.parameters.index + j];
				var b = functionTypeParams.buffer[parametersIndex + j];
				if (!a.IsEqualTo(b))
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

		var parametersTotalSize = 0;
		for (var i = 0; i < builder.parameterCount; i++)
		{
			var param = functionTypeParams.buffer[parametersIndex + i];
			parametersTotalSize += param.GetSize(this);
		}

		functionTypes.PushBack(new FunctionType(
			new Slice(
				parametersIndex,
				builder.parameterCount
			),
			builder.returnType,
			parametersTotalSize
		));

		return functionTypes.count - 1;
	}

	public void AddFunction(string name, int typeIndex)
	{
		functions.PushBack(new Function(name, typeIndex, bytes.count));
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
			var field = structTypeFields.buffer[fieldsIndex + i];
			size += field.type.GetSize(this);
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

	private int FindValueIndex(ValueData value, TypeKind kind)
	{
		for (var i = 0; i < literalData.count; i++)
		{
			if (kind != literalKinds.buffer[i])
				continue;

			var v = literalData.buffer[i];
			switch (kind)
			{
			case TypeKind.Bool:
				if (v.asBool == value.asBool)
					return i;
				break;
			case TypeKind.Int:
				if (v.asInt == value.asInt)
					return i;
				break;
			case TypeKind.Float:
				if (v.asFloat == value.asFloat)
					return i;
				break;
			default: break;
			}
		}

		return -1;
	}
}