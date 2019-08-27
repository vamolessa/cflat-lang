using System.Runtime.CompilerServices;

public interface IContext
{
	bool ArgBool();
	int ArgInt();
	float ArgFloat();
	string ArgString();
	T ArgTuple<T>() where T : struct, ITuple;
	T ArgStruct<T>() where T : struct, IStruct;
	T ArgObject<T>() where T : class;

	FunctionBody<Unit> Body([CallerMemberName] string functionName = "");
	FunctionBody<bool> BodyOfBool([CallerMemberName] string functionName = "");
	FunctionBody<int> BodyOfInt([CallerMemberName] string functionName = "");
	FunctionBody<float> BodyOfFloat([CallerMemberName] string functionName = "");
	FunctionBody<string> BodyOfString([CallerMemberName] string functionName = "");
	FunctionBody<T> BodyOfTuple<T>([CallerMemberName] string functionName = "") where T : struct, ITuple;
	FunctionBody<T> BodyOfStruct<T>([CallerMemberName] string functionName = "") where T : struct, IStruct;
	FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class;

	R CallFunction<A, R>(Function<A, R> function, ref A arguments)
		where A : struct, ITuple
		where R : struct, IMarshalable;
}

public struct RuntimeContext : IContext
{
	private VirtualMachine vm;
	private int argStackIndex;

	public RuntimeContext(VirtualMachine vm, int argStackIndex)
	{
		this.vm = vm;
		this.argStackIndex = argStackIndex;
	}

	public bool ArgBool() => vm.valueStack.buffer[argStackIndex++].asBool;
	public int ArgInt() => vm.valueStack.buffer[argStackIndex++].asInt;
	public float ArgFloat() => vm.valueStack.buffer[argStackIndex++].asFloat;
	public string ArgString() => vm.nativeObjects.buffer[vm.valueStack.buffer[argStackIndex++].asInt] as string;
	public T ArgTuple<T>() where T : struct, ITuple
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<T>.size > 0);

		var value = default(T);
		var marshaler = new ReadMarshaler(vm, argStackIndex);
		argStackIndex += Marshal.SizeOf<T>.size;
		value.Marshal(ref marshaler);
		return value;
	}
	public T ArgStruct<T>() where T : struct, IStruct
	{
		System.Diagnostics.Debug.Assert(Marshal.SizeOf<T>.size > 0);

		var value = default(T);
		var marshaler = new ReadMarshaler(vm, argStackIndex);
		argStackIndex += Marshal.SizeOf<T>.size;
		value.Marshal(ref marshaler);
		return value;
	}
	public T ArgObject<T>() where T : class => vm.nativeObjects.buffer[vm.valueStack.buffer[argStackIndex++].asInt] as T;

	public FunctionBody<Unit> Body([CallerMemberName] string functionName = "") => new FunctionBody<Unit>(vm);
	public FunctionBody<bool> BodyOfBool([CallerMemberName] string functionName = "") => new FunctionBody<bool>(vm);
	public FunctionBody<int> BodyOfInt([CallerMemberName] string functionName = "") => new FunctionBody<int>(vm);
	public FunctionBody<float> BodyOfFloat([CallerMemberName] string functionName = "") => new FunctionBody<float>(vm);
	public FunctionBody<string> BodyOfString([CallerMemberName] string functionName = "") => new FunctionBody<string>(vm);
	public FunctionBody<T> BodyOfTuple<T>([CallerMemberName] string functionName = "") where T : struct, ITuple => new FunctionBody<T>(vm);
	public FunctionBody<T> BodyOfStruct<T>([CallerMemberName] string functionName = "") where T : struct, IStruct => new FunctionBody<T>(vm);
	public FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class => new FunctionBody<object>(vm);

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

		var writer = new WriteMarshaler(vm, vm.valueStack.count);
		vm.valueStack.GrowUnchecked(function.parametersSize);
		arguments.Marshal(ref writer);

		VirtualMachineInstructions.RunTopFunction(vm);

		vm.valueStack.count -= Marshal.SizeOf<R>.size;
		var reader = new ReadMarshaler(vm, vm.valueStack.count);
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

	public bool ArgBool()
	{
		builder.WithParam(new ValueType(TypeKind.Bool));
		return default;
	}
	public int ArgInt()
	{
		builder.WithParam(new ValueType(TypeKind.Int));
		return default;
	}
	public float ArgFloat()
	{
		builder.WithParam(new ValueType(TypeKind.Float));
		return default;
	}
	public string ArgString()
	{
		builder.WithParam(new ValueType(TypeKind.String));
		return default;
	}
	public T ArgTuple<T>() where T : struct, ITuple
	{
		builder.WithParam(Marshal.ReflectOnTuple<T>(chunk).type);
		return default;
	}
	public T ArgStruct<T>() where T : struct, IStruct
	{
		builder.WithParam(Marshal.ReflectOnStruct<T>(chunk).type);
		return default;
	}
	public T ArgObject<T>() where T : class
	{
		builder.WithParam(new ValueType(TypeKind.NativeObject));
		return default;
	}

	public FunctionBody<Unit> Body([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Unit);
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<bool> BodyOfBool([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Bool);
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<int> BodyOfInt([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Int);
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<float> BodyOfFloat([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Float);
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<string> BodyOfString([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.String);
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<T> BodyOfTuple<T>([CallerMemberName] string functionName = "") where T : struct, ITuple
	{
		builder.returnType = Marshal.ReflectOnTuple<T>(chunk).type;
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<T> BodyOfStruct<T>([CallerMemberName] string functionName = "") where T : struct, IStruct
	{
		builder.returnType = Marshal.ReflectOnStruct<T>(chunk).type;
		throw new DefinitionReturn(functionName, builder);
	}
	public FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class
	{
		builder.returnType = new ValueType(TypeKind.NativeObject);
		throw new DefinitionReturn(functionName, builder);
	}

	public R CallFunction<A, R>(Function<A, R> function, ref A arguments)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		var marshaler = new FunctionDefinitionMarshaler(chunk);
		marshaler.Returns<R>();
		var reflection = marshaler.GetReflectionData<A>();

		throw new ReflectionReturn(reflection);
	}
}
