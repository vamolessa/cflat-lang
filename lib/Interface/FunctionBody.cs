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
		return vm.CallFunction(functionName);
	}
}

public static class FunctionBodyExtensions
{
	public static Return Return(this FunctionBody<Tuple> self)
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
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<T>.size > 0);

		var marshaler = new WriteMarshaler(self.vm, self.vm.valueStack.count);
		self.vm.valueStack.Grow(Marshal.SizeOf<T>.size);
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
