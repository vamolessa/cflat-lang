using System.Runtime.CompilerServices;

public interface IContext
{
	bool ArgBool();
	int ArgInt();
	float ArgFloat();
	string ArgString();
	T ArgStruct<T>() where T : struct, IMarshalable;
	T ArgObject<T>() where T : class;

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

	public bool ArgBool() => vm.valueStack.buffer[argStackIndex++].asBool;
	public int ArgInt() => vm.valueStack.buffer[argStackIndex++].asInt;
	public float ArgFloat() => vm.valueStack.buffer[argStackIndex++].asFloat;
	public string ArgString() => vm.heap.buffer[vm.valueStack.buffer[argStackIndex++].asInt] as string;
	public T ArgStruct<T>() where T : struct, IMarshalable
	{
		var value = default(T);
		var marshaler = new ReadMarshaler<T>(vm, ref argStackIndex);
		value.Marshal(ref marshaler);
		return value;
	}
	public T ArgObject<T>() where T : class => vm.heap.buffer[vm.valueStack.buffer[argStackIndex++].asInt] as T;

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
	public T ArgStruct<T>() where T : struct, IMarshalable
	{
		builder.WithParam(Marshal.ReflectOn<T>(chunk).type);
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
		builder.returnType = Marshal.ReflectOn<T>(chunk).type;
		throw new Definition(functionName, builder);
	}
	public FunctionBody<object> BodyOfObject<T>([CallerMemberName] string functionName = "") where T : class
	{
		builder.returnType = new ValueType(TypeKind.NativeObject);
		throw new Definition(functionName, builder);
	}
}
