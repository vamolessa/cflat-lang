public readonly struct Return { }

public readonly struct NativeFunctionBody<T>
{
	internal readonly VirtualMachine vm;

	public NativeFunctionBody(VirtualMachine vm)
	{
		this.vm = vm;
	}
}

public static class NativeFunctionBodyExtensions
{
	public static Return Return(this NativeFunctionBody<Unit> self)
	{
		self.vm.valueStack.PushBack(new ValueData());
		return default;
	}

	public static Return Return(this NativeFunctionBody<bool> self, bool value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
		return default;
	}

	public static Return Return(this NativeFunctionBody<int> self, int value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
		return default;
	}

	public static Return Return(this NativeFunctionBody<float> self, float value)
	{
		self.vm.valueStack.PushBack(new ValueData(value));
		return default;
	}

	public static Return Return(this NativeFunctionBody<string> self, string value)
	{
		self.vm.valueStack.PushBack(new ValueData(self.vm.nativeObjects.count));
		self.vm.nativeObjects.PushBack(value);
		return default;
	}

	public static Return Return<T>(this NativeFunctionBody<T> self, T value) where T : struct, IMarshalable
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<T>.size > 0);

		var marshaler = new StackWriteMarshaler(self.vm, self.vm.valueStack.count);
		self.vm.valueStack.GrowUnchecked(Marshal.SizeOf<T>.size);
		value.Marshal(ref marshaler);
		return default;
	}

	public static Return Return(this NativeFunctionBody<object> self, object value)
	{
		self.vm.valueStack.PushBack(new ValueData(self.vm.nativeObjects.count));
		self.vm.nativeObjects.PushBack(value);
		return default;
	}
}
