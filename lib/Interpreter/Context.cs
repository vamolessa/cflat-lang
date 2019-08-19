using System.Runtime.CompilerServices;

public interface IContext
{
	void Arg(out bool value);
	void Arg(out int value);
	void Arg(out float value);
	void Arg(out string value);
	void Arg<T>(out T value) where T : struct, IMarshalable;

	FunctionBodyUnit BodyUnit([CallerMemberName] string functionName = "");
	FunctionBodyBool BodyBool([CallerMemberName] string functionName = "");
	FunctionBodyInt BodyInt([CallerMemberName] string functionName = "");
	FunctionBodyFloat BodyFloat([CallerMemberName] string functionName = "");
	FunctionBodyString BodyString([CallerMemberName] string functionName = "");
	FunctionBodyStruct<T> BodyStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable;
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
		var marshal = new RuntimeMarshal(vm, argStackIndex);
		value.Read(ref marshal);
		argStackIndex += value.Size;
	}

	public FunctionBodyUnit BodyUnit([CallerMemberName] string functionName = "") => new FunctionBodyUnit(vm);
	public FunctionBodyBool BodyBool([CallerMemberName] string functionName = "") => new FunctionBodyBool(vm);
	public FunctionBodyInt BodyInt([CallerMemberName] string functionName = "") => new FunctionBodyInt(vm);
	public FunctionBodyFloat BodyFloat([CallerMemberName] string functionName = "") => new FunctionBodyFloat(vm);
	public FunctionBodyString BodyString([CallerMemberName] string functionName = "") => new FunctionBodyString(vm);
	public FunctionBodyStruct<T> BodyStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable => new FunctionBodyStruct<T>(vm);
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

	public FunctionBodyUnit BodyUnit([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Unit);
		throw new Definition(functionName, builder);
	}
	public FunctionBodyBool BodyBool([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Bool);
		throw new Definition(functionName, builder);
	}
	public FunctionBodyInt BodyInt([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Int);
		throw new Definition(functionName, builder);
	}
	public FunctionBodyFloat BodyFloat([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.Float);
		throw new Definition(functionName, builder);
	}
	public FunctionBodyString BodyString([CallerMemberName] string functionName = "")
	{
		builder.returnType = new ValueType(TypeKind.String);
		throw new Definition(functionName, builder);
	}
	public FunctionBodyStruct<T> BodyStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable
	{
		builder.returnType = new ValueType(TypeKind.Struct);
		throw new Definition(functionName, builder);
	}
}
