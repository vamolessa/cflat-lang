public struct NewFunctionCall<A, R>
	where A : ITuple
	where R : IMarshalable
{
	private readonly int codeIndex;
	private readonly ushort functionIndex;

	public NewFunctionCall(int codeIndex, ushort functionIndex)
	{
		this.codeIndex = codeIndex;
		this.functionIndex = functionIndex;
	}

	public R Call<M>(ref M marshaler, VirtualMachine vm, A arguments) where M : IMarshaler
	{
		vm.valueStack.PushBack(new ValueData(functionIndex));
		vm.callframeStack.PushBack(new CallFrame(
			-1,
			vm.chunk.bytes.count - 1,
			vm.valueStack.count
		));
		vm.callframeStack.PushBack(new CallFrame(
			functionIndex,
			codeIndex,
			vm.valueStack.count
		));

		arguments.Marshal(ref marshaler);
		vm.CallTopFunction();

		var result = default(R);
		result.Marshal(ref marshaler);
		
		return result;
	}
}