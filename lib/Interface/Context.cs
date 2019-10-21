using System.Runtime.CompilerServices;

public interface IContext
{
	T Arg<T>() where T : struct, IMarshalable;

	NativeFunctionBody<T> Body<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable;

	R CallFunction<A, R>(Function<A, R> function, ref A arguments)
		where A : struct, ITuple
		where R : struct, IMarshalable;
}

public struct RuntimeContext : IContext
{
	private VirtualMachine vm;
	private StackReadMarshaler marshaler;

	public RuntimeContext(VirtualMachine vm, int argStackIndex)
	{
		this.vm = vm;
		this.marshaler = new StackReadMarshaler(vm, argStackIndex);
	}

	public T Arg<T>() where T : struct, IMarshalable
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<T>.size > 0);

		var value = default(T);
		value.Marshal(ref marshaler);
		return value;
	}

	public NativeFunctionBody<T> Body<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable
	{
		return new NativeFunctionBody<T>(vm);
	}

	public R CallFunction<A, R>(Function<A, R> function, ref A arguments)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<R>.size > 0);

		vm.memory.PushBackStack(new ValueData(function.functionIndex));
		vm.callframeStack.PushBackUnchecked(new CallFrame(
			vm.chunk.bytes.count - 1,
			vm.memory.stackCount,
			0,
			CallFrame.Type.EntryPoint
		));
		vm.callframeStack.PushBackUnchecked(new CallFrame(
			function.codeIndex,
			vm.memory.stackCount,
			function.functionIndex,
			CallFrame.Type.Function
		));

		var writer = new StackWriteMarshaler(vm, vm.memory.stackCount);
		vm.memory.GrowStack(function.parametersSize);
		arguments.Marshal(ref writer);

		VirtualMachineInstructions.RunTopFunction(vm);

		vm.memory.stackCount -= Marshal.SizeOf<R>.size;
		var reader = new StackReadMarshaler(vm, vm.memory.stackCount);
		var result = default(R);
		result.Marshal(ref reader);

		return result;
	}
}

public struct DefinitionContext : IContext
{
	internal sealed class DefinitionReturn : System.Exception
	{
		public readonly string functionName;
		public FunctionTypeBuilder functionTypeBuilder;

		public DefinitionReturn(string functionName, FunctionTypeBuilder functionTypeBuilder) : base("", null)
		{
			this.functionName = functionName;
			this.functionTypeBuilder = functionTypeBuilder;
		}
	}

	internal sealed class TypeReturn : System.Exception
	{
		public readonly ValueType type;

		public TypeReturn(ValueType type) : base("", null)
		{
			this.type = type;
		}
	}

	internal ByteCodeChunk chunk;
	internal FunctionTypeBuilder builder;

	public DefinitionContext(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginFunctionType();
	}

	public T Arg<T>() where T : struct, IMarshalable
	{
		builder.WithParam(Marshal.TypeOf<T>(chunk));
		return default;
	}

	public NativeFunctionBody<T> Body<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable
	{
		builder.returnType = Marshal.TypeOf<T>(chunk);
		throw new DefinitionReturn(functionName, builder);
	}

	public R CallFunction<A, R>(Function<A, R> function, ref A arguments)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		var marshaler = new FunctionDefinitionMarshaler<A, R>(chunk);
		marshaler.Returns();
		throw new TypeReturn(marshaler.GetDefinedType());
	}
}

public static class ContextExtensions
{
	public static NativeFunctionBody<Unit> Body<C>(this C self, [CallerMemberName] string functionName = "") where C : IContext
	{
		return self.Body<Unit>(functionName);
	}
}