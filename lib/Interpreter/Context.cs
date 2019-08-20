using System.Runtime.CompilerServices;

public interface IContext
{
	void Arg(out bool value);
	void Arg(out int value);
	void Arg(out float value);
	void Arg(out string value);
	void Arg<T>(out T value) where T : struct, IMarshalable;
	void Arg(out object value);

	FunctionBody<Unit> Body([CallerMemberName] string functionName = "");
	FunctionBody<bool> BodyOfBool([CallerMemberName] string functionName = "");
	FunctionBody<int> BodyOfInt([CallerMemberName] string functionName = "");
	FunctionBody<float> BodyOfFloat([CallerMemberName] string functionName = "");
	FunctionBody<string> BodyOfString([CallerMemberName] string functionName = "");
	FunctionBody<T> BodyOfStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable;
	FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class;
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

	public void Arg(out bool value) => value = vm.valueStack.buffer[argStackIndex++].asBool;
	public void Arg(out int value) => value = vm.valueStack.buffer[argStackIndex++].asInt;
	public void Arg(out float value) => value = vm.valueStack.buffer[argStackIndex++].asFloat;
	public void Arg(out string value) => value = vm.heap.buffer[vm.valueStack.buffer[argStackIndex++].asInt] as string;
	public void Arg<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		var marshal = new ReaderMarshaler(vm, argStackIndex);
		value.Marshal(ref marshal);
		argStackIndex += MarshalSizeOf<T>.size;
	}
	public void Arg(out object value) => value = vm.heap.buffer[vm.valueStack.buffer[argStackIndex++].asInt];

	public FunctionBody<Unit> Body([CallerMemberName] string functionName = "") => new FunctionBody<Unit>(vm);
	public FunctionBody<bool> BodyOfBool([CallerMemberName] string functionName = "") => new FunctionBody<bool>(vm);
	public FunctionBody<int> BodyOfInt([CallerMemberName] string functionName = "") => new FunctionBody<int>(vm);
	public FunctionBody<float> BodyOfFloat([CallerMemberName] string functionName = "") => new FunctionBody<float>(vm);
	public FunctionBody<string> BodyOfString([CallerMemberName] string functionName = "") => new FunctionBody<string>(vm);
	public FunctionBody<T> BodyOfStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable => new FunctionBody<T>(vm);
	public FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class => new FunctionBody<object>(vm);
}

public struct DefinitionContext : IContext
{
	public sealed class Definition : System.Exception
	{
		public readonly string functionName;
		public FunctionTypeBuilder functionTypeBuilder;

		public Definition(string functionName, FunctionTypeBuilder functionTypeBuilder) : base("", null)
		{
			this.functionName = functionName;
			this.functionTypeBuilder = functionTypeBuilder;
		}
	}

	internal ByteCodeChunk chunk;
	internal FunctionTypeBuilder builder;

	public DefinitionContext(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginFunctionType();
	}

	public void Arg(out bool value)
	{
		value = default;
		builder.WithParam(new ValueType(TypeKind.Bool));
	}
	public void Arg(out int value)
	{
		value = default;
		builder.WithParam(new ValueType(TypeKind.Int));
	}
	public void Arg(out float value)
	{
		value = default;
		builder.WithParam(new ValueType(TypeKind.Float));
	}
	public void Arg(out string value)
	{
		value = default;
		builder.WithParam(new ValueType(TypeKind.String));
	}
	public void Arg<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		builder.WithParam(MarshalHelper.RegisterStruct<T>(chunk));
	}
	public void Arg(out object value)
	{
		value = default;
		builder.WithParam(new ValueType(TypeKind.NativeObject));
	}

	public FunctionBody<Unit> Body([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Unit);
		throw new Definition(functionName, builder);
	}
	public FunctionBody<bool> BodyOfBool([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Bool);
		throw new Definition(functionName, builder);
	}
	public FunctionBody<int> BodyOfInt([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Int);
		throw new Definition(functionName, builder);
	}
	public FunctionBody<float> BodyOfFloat([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Float);
		throw new Definition(functionName, builder);
	}
	public FunctionBody<string> BodyOfString([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.String);
		throw new Definition(functionName, builder);
	}
	public FunctionBody<T> BodyOfStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable
	{
		builder.returnType = MarshalHelper.RegisterStruct<T>(chunk);
		throw new Definition(functionName, builder);
	}
	public FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class
	{
		builder.returnType = new ValueType(TypeKind.NativeObject);
		throw new Definition(functionName, builder);
	}
}
