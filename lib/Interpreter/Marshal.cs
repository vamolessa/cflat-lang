public interface IMarshalable
{
	int Size { get; }
	void Read<M>(ref M marshal) where M : IMarshal;
	void Write<M>(ref M marshal) where M : IMarshal;
}

public sealed class WrongStructSizeException : System.Exception
{
	public readonly System.Type type;
	public readonly int expectedSize;

	public WrongStructSizeException(System.Type type, int expectedSize)
	{
		this.type = type;
		this.expectedSize = expectedSize;
	}
}

public static class MarshalHelper
{
	public static ValueType RegisterStruct<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		return RegisterStruct(chunk, default(T));
	}

	public static ValueType RegisterStruct<T>(ByteCodeChunk chunk, T value) where T : IMarshalable
	{
		var type = value.GetType();
		var name = type.Name;
		for (var i = 0; i < chunk.structTypes.count; i++)
		{
			if (chunk.structTypes.buffer[i].name == name)
				return new ValueType(TypeKind.Struct, i);
		}

		var marshal = new DefinitionMarshal(chunk);
		value.Write(ref marshal);
		var structTypeIndex = marshal.builder.Build(name);
		var size = chunk.structTypes.buffer[structTypeIndex].size;
		if (size == value.Size)
			return new ValueType(TypeKind.Struct, structTypeIndex);

		throw new WrongStructSizeException(type, size);
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
	public void Write<T>(T value, string name) where T : struct, IMarshalable => builder.WithField(name, MarshalHelper.RegisterStruct<T>(chunk));
}