public sealed class Function<A, R>
	where A : struct, ITuple
	where R : struct, IMarshalable
{
	internal readonly int codeIndex;
	internal readonly ushort functionIndex;
	internal readonly byte parametersSize;

	internal Function(int codeIndex, ushort functionIndex, byte parametersSize)
	{
		this.codeIndex = codeIndex;
		this.functionIndex = functionIndex;
		this.parametersSize = parametersSize;
	}

	public R Call(CFlat cflat, A arguments)
	{
		return Call(cflat.vm, arguments);
	}

	public R Call(VirtualMachine vm, A arguments)
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<R>.size > 0);

		vm.memory.PushBackStack(new ValueData(functionIndex));
		vm.callFrameStack.PushBackUnchecked(new CallFrame(
			vm.chunk.bytes.count - 1,
			vm.memory.stackCount,
			0,
			CallFrame.Type.EntryPoint
		));
		vm.callFrameStack.PushBackUnchecked(new CallFrame(
			codeIndex,
			vm.memory.stackCount,
			functionIndex,
			CallFrame.Type.Function
		));

		var writer = new MemoryWriteMarshaler(vm, vm.memory.stackCount);
		vm.memory.GrowStack(parametersSize);
		arguments.Marshal(ref writer);

		VirtualMachineInstructions.RunTopFunction(vm);
		if (vm.error.isSome)
			return default;

		vm.memory.stackCount -= Marshal.SizeOf<R>.size;
		var reader = new MemoryReadMarshaler(vm, vm.memory.stackCount);
		var result = default(R);
		result.Marshal(ref reader);

		return result;
	}
}
