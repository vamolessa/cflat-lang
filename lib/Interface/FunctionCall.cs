public struct FunctionCall
{
	private readonly VirtualMachine vm;
	private readonly ushort functionIndex;
	private readonly ushort paramEnd;
	private ushort paramIndex;

	public FunctionCall(VirtualMachine vm, ushort functionIndex)
	{
		this.vm = vm;
		this.functionIndex = functionIndex;

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
		vm.valueStack.PushBack(new ValueData(value));
		CheckArgument(new ValueType(TypeKind.Bool));
		return this;
	}

	public FunctionCall WithInt(int value)
	{
		vm.valueStack.PushBack(new ValueData(value));
		CheckArgument(new ValueType(TypeKind.Int));
		return this;
	}

	public FunctionCall WithFloat(float value)
	{
		vm.valueStack.PushBack(new ValueData(value));
		CheckArgument(new ValueType(TypeKind.Float));
		return this;
	}

	public FunctionCall WithString(string value)
	{
		vm.valueStack.PushBack(new ValueData(vm.heap.count));
		vm.heap.PushBack(value);
		CheckArgument(new ValueType(TypeKind.String));
		return this;
	}

	public FunctionCall WithStruct<T>(T value) where T : struct, IMarshalable
	{
		var marshaler = WriteMarshaler.For<T>(vm);
		value.Marshal(ref marshaler);
		CheckArgument(Marshal.ReflectOn<T>(vm.chunk).type);
		return this;
	}

	public FunctionCall WithObject<T>(T value) where T : class
	{
		vm.valueStack.PushBack(new ValueData(vm.heap.count));
		vm.heap.PushBack(value);
		CheckArgument(new ValueType(TypeKind.NativeObject));
		return this;
	}

	private void CheckArgument(ValueType argumentType)
	{
		if (paramIndex == ushort.MaxValue)
			return;

		if (paramIndex >= paramEnd)
		{
			ArgumentCountError();
			return;
		}

		var parameterType = vm.chunk.functionParamTypes.buffer[paramIndex++];
		if (!parameterType.IsEqualTo(argumentType))
		{
			vm.callframeStack.count -= 1;
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
	}

	private void ArgumentCountError()
	{
		vm.callframeStack.count -= 1;
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

	public bool Get()
	{
		return CallAndCheckReturn(new ValueType(TypeKind.Unit));
	}

	public bool GetBool(out bool value)
	{
		if (CallAndCheckReturn(new ValueType(TypeKind.Bool)))
		{
			value = vm.valueStack.PopLast().asBool;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}

	public bool GetInt(out int value)
	{
		if (CallAndCheckReturn(new ValueType(TypeKind.Int)))
		{
			value = vm.valueStack.PopLast().asInt;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}

	public bool GetFloat(out float value)
	{
		if (CallAndCheckReturn(new ValueType(TypeKind.Float)))
		{
			value = vm.valueStack.PopLast().asFloat;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}

	public bool GetString(out string value)
	{
		if (CallAndCheckReturn(new ValueType(TypeKind.String)))
		{
			value = vm.heap.buffer[vm.valueStack.PopLast().asInt] as string;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}

	public bool GetStruct<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		if (CallAndCheckReturn(Marshal.ReflectOn<T>(vm.chunk).type))
		{
			var marshaler = ReadMarshaler.For<T>(vm);
			value.Marshal(ref marshaler);
			return true;
		}
		else
		{
			return false;
		}
	}

	public bool GetObject<T>(out T value) where T : class
	{
		if (CallAndCheckReturn(new ValueType(TypeKind.NativeObject)))
		{
			value = vm.heap.buffer[vm.valueStack.PopLast().asInt] as T;
			return true;
		}
		else
		{
			value = default;
			return false;
		}
	}


	private bool CallAndCheckReturn(ValueType valueType)
	{
		if (paramIndex == ushort.MaxValue)
			return false;

		if (paramIndex != paramEnd)
		{
			ArgumentCountError();
			return false;
		}

		var typeIndex = vm.chunk.functions.buffer[functionIndex].typeIndex;
		var returnType = vm.chunk.functionTypes.buffer[typeIndex].returnType;
		if (!returnType.IsEqualTo(valueType))
		{
			vm.callframeStack.count -= 1;
			var function = vm.chunk.functions.buffer[functionIndex];
			vm.Error(string.Format(
				"Return type does not match for function '{0}'. Expected {1}. Tried {2}",
				function.name,
				returnType.ToString(vm.chunk),
				valueType.ToString(vm.chunk)
			));
			return false;
		}

		vm.CallTopFunction();
		return !vm.error.isSome;
	}
}