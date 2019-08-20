using System.Runtime.CompilerServices;

public interface IContext
{
	void Arg(out bool value);
	void Arg(out int value);
	void Arg(out float value);
	void Arg(out string value);
	void Arg<T>(out T value) where T : struct, IMarshalable;

	FunctionBody<T> Body<T>([CallerMemberName] string functionName = "");
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
		argStackIndex += value.Size;
	}

	public FunctionBody<T> Body<T>([CallerMemberName] string functionName = "") => new FunctionBody<T>(vm);
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
		value = default(T);
		builder.WithParam(MarshalHelper.RegisterStruct<T>(chunk));
	}

	public FunctionBody<T> Body<T>([CallerMemberName] string functionName = "")
	{
		var type = typeof(T);
		if (type == typeof(Unit))
			builder.returnType = new ValueType(TypeKind.Unit);
		else if (type == typeof(bool))
			builder.returnType = new ValueType(TypeKind.Bool);
		else if (type == typeof(int))
			builder.returnType = new ValueType(TypeKind.Int);
		else if (type == typeof(float))
			builder.returnType = new ValueType(TypeKind.Float);
		else if (type == typeof(string))
			builder.returnType = new ValueType(TypeKind.String);
		else if (type.IsValueType && typeof(IMarshalable).IsAssignableFrom(type))
			builder.returnType = MarshalHelper.RegisterStruct<IMarshalable>(chunk, (IMarshalable)default(T));
		else
			builder.returnType = new ValueType(TypeKind.Unit);

		throw new Definition(functionName, builder);
	}

	public FunctionBody<T> BodyStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable
	{
		builder.returnType = MarshalHelper.RegisterStruct<T>(chunk);
		throw new Definition(functionName, builder);
	}
}
