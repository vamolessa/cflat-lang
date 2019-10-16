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

		vm.valueStack.PushBack(new ValueData(function.functionIndex));
		vm.callframeStack.PushBack(new CallFrame(
			vm.chunk.bytes.count - 1,
			vm.valueStack.count,
			0,
			CallFrame.Type.EntryPoint
		));
		vm.callframeStack.PushBack(new CallFrame(
			function.codeIndex,
			vm.valueStack.count,
			function.functionIndex,
			CallFrame.Type.Function
		));

		var writer = new StackWriteMarshaler(vm, vm.valueStack.count);
		vm.valueStack.GrowUnchecked(function.parametersSize);
		arguments.Marshal(ref writer);

		VirtualMachineInstructions.RunTopFunction(vm);

		vm.valueStack.count -= Marshal.SizeOf<R>.size;
		var reader = new StackReadMarshaler(vm, vm.valueStack.count);
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

	internal sealed class ReflectionReturn : System.Exception
	{
		public readonly Marshal.ReflectionData reflectionData;

		public ReflectionReturn(Marshal.ReflectionData reflectionData) : base("", null)
		{
			this.reflectionData = reflectionData;
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
		builder.WithParam(Marshal.ReflectOn<T>(chunk).type);
		return default;
	}

	public NativeFunctionBody<T> Body<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable
	{
		builder.returnType = Marshal.ReflectOn<T>(chunk).type;
		throw new DefinitionReturn(functionName, builder);
	}

	public R CallFunction<A, R>(Function<A, R> function, ref A arguments)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		var marshaler = new FunctionDefinitionMarshaler<A, R>(chunk);
		marshaler.Returns();
		var reflection = marshaler.GetReflectionData();

		throw new ReflectionReturn(reflection);
	}
}

public static class ContextExtensions
{
	public static NativeFunctionBody<Unit> Body<C>(this C self, [CallerMemberName] string functionName = "") where C : IContext
	{
		return self.Body<Unit>(functionName);
	}
}