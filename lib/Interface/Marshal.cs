public interface IStruct : IMarshalable
{
}

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
	public sealed class InvalidReflectionType : System.Exception { }

	internal static class ReflectionCache<T> where T : struct, IMarshalable
	{
		public static ReflectionData data;
	}

	public static ReflectionData ReflectOn<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		var type = typeof(T);
		if (typeof(IStruct).IsAssignableFrom(type))
		{
			var marshaler = new StructDefinitionMarshaler(chunk);
			return ReflectOn<T, StructDefinitionMarshaler>(chunk, ref marshaler);
		}
		else if (typeof(ITuple).IsAssignableFrom(type))
		{
			var marshaler = new TupleDefinitionMarshaler(chunk);
			return ReflectOn<T, TupleDefinitionMarshaler>(chunk, ref marshaler);
		}

		throw new InvalidReflectionType();
	}

	public static ReflectionData ReflectOn<T, M>(ByteCodeChunk chunk, ref M marshaler)
		where T : struct, IMarshalable
		where M : IDefinitionMarshaler
	{
		if (ReflectionCache<T>.data.initialized)
			return ReflectionCache<T>.data;

		var name = typeof(T).Name;
		default(T).Marshal(ref marshaler);
		marshaler.FinishDefinition<T>();

		return ReflectionCache<T>.data;
	}

	public static ReflectionData ReflectOnStruct<T>(ByteCodeChunk chunk) where T : struct, IStruct
	{
		var marshaler = new StructDefinitionMarshaler(chunk);
		return ReflectOn<T, StructDefinitionMarshaler>(chunk, ref marshaler);
	}

	public static ReflectionData ReflectOnTuple<T>(ByteCodeChunk chunk) where T : struct, ITuple
	{
		var marshaler = new TupleDefinitionMarshaler(chunk);
		return ReflectOn<T, TupleDefinitionMarshaler>(chunk, ref marshaler);
	}
}

public interface IMarshaler
{
	void Marshal(ref bool value, string name);
	void Marshal(ref int value, string name);
	void Marshal(ref float value, string name);
	void Marshal(ref string value, string name);
	void Marshal<T>(ref T value, string name) where T : struct, IMarshalable;
	void Marshal(ref object value, string name);
}

internal interface IDefinitionMarshaler : IMarshaler
{
	void FinishDefinition<T>() where T : struct, IMarshalable;
}

internal struct ReadMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

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

	public void Marshal(ref object value, string name) => value = vm.heap.buffer[vm.valueStack.buffer[stackIndex++].asInt];
}

internal struct WriteMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

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

	public void Marshal(ref object value, string name)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.heap.count;
		vm.heap.PushBack(value);
	}
}

internal struct StructDefinitionMarshaler : IDefinitionMarshaler
{
	internal ByteCodeChunk chunk;
	internal StructTypeBuilder builder;

	public StructDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginStructType();
	}

	public void Marshal(ref bool value, string name) => builder.WithField(name, new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithField(name, new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithField(name, new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithField(name, new ValueType(TypeKind.String));
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithField(name, global::Marshal.ReflectOn<T>(chunk).type);
	public void Marshal(ref object value, string name) => builder.WithField(name, new ValueType(TypeKind.NativeObject));

	public void FinishDefinition<T>() where T : struct, IMarshalable
	{
		var structTypeIndex = builder.Build(typeof(T).Name);

		var reflection = new ReflectionData(
			new ValueType(TypeKind.Struct, structTypeIndex),
			chunk.structTypes.buffer[structTypeIndex].size
		);
		global::Marshal.ReflectionCache<T>.data = reflection;
	}
}

internal struct TupleDefinitionMarshaler : IDefinitionMarshaler
{
	internal ByteCodeChunk chunk;
	internal TupleTypeBuilder builder;

	public TupleDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginTupleType();
	}

	public void Marshal(ref bool value, string name) => builder.WithElement(new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithElement(new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithElement(new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithElement(new ValueType(TypeKind.String));
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithElement(global::Marshal.ReflectOn<T>(chunk).type);
	public void Marshal(ref object value, string name) => builder.WithElement(new ValueType(TypeKind.NativeObject));

	public void FinishDefinition<T>() where T : struct, IMarshalable
	{
		var tupleTypeIndex = builder.Build();

		var reflection = new ReflectionData(
			new ValueType(TypeKind.Tuple, tupleTypeIndex),
			chunk.tupleTypes.buffer[tupleTypeIndex].size
		);
		global::Marshal.ReflectionCache<T>.data = reflection;
	}
}