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

	public void WithParam(ValueType type)
	{
		chunk.functionTypeParams.PushBack(type);
		parameterCount += 1;
	}

	public int Build()
	{
		var parametersIndex = chunk.functionTypeParams.count - parameterCount;

		for (var i = 0; i < chunk.functionTypes.count; i++)
		{
			var function = chunk.functionTypes.buffer[i];
			if (!function.returnType.IsEqualTo(returnType) || function.parameters.length != parameterCount)
				continue;

			var match = true;
			for (var j = 0; j < parameterCount; j++)
			{
				var a = chunk.functionTypeParams.buffer[function.parameters.index + j];
				var b = chunk.functionTypeParams.buffer[parametersIndex + j];
				if (!a.IsEqualTo(b))
				{
					match = false;
					break;
				}
			}

			if (match)
			{
				chunk.functionTypeParams.count = parametersIndex;
				return i;
			}
		}

		var parametersTotalSize = 0;
		for (var i = 0; i < parameterCount; i++)
		{
			var param = chunk.functionTypeParams.buffer[parametersIndex + i];
			parametersTotalSize += param.GetSize(chunk);
		}

		chunk.functionTypes.PushBack(new FunctionType(
			new Slice(
				parametersIndex,
				parameterCount
			),
			returnType,
			parametersTotalSize
		));

		return chunk.functionTypes.count - 1;
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

	public void WithField(string name, ValueType type)
	{
		chunk.structTypeFields.PushBack(new StructTypeField(name, type));
		fieldCount += 1;
	}

	public int Build(string name)
	{
		var fieldsIndex = chunk.structTypeFields.count - fieldCount;

		var size = 0;
		for (var i = 0; i < fieldCount; i++)
		{
			var field = chunk.structTypeFields.buffer[fieldsIndex + i];
			size += field.type.GetSize(chunk);
		}

		if (size == 0)
			size = 1;

		chunk.structTypes.PushBack(new StructType(
			name,
			new Slice(
				fieldsIndex,
				fieldCount
			),
			size
		));

		return chunk.structTypes.count - 1;
	}

	public int BuildAnonymous()
	{
		var fieldsIndex = chunk.structTypeFields.count - fieldCount;

		for (var i = 0; i < chunk.structTypes.count; i++)
		{
			var other = chunk.structTypes.buffer[i];
			if (
				other.fields.length != fieldCount ||
				!string.IsNullOrEmpty(other.name)
			)
				continue;

			var matched = true;
			for (var j = 0; j < fieldCount; j++)
			{
				var thisField = chunk.structTypeFields.buffer[fieldsIndex + j];
				var otherField = chunk.structTypeFields.buffer[other.fields.index + j];
				if (
					!thisField.type.IsEqualTo(otherField.type) ||
					thisField.name != otherField.name
				)
				{
					matched = false;
					break;
				}
			}
			if (!matched)
				continue;

			chunk.structTypeFields.count = fieldsIndex;
			return i;
		}

		return Build("");
	}
}