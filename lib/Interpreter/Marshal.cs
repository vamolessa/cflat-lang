public interface IMarshalable
{
	int Size { get; }
	void Read<M>(ref M marshal) where M : IMarshal;
	void Write<M>(ref M marshal) where M : IMarshal;
}

public static class MarshalHelper
{
	public static ValueType GetStructType<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		var marshal = new DefinitionMarshal(chunk);
		default(T).Write(ref marshal);
		var structTypeIndex = marshal.builder.BuildAnonymous();
		return new ValueType(TypeKind.Struct, structTypeIndex);
	}
}

public interface IMarshal
{
	void Read(out bool value);
	void Read(out int value);
	void Read(out float value);
	void Read(out string value);
	void Read<T>(out T value) where T : struct, IMarshalable;

	void Write(bool value, string name);
	void Write(int value, string name);
	void Write(float value, string name);
	void Write(string value, string name);
	void Write<T>(T value, string name) where T : struct, IMarshalable;
}

public struct RuntimeMarshal : IMarshal
{
	private VirtualMachine vm;
	private int stackIndex;

	public RuntimeMarshal(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public void Read(out bool value) => value = vm.valueStack.buffer[stackIndex++].asBool;

	public void Read(out int value) => value = vm.valueStack.buffer[stackIndex++].asInt;

	public void Read(out float value) => value = vm.valueStack.buffer[stackIndex++].asFloat;

	public void Read(out string value) => value = vm.heap.buffer[vm.valueStack.buffer[stackIndex++].asInt] as string;

	public void Read<T>(out T value) where T : struct, IMarshalable
	{
		value = default;
		value.Read(ref this);
	}

	public void Write(bool value, string name) => vm.valueStack.buffer[stackIndex++].asBool = value;

	public void Write(int value, string name) => vm.valueStack.buffer[stackIndex++].asInt = value;

	public void Write(float value, string name) => vm.valueStack.buffer[stackIndex++].asFloat = value;

	public void Write(string value, string name)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.heap.count;
		vm.heap.PushBack(value);
	}

	public void Write<T>(T value, string name) where T : struct, IMarshalable => value.Write(ref this);
}

public struct DefinitionMarshal : IMarshal
{
	internal ByteCodeChunk chunk;
	internal StructTypeBuilder builder;

	public DefinitionMarshal(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginStructType();
	}

	public void Read(out bool value) => value = default;
	public void Read(out int value) => value = default;
	public void Read(out float value) => value = default;
	public void Read(out string value) => value = default;
	public void Read<T>(out T value) where T : struct, IMarshalable => value = default;

	public void Write(bool value, string name) => builder.WithField(name, new ValueType(TypeKind.Bool));
	public void Write(int value, string name) => builder.WithField(name, new ValueType(TypeKind.Int));
	public void Write(float value, string name) => builder.WithField(name, new ValueType(TypeKind.Float));
	public void Write(string value, string name) => builder.WithField(name, new ValueType(TypeKind.String));
	public void Write<T>(T value, string name) where T : struct, IMarshalable => builder.WithField(name, MarshalHelper.GetStructType<T>(chunk));
}