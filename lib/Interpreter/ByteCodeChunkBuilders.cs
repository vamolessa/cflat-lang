public struct FunctionTypeBuilder
{
	public ByteCodeChunk chunk;
	public ushort startParameterIndex;
	public ushort parameterCount;
	public ValueType returnType;

	public FunctionTypeBuilder(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.startParameterIndex = (ushort)chunk.functionParamTypes.count;
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

			startParameterIndex = (ushort)(chunk.functionParamTypes.count - parameterCount);
		}

		chunk.functionParamTypes.PushBack(type);
		parameterCount += 1;
	}

	public void Cancel()
	{
		chunk.functionParamTypes.count -= parameterCount;
	}

	public ushort Build()
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
				return (ushort)i;
			}
		}

		var parametersSize = 0;
		for (var i = 0; i < parameterCount; i++)
		{
			var param = chunk.functionParamTypes.buffer[parametersIndex + i];
			parametersSize += param.GetSize(chunk);
		}

		if (parametersSize > byte.MaxValue)
			parametersSize = byte.MaxValue;

		chunk.functionTypes.PushBack(new FunctionType(
			new Slice(
				parametersIndex,
				parameterCount
			),
			returnType,
			(byte)parametersSize
		));

		return (ushort)(chunk.functionTypes.count - 1);
	}
}

public struct TupleTypeBuilder
{
	public ByteCodeChunk chunk;
	public ushort startElementIndex;
	public ushort elementCount;

	public TupleTypeBuilder(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.startElementIndex = (ushort)chunk.structTypeFields.count;
		this.elementCount = 0;
	}

	public void WithElement(ValueType type)
	{
		var elementsIndex = startElementIndex + elementCount;
		if (elementsIndex < chunk.tupleElementTypes.count)
		{
			var swapCount = chunk.tupleElementTypes.count - startElementIndex;
			chunk.tupleElementTypes.buffer.SwapRanges(startElementIndex, elementsIndex, swapCount);

			for (var i = chunk.tupleTypes.count - 1; i >= 0; i--)
			{
				ref var tupleType = ref chunk.tupleTypes.buffer[i];
				var fieldsSlice = tupleType.elements;
				if (fieldsSlice.index < elementsIndex)
					break;

				tupleType = new TupleType(
					new Slice(
						fieldsSlice.index - elementCount,
						fieldsSlice.length
					),
					tupleType.size
				);
			}

			startElementIndex = (ushort)(chunk.tupleElementTypes.count - elementCount);
		}

		chunk.tupleElementTypes.PushBack(type);
		elementCount += 1;
	}

	public void Cancel()
	{
		chunk.tupleTypes.count -= elementCount;
	}

	public ushort Build()
	{
		var elementsIndex = chunk.tupleElementTypes.count - elementCount;

		var size = 0;
		for (var i = 0; i < elementCount; i++)
		{
			var element = chunk.tupleElementTypes.buffer[elementsIndex + i];
			size += element.GetSize(chunk);
		}

		if (size == 0)
			size = 1;
		else if (size >= byte.MaxValue)
			size = byte.MaxValue;

		chunk.tupleTypes.PushBack(new TupleType(
			new Slice(
				elementsIndex,
				elementCount
			),
			(byte)size
		));

		return (ushort)(chunk.tupleTypes.count - 1);
	}
}

public struct StructTypeBuilder
{
	public ByteCodeChunk chunk;
	public ushort startFieldIndex;
	public ushort fieldCount;

	public StructTypeBuilder(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.startFieldIndex = (ushort)chunk.structTypeFields.count;
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

			startFieldIndex = (ushort)(chunk.structTypeFields.count - fieldCount);
		}

		chunk.structTypeFields.PushBack(new StructTypeField(name, type));
		fieldCount += 1;
	}

	public void Cancel()
	{
		chunk.structTypes.count -= fieldCount;
	}

	public ushort Build(string name)
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
		else if (size >= byte.MaxValue)
			size = byte.MaxValue;

		chunk.structTypes.PushBack(new StructType(
			name,
			new Slice(
				fieldsIndex,
				fieldCount
			),
			(byte)size
		));

		return (ushort)(chunk.structTypes.count - 1);
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