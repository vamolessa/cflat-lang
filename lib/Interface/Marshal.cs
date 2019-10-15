public interface IStruct : IMarshalable
{
}

public interface IClass : IMarshalable
{
	System.Type ClassType { get; }
}

public interface IMarshalable
{
	void Marshal<M>(ref M marshaler) where M : IMarshaler;
}

internal interface IReflectable
{
	Marshal.ReflectionData GetReflectionData(ByteCodeChunk chunk);
}

internal static class Marshal
{
	public sealed class InvalidReflectionException : System.Exception { }
	public sealed class InvalidDefinitionException : System.Exception { }

	internal readonly struct ReflectionData
	{
		public readonly ValueType type;

		public ReflectionData(ValueType type)
		{
			this.type = type;
		}
	}

	internal static class SizeOf<T> where T : struct, IMarshalable
	{
		public static byte size;
	}

	internal static class BasicTypeOf<T>
	{
		public static Option<ValueType> type;

		static BasicTypeOf()
		{
			BasicTypeOf<Unit>.type = Option.Some(new ValueType(TypeKind.Unit));
			BasicTypeOf<Bool>.type = Option.Some(new ValueType(TypeKind.Bool));
			BasicTypeOf<Int>.type = Option.Some(new ValueType(TypeKind.Int));
			BasicTypeOf<Float>.type = Option.Some(new ValueType(TypeKind.Float));
			BasicTypeOf<String>.type = Option.Some(new ValueType(TypeKind.String));

			SizeOf<Unit>.size = 1;
			SizeOf<Bool>.size = 1;
			SizeOf<Int>.size = 1;
			SizeOf<Float>.size = 1;
			SizeOf<String>.size = 1;
		}
	}

	public static ReflectionData ReflectOn<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		var type = typeof(T);
		var marshalType = Marshal.BasicTypeOf<T>.type;

		if (marshalType.isSome)
			return new ReflectionData(marshalType.value);
		else if (typeof(ITuple).IsAssignableFrom(type))
			return new TupleDefinitionMarshaler<T>(chunk).GetReflectionData();
		else if (typeof(IStruct).IsAssignableFrom(type))
			return new StructDefinitionMarshaler<T>(chunk).GetReflectionData();
		else if (typeof(IClass).IsAssignableFrom(type))
			return new ClassDefinitionMarshaler<T>(chunk).GetReflectionData();
		else if (typeof(IReflectable).IsAssignableFrom(type))
			return (default(T) as IReflectable).GetReflectionData(chunk);

		throw new InvalidReflectionException();
	}

	public static ReflectionData ReflectOnTuple<T>(ByteCodeChunk chunk) where T : struct, ITuple
	{
		var marshaler = new TupleDefinitionMarshaler<T>(chunk);
		return marshaler.GetReflectionData();
	}

	public static ReflectionData ReflectOnStruct<T>(ByteCodeChunk chunk) where T : struct, IStruct
	{
		var marshaler = new StructDefinitionMarshaler<T>(chunk);
		return marshaler.GetReflectionData();
	}

	public static ReflectionData ReflectOnClass<T>(ByteCodeChunk chunk) where T : struct, IClass
	{
		var marshaler = new ClassDefinitionMarshaler<T>(chunk);
		return marshaler.GetReflectionData();
	}

	public static object GetObject(ref StackReadMarshaler marshaler, ValueType type)
	{
		if (type.IsKind(TypeKind.Bool))
		{
			var v = default(Bool);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.Int))
		{
			var v = default(Int);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.Float))
		{
			var v = default(Float);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.String))
		{
			var v = default(String);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else
		{
			return null;
		}
	}
}

public interface IMarshaler
{
	VirtualMachine VirtualMachine { get; }

	void Marshal(string name);
	void Marshal(ref bool value, string name);
	void Marshal(ref int value, string name);
	void Marshal(ref float value, string name);
	void Marshal(ref string value, string name);
	void Marshal<T>(ref T value, string name) where T : struct, IMarshalable;
	void MarshalObject(ref object value);
}

internal interface IDefinitionMarshaler
{
	Marshal.ReflectionData GetReflectionData();
}

internal struct StackReadMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public StackReadMarshaler(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public VirtualMachine VirtualMachine { get { return vm; } }

	public void Marshal(string name) => stackIndex++;
	public void Marshal(ref bool value, string name) => value = vm.valueStack.buffer[stackIndex++].asBool;
	public void Marshal(ref int value, string name) => value = vm.valueStack.buffer[stackIndex++].asInt;
	public void Marshal(ref float value, string name) => value = vm.valueStack.buffer[stackIndex++].asFloat;
	public void Marshal(ref string value, string name) => value = vm.nativeObjects.buffer[vm.valueStack.buffer[stackIndex++].asInt] as string;
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => value.Marshal(ref this);
	public void MarshalObject(ref object value) => value = vm.nativeObjects.buffer[vm.valueStack.buffer[stackIndex++].asInt];
}

internal struct StackWriteMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public StackWriteMarshaler(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public VirtualMachine VirtualMachine { get { return vm; } }

	public void Marshal(string name) => stackIndex++;
	public void Marshal(ref bool value, string name) => vm.valueStack.buffer[stackIndex++].asBool = value;
	public void Marshal(ref int value, string name) => vm.valueStack.buffer[stackIndex++].asInt = value;
	public void Marshal(ref float value, string name) => vm.valueStack.buffer[stackIndex++].asFloat = value;
	public void Marshal(ref string value, string name)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.nativeObjects.count;
		vm.nativeObjects.PushBack(value);
	}
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => value.Marshal(ref this);
	public void MarshalObject(ref object value)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.nativeObjects.count;
		vm.nativeObjects.PushBack(value);
	}
}

internal struct HeapReadMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int heapIndex;

	public HeapReadMarshaler(VirtualMachine vm, int heapIndex)
	{
		this.vm = vm;
		this.heapIndex = heapIndex;
	}

	public VirtualMachine VirtualMachine { get { return vm; } }

	public void Marshal(string name) => heapIndex++;
	public void Marshal(ref bool value, string name) => value = vm.valueHeap.buffer[heapIndex++].asBool;
	public void Marshal(ref int value, string name) => value = vm.valueHeap.buffer[heapIndex++].asInt;
	public void Marshal(ref float value, string name) => value = vm.valueHeap.buffer[heapIndex++].asFloat;
	public void Marshal(ref string value, string name) => value = vm.nativeObjects.buffer[vm.valueHeap.buffer[heapIndex++].asInt] as string;
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => value.Marshal(ref this);
	public void MarshalObject(ref object value) => value = vm.nativeObjects.buffer[vm.valueHeap.buffer[heapIndex++].asInt];
}

internal struct HeapWriteMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int heapIndex;

	public HeapWriteMarshaler(VirtualMachine vm, int heapIndex)
	{
		this.vm = vm;
		this.heapIndex = heapIndex;
	}

	public VirtualMachine VirtualMachine { get { return vm; } }

	public void Marshal(string name) => heapIndex++;
	public void Marshal(ref bool value, string name) => vm.valueHeap.buffer[heapIndex++].asBool = value;
	public void Marshal(ref int value, string name) => vm.valueHeap.buffer[heapIndex++].asInt = value;
	public void Marshal(ref float value, string name) => vm.valueHeap.buffer[heapIndex++].asFloat = value;
	public void Marshal(ref string value, string name)
	{
		vm.valueHeap.buffer[heapIndex++].asInt = vm.nativeObjects.count;
		vm.nativeObjects.PushBack(value);
	}
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => value.Marshal(ref this);
	public void MarshalObject(ref object value)
	{
		vm.valueHeap.buffer[heapIndex++].asInt = vm.nativeObjects.count;
		vm.nativeObjects.PushBack(value);
	}
}

internal struct TupleDefinitionMarshaler<A> : IMarshaler, IDefinitionMarshaler where A : struct, IMarshalable
{
	internal ByteCodeChunk chunk;
	internal TupleTypeBuilder builder;

	public TupleDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginTupleType();
	}

	public VirtualMachine VirtualMachine { get { return null; } }

	public void Marshal(string name) => builder.WithElement(new ValueType(TypeKind.Unit));
	public void Marshal(ref bool value, string name) => builder.WithElement(new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithElement(new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithElement(new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithElement(new ValueType(TypeKind.String));
	public void Marshal<B>(ref B value, string name) where B : struct, IMarshalable => builder.WithElement(global::Marshal.ReflectOn<B>(chunk).type);
	public void MarshalObject(ref object value) => throw new Marshal.InvalidDefinitionException();

	public Marshal.ReflectionData GetReflectionData()
	{
		default(A).Marshal(ref this);
		var result = builder.Build(out var typeIndex);
		if (result == TupleTypeBuilder.Result.Success)
		{
			global::Marshal.SizeOf<A>.size = chunk.tupleTypes.buffer[typeIndex].size;
			return new Marshal.ReflectionData(new ValueType(TypeKind.Tuple, typeIndex));
		}

		throw new Marshal.InvalidReflectionException();
	}
}

internal struct StructDefinitionMarshaler<A> : IMarshaler, IDefinitionMarshaler where A : struct, IMarshalable
{
	internal ByteCodeChunk chunk;
	internal StructTypeBuilder builder;

	public StructDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginStructType();
	}

	public VirtualMachine VirtualMachine { get { return null; } }

	public void Marshal(string name) => builder.WithField(name, new ValueType(TypeKind.Unit));
	public void Marshal(ref bool value, string name) => builder.WithField(name, new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithField(name, new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithField(name, new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithField(name, new ValueType(TypeKind.String));
	public void Marshal<B>(ref B value, string name) where B : struct, IMarshalable => builder.WithField(name, global::Marshal.ReflectOn<B>(chunk).type);
	public void MarshalObject(ref object value) => throw new Marshal.InvalidDefinitionException();

	public Marshal.ReflectionData GetReflectionData()
	{
		default(A).Marshal(ref this);
		var result = builder.Build(typeof(A).Name, out var typeIndex);
		if (
			result == StructTypeBuilder.Result.Success ||
			(
				result == StructTypeBuilder.Result.DuplicatedName &&
				global::Marshal.SizeOf<A>.size > 0
			)
		)
		{
			global::Marshal.SizeOf<A>.size = chunk.structTypes.buffer[typeIndex].size;
			return new Marshal.ReflectionData(new ValueType(TypeKind.Struct, typeIndex));
		}

		throw new Marshal.InvalidReflectionException();
	}
}

internal struct ClassDefinitionMarshaler<T> : IDefinitionMarshaler where T : struct, IMarshalable
{
	internal ByteCodeChunk chunk;
	internal ClassTypeBuilder builder;

	public ClassDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginClassType();
	}

	public Marshal.ReflectionData GetReflectionData()
	{
		var classType = (default(T) as IClass).ClassType;
		var result = builder.Build(classType.Name, classType, out var typeIndex);
		if (
			result == ClassTypeBuilder.Result.Success ||
			(
				result == ClassTypeBuilder.Result.DuplicatedName &&
				global::Marshal.SizeOf<T>.size > 0
			)
		)
		{
			Marshal.SizeOf<T>.size = 1;
			return new Marshal.ReflectionData(new ValueType(TypeKind.NativeClass, typeIndex));
		}

		throw new Marshal.InvalidReflectionException();
	}
}

internal struct FunctionDefinitionMarshaler<A, R> : IMarshaler, IDefinitionMarshaler
	where A : struct, ITuple
	where R : struct, IMarshalable
{
	internal ByteCodeChunk chunk;
	internal FunctionTypeBuilder builder;

	public FunctionDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginFunctionType();
	}

	public VirtualMachine VirtualMachine { get { return null; } }

	public void Marshal(string name) => builder.WithParam(new ValueType(TypeKind.Unit));
	public void Marshal(ref bool value, string name) => builder.WithParam(new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithParam(new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithParam(new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithParam(new ValueType(TypeKind.String));
	public void Marshal<M>(ref M value, string name) where M : struct, IMarshalable => builder.WithParam(global::Marshal.ReflectOn<M>(chunk).type);
	public void MarshalObject(ref object value) => throw new Marshal.InvalidDefinitionException();

	public void Returns()
	{
		builder.returnType = global::Marshal.ReflectOn<R>(chunk).type;
	}

	public Marshal.ReflectionData GetReflectionData()
	{
		default(A).Marshal(ref this);
		var result = builder.Build(out var typeIndex);
		if (result == FunctionTypeBuilder.Result.Success)
		{
			return new Marshal.ReflectionData(new ValueType(TypeKind.Function, typeIndex));
		}

		throw new Marshal.InvalidReflectionException();
	}
}
