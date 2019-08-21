public struct FunctionCall
{
	private readonly VirtualMachine vm;
	private readonly ushort functionIndex;
	private readonly ushort paramEnd;
	private ushort paramIndex;

	public FunctionCall(VirtualMachine vm, int functionIndex)
	{
		this.vm = vm;
		this.functionIndex = (ushort)functionIndex;

		if (functionIndex < ushort.MaxValue)
		{
			var typeIndex = vm.chunk.functions.buffer[functionIndex].typeIndex;
			var paramsSlice = vm.chunk.functionTypes.buffer[typeIndex].parameters;
			paramIndex = paramsSlice.index;
			paramEnd = (ushort)(paramIndex + paramsSlice.length);
		}
		else
		{
			paramIndex = ushort.MaxValue;
			paramEnd = ushort.MaxValue;
		}
	}

	public FunctionCall WithBool(bool value)
	{
		PushArgument(new ValueData(value), new ValueType(TypeKind.Bool));
		return this;
	}

	public FunctionCall WithInt(int value)
	{
		PushArgument(new ValueData(value), new ValueType(TypeKind.Int));
		return this;
	}

	public FunctionCall WithFloat(float value)
	{
		PushArgument(new ValueData(value), new ValueType(TypeKind.Float));
		return this;
	}

	public FunctionCall WithString(string value)
	{
		PushArgument(new ValueData(vm.heap.count), new ValueType(TypeKind.String));
		vm.heap.PushBack(value);
		return this;
	}

	public FunctionCall WithStruct<T>(T value) where T : struct, IMarshalable
	{
		if (paramIndex < ushort.MaxValue)
		{
			if (paramIndex < paramEnd)
			{
				var type = Marshal.ReflectOn<T>(vm.chunk).type;
				var paramType = vm.chunk.functionParamTypes.buffer[paramIndex++];
				if (paramType.IsEqualTo(type))
				{
					var marshaler = new WriteMarshaler<T>(vm);
					value.Marshal(ref marshaler);
				}
				else
				{
					ArgumentTypeError(paramType, type);
				}
			}
			else
			{
				ArgumentCountError();
			}
		}

		return this;
	}

	public FunctionCall WithObject<T>(T value) where T : class
	{
		PushArgument(new ValueData(vm.heap.count), new ValueType(TypeKind.NativeObject));
		vm.heap.PushBack(value);
		return this;
	}

	private void PushArgument(ValueData value, ValueType type)
	{
		if (paramIndex < ushort.MaxValue)
		{
			if (paramIndex < paramEnd)
			{
				var paramType = vm.chunk.functionParamTypes.buffer[paramIndex++];
				if (paramType.IsEqualTo(type))
					vm.valueStack.PushBack(value);
				else
					ArgumentTypeError(paramType, type);
			}
			else
			{
				ArgumentCountError();
			}
		}
	}

	private void ArgumentTypeError(ValueType parameterType, ValueType argumentType)
	{
		var function = vm.chunk.functions.buffer[functionIndex];
		var paramsStartIndex = vm.chunk.functionTypes.buffer[function.typeIndex].parameters.index;

		vm.Error(string.Format(
			"Wrong type for function '{0}' argument {1}. Expected {2}. Got {3}",
			function.name,
			paramIndex - paramsStartIndex,
			parameterType.ToString(vm.chunk),
			argumentType.ToString(vm.chunk)
		));

		paramIndex = ushort.MaxValue;
	}

	private void ArgumentCountError()
	{
		var function = vm.chunk.functions.buffer[functionIndex];
		var parameters = vm.chunk.functionTypes.buffer[function.typeIndex].parameters;
		var argCount = paramIndex - parameters.index;

		vm.Error(string.Format(
			"Wrong number of arguments for function '{0}'. Expected {1}. Got {2}",
			function.name,
			parameters.length,
			argCount
		));

		paramIndex = ushort.MaxValue;
	}

	public Option<bool> GetBool()
	{
		if (paramIndex < ushort.MaxValue)
		{
			var typeIndex = vm.chunk.functions.buffer[functionIndex].typeIndex;
			var type = vm.chunk.functionTypes.buffer[typeIndex];
			var argCount = paramIndex - type.parameters.index;
			if (argCount == type.parameters.length)
			{
				var returnType = type.returnType;
				var valueType = new ValueType(TypeKind.Bool);
				if (returnType.IsEqualTo(valueType))
				{

				}
				else
				{
					ReturnError(returnType, valueType);
				}
			}
			else
			{
				ArgumentCountError();
			}
		}
	}

	private void ReturnError(ValueType returnType, ValueType type)
	{
		var function = vm.chunk.functions.buffer[functionIndex];

		vm.Error(string.Format(
			"Wrong return type for function '{0}'. Expected {1}. Got {2}",
			function.name,
			returnType,
			type
		));
	}
}