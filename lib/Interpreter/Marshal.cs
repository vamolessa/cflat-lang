public struct Anonymous<T> : IMarshalable
	where T : struct, IMarshalable
{
	public T value;

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		value.Marshal(ref marshaler);
	}
}

public interface IMarshalable
{
	void Marshal<M>(ref M marshaler) where M : IMarshaler;
}

internal static class MarshalSizeOf<T> where T : struct, IMarshalable
{
	public static int size = 1;
}

public static class MarshalHelper
{
	public static ValueType RegisterStruct<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		var name = typeof(T).Name;
		for (var i = 0; i < chunk.structTypes.count; i++)
		{
			if (chunk.structTypes.buffer[i].name == name)
				return new ValueType(TypeKind.Struct, i);
		}

		var marshal = new DefinitionMarshaler(chunk);
		default(T).Marshal(ref marshal);
		var structTypeIndex = marshal.builder.Build(name);
		MarshalSizeOf<T>.size = chunk.structTypes.buffer[structTypeIndex].size;
		return new ValueType(TypeKind.Struct, structTypeIndex);
	}
}

public interface IMarshaler
{
	void Marshal(ref bool value, string name);
	void Marshal(ref int value, string name);
	void Marshal(ref float value, string name);
	void Marshal(ref string value, string name);
	void Marshal<T>(ref T value, string name) where T : struct, IMarshalable;
}

public struct ReaderMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public ReaderMarshaler(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public void Marshal(ref bool value, string name) => value = vm.valueStack.buffer[stackIndex++].asBool;

	public void Marshal(ref int value, string name) => value = vm.valueStack.buffer[stackIndex++].asInt;

	public void Marshal(ref float value, string name) => value = vm.valueStack.buffer[stackIndex++].asFloat;

	public void Marshal(ref string value, string name) => value = vm.heap.buffer[vm.valueStack.buffer[stackIndex++].asInt] as string;

	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable
	{
		value = default;
		value.Marshal(ref this);
	}
}

public struct WriterMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public WriterMarshaler(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public void Marshal(ref bool value, string name) => vm.valueStack.buffer[stackIndex++].asBool = value;

	public void Marshal(ref int value, string name) => vm.valueStack.buffer[stackIndex++].asInt = value;

	public void Marshal(ref float value, string name) => vm.valueStack.buffer[stackIndex++].asFloat = value;

	public void Marshal(ref string value, string name)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.heap.count;
		vm.heap.PushBack(value);
	}

	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => value.Marshal(ref this);
}

public struct DefinitionMarshaler : IMarshaler
{
	internal ByteCodeChunk chunk;
	internal StructTypeBuilder builder;

	public DefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginStructType();
	}

	public void Marshal(ref bool value, string name) => builder.WithField(name, new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithField(name, new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithField(name, new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithField(name, new ValueType(TypeKind.String));
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithField(name, MarshalHelper.RegisterStruct<T>(chunk));
}