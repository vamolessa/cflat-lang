public struct FunctionTypeBuilder
{
	public ByteCodeChunk chunk;
	public int startParameterIndex;
	public int parameterCount;
	public ValueType returnType;

	public FunctionTypeBuilder(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.startParameterIndex = chunk.functionParamTypes.count;
		this.parameterCount = 0;
		this.returnType = new ValueType(TypeKind.Unit);
	}

	public void WithParam(ValueType type)
	{
		var paramIndex = startParameterIndex + parameterCount;
		if (paramIndex < chunk.functionParamTypes.count)
		{
			var swapCount = chunk.functionParamTypes.count - startParameterIndex;
			chunk.functionParamTypes.buffer.SwapRanges(startParameterIndex, paramIndex, swapCount);

			for (var i = chunk.functionTypes.count - 1; i >= 0; i--)
			{
				ref var functionType = ref chunk.functionTypes.buffer[i];
				var parametersSlice = functionType.parameters;
				if (parametersSlice.index < paramIndex)
					break;

				functionType = new FunctionType(
					new Slice(
						parametersSlice.index - parameterCount,
						parametersSlice.length
					),
					functionType.returnType,
					functionType.parametersSize
				);
			}

			startParameterIndex = chunk.functionParamTypes.count - parameterCount;
		}

		chunk.functionParamTypes.PushBack(type);
		parameterCount += 1;
	}

	public void Cancel()
	{
		chunk.functionParamTypes.count -= parameterCount;
	}

	public int Build()
	{
		var parametersIndex = chunk.functionParamTypes.count - parameterCount;

		for (var i = 0; i < chunk.functionTypes.count; i++)
		{
			var function = chunk.functionTypes.buffer[i];
			if (!function.returnType.IsEqualTo(returnType) || function.parameters.length != parameterCount)
				continue;

			var match = true;
			for (var j = 0; j < parameterCount; j++)
			{
				var a = chunk.functionParamTypes.buffer[function.parameters.index + j];
				var b = chunk.functionParamTypes.buffer[parametersIndex + j];
				if (!a.IsEqualTo(b))
				{
					match = false;
					break;
				}
			}

			if (match)
			{
				chunk.functionParamTypes.count = parametersIndex;
				return i;
			}
		}

		var parametersTotalSize = 0;
		for (var i = 0; i < parameterCount; i++)
		{
			var param = chunk.functionParamTypes.buffer[parametersIndex + i];
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
	public int startFieldIndex;
	public int fieldCount;

	public StructTypeBuilder(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.startFieldIndex = chunk.structTypeFields.count;
		this.fieldCount = 0;
	}

	public void WithField(string name, ValueType type)
	{
		var fieldIndex = startFieldIndex + fieldCount;
		if (fieldIndex < chunk.structTypeFields.count)
		{
			var swapCount = chunk.structTypeFields.count - startFieldIndex;
			chunk.structTypeFields.buffer.SwapRanges(startFieldIndex, fieldIndex, swapCount);

			for (var i = chunk.structTypes.count - 1; i >= 0; i--)
			{
				ref var structType = ref chunk.structTypes.buffer[i];
				var fieldsSlice = structType.fields;
				if (fieldsSlice.index < fieldIndex)
					break;

				structType = new StructType(
					structType.name,
					new Slice(
						fieldsSlice.index - fieldCount,
						fieldsSlice.length
					),
					structType.size
				);
			}

			startFieldIndex = chunk.structTypeFields.count - fieldCount;
		}

		chunk.structTypeFields.PushBack(new StructTypeField(name, type));
		fieldCount += 1;
	}

	public void Cancel()
	{
		chunk.structTypes.count -= fieldCount;
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