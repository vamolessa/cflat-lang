public readonly struct Return { }

public readonly struct NativeFunctionBody<T> where T : struct, IMarshalable
{
	internal readonly VirtualMachine vm;

	public NativeFunctionBody(VirtualMachine vm)
	{
		this.vm = vm;
	}

	public Return Return(T value)
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<T>.size > 0);

		var marshaler = new MemoryWriteMarshaler(vm, vm.memory.stackCount);
		vm.memory.GrowStack(Marshal.SizeOf<T>.size);
		value.Marshal(ref marshaler);
		return default;
	}
}

public static class NativeFunctionBodyExtensions
{
	public static Return Return(this NativeFunctionBody<Unit> self)
	{
		return self.Return(new Unit());
	}
}