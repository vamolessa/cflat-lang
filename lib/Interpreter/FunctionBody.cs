public readonly struct FunctionBody<T>
{
	internal readonly VirtualMachine vm;

	public FunctionBody(VirtualMachine vm)
	{
		this.vm = vm;
	}
}

public static class FunctionBodyExtensions
{
	public static void Return(this FunctionBody<Unit> self)
	{
		self.vm.valueStack.PushBack(new ValueData());
	}

	public static void Return(this FunctionBody<bool> self, bool value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
	}

	public static void Return(this FunctionBody<int> self, int value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
	}

	public static void Return(this FunctionBody<float> self, float value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
	}

	public static void Return(this FunctionBody<string> self, string value)
	{
		self.vm.valueStack.PushBack(new ValueData(self.vm.heap.count));
		self.vm.heap.PushBack(value);
	}

	public static void Return<T>(this FunctionBody<T> self, T value) where T : struct, IMarshalable
	{
		var stackIndex = self.vm.valueStack.count;
		self.vm.valueStack.Grow(value.Size);
		var marshal = new WriterMarshaler(self.vm, stackIndex);
		value.Marshal(ref marshal);
	}
}
