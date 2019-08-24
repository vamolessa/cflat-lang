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

	public R Call<C>(ref C context, A arguments) where C : IContext
	{
		return context.CallFunction<A, R>(this, ref arguments);
	}

	public R Call(Pepper pepper, A arguments)
	{
		var context = new RuntimeContext(
			pepper.virtualMachine,
			pepper.virtualMachine.valueStack.count - parametersSize
		);

		return context.CallFunction<A, R>(this, ref arguments);
	}
}
