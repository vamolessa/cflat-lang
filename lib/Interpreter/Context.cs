using System.Runtime.CompilerServices;

public interface IContext
{
	void Arg(out bool value);
	void Arg(out int value);
	void Arg(out float value);
	void Arg(out string value);
	void Arg<T>(out T value) where T : struct, IMarshalable;

	void ReturnsUnit([CallerMemberName] string functionName = "");
	void ReturnsInt([CallerMemberName] string functionName = "");
	void ReturnsFloat([CallerMemberName] string functionName = "");
	void ReturnsString([CallerMemberName] string functionName = "");
	void ReturnsStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable;

	void Pop();
	void Pop(out bool value);
	void Pop(out int value);
	void Pop(out float value);
	void Pop(out string value);
	void Pop<T>(out T value) where T : struct, IMarshalable;


	void Push();
	void Push(bool value);
	void Push(int value);
	void Push(float value);
	void Push(string value);
	void Push<T>(T value) where T : struct, IMarshalable;
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

	public void ReturnsUnit([CallerMemberName] string functionName = "") { }
	public void ReturnsInt([CallerMemberName] string functionName = "") { }
	public void ReturnsFloat([CallerMemberName] string functionName = "") { }
	public void ReturnsString([CallerMemberName] string functionName = "") { }
	public void ReturnsStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable { }

	public void Pop() => vm.valueStack.count--;
	public void Pop(out bool value) => value = vm.valueStack.buffer[--vm.valueStack.count].asBool;
	public void Pop(out int value) => value = vm.valueStack.buffer[--vm.valueStack.count].asInt;
	public void Pop(out float value) => value = vm.valueStack.buffer[--vm.valueStack.count].asFloat;
	public void Pop(out string value) => value = vm.heap.buffer[vm.valueStack.buffer[--vm.valueStack.count].asInt] as string;
	public void Pop<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		vm.valueStack.count -= value.Size;
		var marshal = new RuntimeMarshal(vm, vm.valueStack.count);
		value.Read(ref marshal);
	}

	public void Push() => vm.valueStack.PushBack(new ValueData());
	public void Push(bool value) => vm.valueStack.PushBack(new ValueData(value));
	public void Push(int value) => vm.valueStack.PushBack(new ValueData(value));
	public void Push(float value) => vm.valueStack.PushBack(new ValueData(value));
	public void Push(string value)
	{
		vm.valueStack.PushBack(new ValueData(vm.heap.count));
		vm.heap.PushBack(value);
	}
	public void Push<T>(T value) where T : struct, IMarshalable
	{
		var stackIndex = vm.valueStack.count;
		vm.valueStack.Grow(value.Size);
		var marshal = new RuntimeMarshal(vm, stackIndex);
		value.Write(ref marshal);
	}
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

	private void Returns(string functionName, ValueType returnType)
	{
		builder.returnType = returnType;
		throw new Definition(functionName, builder);
	}
	public void ReturnsUnit([CallerMemberName] string functionName = "") => Returns(functionName, new ValueType(TypeKind.Unit));
	public void ReturnsBool([CallerMemberName] string functionName = "") => Returns(functionName, new ValueType(TypeKind.Bool));
	public void ReturnsInt([CallerMemberName] string functionName = "") => Returns(functionName, new ValueType(TypeKind.Int));
	public void ReturnsFloat([CallerMemberName] string functionName = "") => Returns(functionName, new ValueType(TypeKind.Float));
	public void ReturnsString([CallerMemberName] string functionName = "") => Returns(functionName, new ValueType(TypeKind.String));
	public void ReturnsStruct<T>([CallerMemberName] string functionName = "") where T : struct, IMarshalable => Returns(functionName, MarshalHelper.RegisterStruct<T>(chunk));

	public void Pop() { }
	public void Pop(out bool value) => value = default;
	public void Pop(out int value) => value = default;
	public void Pop(out float value) => value = default;
	public void Pop(out string value) => value = default;
	public void Pop<T>(out T value) where T : struct, IMarshalable => value = default;

	public void Push() { }
	public void Push(bool value) { }
	public void Push(int value) { }
	public void Push(float value) { }
	public void Push(string value) { }
	public void Push<T>(T value) where T : struct, IMarshalable { }
}
