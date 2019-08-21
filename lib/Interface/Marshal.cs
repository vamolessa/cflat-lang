public interface IMarshalable
{
	void Marshal<M>(ref M marshaler) where M : IMarshaler;
}

internal readonly struct ReflectionData
{
	public readonly bool initialized;
	public readonly ValueType type;
	public readonly int size;

	public ReflectionData(ValueType type, int size)
	{
		this.initialized = true;
		this.type = type;
		this.size = size;
	}
}

internal static class Marshal
{
	private static class ReflectionCache<T>
	{
		public static ReflectionData data;
	}

	public static ReflectionData ReflectOn<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		if (ReflectionCache<T>.data.initialized)
			return ReflectionCache<T>.data;

		var name = typeof(T).Name;
		var marshal = new DefinitionMarshaler(chunk);
		default(T).Marshal(ref marshal);
		var structTypeIndex = marshal.builder.Build(name);

		var reflection = new ReflectionData(
			new ValueType(TypeKind.Struct, structTypeIndex),
			chunk.structTypes.buffer[structTypeIndex].size
		);
		ReflectionCache<T>.data = reflection;
		return reflection;
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

internal struct ReadMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public static ReadMarshaler ArgFor<T>(VirtualMachine vm, ref int stackIndex) where T : struct, IMarshalable
	{
		var marshaler = new ReadMarshaler(vm, stackIndex);
		stackIndex += global::Marshal.ReflectOn<T>(vm.chunk).size;
		return marshaler;
	}

	public static ReadMarshaler For<T>(VirtualMachine vm) where T : struct, IMarshalable
	{
		var marshaler = new ReadMarshaler(vm, vm.valueStack.count);
		vm.valueStack.Grow(global::Marshal.ReflectOn<T>(vm.chunk).size);
		return marshaler;
	}

	public ReadMarshaler(VirtualMachine vm, int stackIndex)
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

internal struct WriteMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public static WriteMarshaler For<T>(VirtualMachine vm) where T : struct, IMarshalable
	{
		var marshaler = new WriteMarshaler(vm, vm.valueStack.count);
		vm.valueStack.Grow(global::Marshal.ReflectOn<T>(vm.chunk).size);
		return marshaler;
	}

	public WriteMarshaler(VirtualMachine vm, int stackIndex)
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

internal struct DefinitionMarshaler : IMarshaler
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
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithField(name, global::Marshal.ReflectOn<T>(chunk).type);
}