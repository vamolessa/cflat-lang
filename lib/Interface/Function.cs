public sealed class Function<A, R>
	where A : struct, ITuple
	where R : struct, IMarshalable
{
	internal readonly int codeIndex;
	internal readonly ushort functionIndex;
	internal readonly byte chunkIndex;
	internal readonly byte parametersSize;

	internal Function(byte chunkIndex, int codeIndex, ushort functionIndex, byte parametersSize)
	{
		this.chunkIndex = chunkIndex;
		this.codeIndex = codeIndex;
		this.functionIndex = functionIndex;
		this.parametersSize = parametersSize;
	}

	public R Call<C>(ref C context, A arguments) where C : IContext
	{
		return context.CallFunction<A, R>(this, ref arguments);
	}

	public R Call(CFlat cflat, A arguments)
	{
		var context = new RuntimeContext(
			cflat.virtualMachine,
			cflat.virtualMachine.valueStack.count - parametersSize
		);

		return context.CallFunction<A, R>(this, ref arguments);
	}
}
