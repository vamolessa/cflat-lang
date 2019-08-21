public readonly struct Return { }

public readonly struct FunctionBody<T>
{
	internal readonly VirtualMachine vm;

	public FunctionBody(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public FunctionCall Call(string functionName)
	{
		for (var i = 0; i < vm.chunk.functions.count; i++)
		{
			var function = vm.chunk.functions.buffer[i];
			if (function.name == functionName)
			{
				vm.PushFunction(i);
				return new FunctionCall(vm, i);
			}
		}

		vm.Error(string.Format("Could not find function named '{0}'", functionName));
		return new FunctionCall(vm, ushort.MaxValue);
	}
}

public static class FunctionBodyExtensions
{
	public static Return Return(this FunctionBody<Unit> self)
	{
		self.vm.valueStack.PushBack(new ValueData());
		return default;
	}

	public static Return Return(this FunctionBody<bool> self, bool value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
		return default;
	}

	public static Return Return(this FunctionBody<int> self, int value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
		return default;
	}

	public static Return Return(this FunctionBody<float> self, float value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
		return default;
	}

	public static Return Return(this FunctionBody<string> self, string value)
	{
		self.vm.valueStack.PushBack(new ValueData(self.vm.heap.count));
		self.vm.heap.PushBack(value);
		return default;
	}

	public static Return Return<T>(this FunctionBody<T> self, T value) where T : struct, IMarshalable
	{
		var marshaler = WriteMarshaler.For<T>(self.vm);
		value.Marshal(ref marshaler);
		return default;
	}

	public static Return Return(this FunctionBody<object> self, object value)
	{
		self.vm.valueStack.PushBack(new ValueData(self.vm.heap.count));
		self.vm.heap.PushBack(value);
		return default;
	}
}
