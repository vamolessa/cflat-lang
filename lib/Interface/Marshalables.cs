public readonly struct Empty : ITuple
{
	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		return new ValueType();
	}
}

public readonly struct Unit : IMarshalable
{
	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Unit>.size = 1;
		return new ValueType(TypeKind.Unit);
	}
}

public struct Bool : IMarshalable
{
	public bool value;

	public Bool(bool value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(ref value, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Bool>.size = 1;
		return new ValueType(TypeKind.Bool);
	}

	public static implicit operator Bool(bool value)
	{
		return new Bool(value);
	}

	public static implicit operator bool(Bool self)
	{
		return self.value;
	}
}

public struct Int : IMarshalable
{
	public int value;

	public Int(int value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(ref value, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Int>.size = 1;
		return new ValueType(TypeKind.Int);
	}

	public static implicit operator Int(int value)
	{
		return new Int(value);
	}

	public static implicit operator int(Int self)
	{
		return self.value;
	}
}

public struct Float : IMarshalable
{
	public float value;

	public Float(float value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(ref value, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Float>.size = 1;
		return new ValueType(TypeKind.Float);
	}

	public static implicit operator Float(float value)
	{
		return new Float(value);
	}

	public static implicit operator float(Float self)
	{
		return self.value;
	}
}

public struct String : IMarshalable
{
	public string value;

	public String(string value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(ref value, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<String>.size = 1;
		return new ValueType(TypeKind.String);
	}

	public static implicit operator String(string value)
	{
		return new String(value);
	}

	public static implicit operator string(String self)
	{
		return self.value;
	}
}

public struct Struct<T> : IMarshalable where T : struct, IStruct
{
	public T value;

	public Struct(T value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		value.Marshal(ref marshaler);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		return new StructDefinitionMarshaler<T>(chunk).GetDefinedType();
	}

	public static implicit operator Struct<T>(T value)
	{
		return new Struct<T>(value);
	}

	public static implicit operator T(Struct<T> self)
	{
		return self.value;
	}
}

public struct Class<T> : IMarshalable where T : class
{
	public T value;

	public Class(T value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		object o = value;
		marshaler.MarshalObject(ref o);
		value = o as T;
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		var classType = typeof(T);
		var builder = chunk.BeginClassType();
		var result = builder.Build(classType.Name, classType, out var typeIndex);
		if (result == ClassTypeBuilder.Result.Success)
		{
			global::Marshal.SizeOf<Class<T>>.size = 1;
			return new ValueType(TypeKind.NativeClass, typeIndex);
		}

		throw new Marshal.InvalidReflectionException();
	}

	public static implicit operator Class<T>(T value)
	{
		return new Class<T>(value);
	}

	public static implicit operator T(Class<T> self)
	{
		return self.value;
	}
}

public struct Array<T> : IMarshalable where T : struct, IMarshalable
{
	internal VirtualMachine vm;
	internal int headAddress;

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		vm = marshaler.VirtualMachine;
		marshaler.Marshal(ref headAddress, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Array<T>>.size = 1;
		return global::Marshal.TypeOf<T>(chunk).ToArrayType();
	}

	public int Length
	{
		get { return vm.memory.values[headAddress - 1].asInt; }
	}

	public T this[int index]
	{
		get
		{
			var size = global::Marshal.SizeOf<T>.size;
			var marshaler = new MemoryReadMarshaler(vm, headAddress + size * index);
			var value = default(T);
			value.Marshal(ref marshaler);
			return value;
		}

		set
		{
			var size = global::Marshal.SizeOf<T>.size;
			var marshaler = new MemoryWriteMarshaler(vm, headAddress + size * index);
			value.Marshal(ref marshaler);
		}
	}
}

public struct Ref<T> : IMarshalable where T : struct, IMarshalable
{
	internal VirtualMachine vm;
	internal int address;

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		vm = marshaler.VirtualMachine;
		marshaler.Marshal(ref address, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Ref<T>>.size = 1;
		return global::Marshal.TypeOf<T>(chunk).ToReferenceType(false);
	}

	public T Value
	{
		get
		{
			var size = global::Marshal.SizeOf<T>.size;
			var marshaler = new MemoryReadMarshaler(vm, address);
			var value = default(T);
			value.Marshal(ref marshaler);
			return value;
		}
	}
}

public struct MutRef<T> : IMarshalable where T : struct, IMarshalable
{
	internal VirtualMachine vm;
	internal int address;

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		vm = marshaler.VirtualMachine;
		marshaler.Marshal(ref address, null);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<MutRef<T>>.size = 1;
		return global::Marshal.TypeOf<T>(chunk).ToReferenceType(true);
	}

	public T Value
	{
		get
		{
			var size = global::Marshal.SizeOf<T>.size;
			var marshaler = new MemoryReadMarshaler(vm, address);
			var value = default(T);
			value.Marshal(ref marshaler);
			return value;
		}

		set
		{
			var size = global::Marshal.SizeOf<T>.size;
			var marshaler = new MemoryWriteMarshaler(vm, address);
			value.Marshal(ref marshaler);
		}
	}
}


public interface ITuple : IMarshalable { }

public static class Tuple
{
	public static Tuple<E0> New<E0>(E0 e0)
		where E0 : IMarshalable
	{
		return new Tuple<E0>(e0);
	}

	public static Tuple<E0, E1> New<E0, E1>(E0 e0, E1 e1)
		where E0 : IMarshalable
		where E1 : IMarshalable
	{
		return new Tuple<E0, E1>(e0, e1);
	}

	public static Tuple<E0, E1, E2> New<E0, E1, E2>(E0 e0, E1 e1, E2 e2)
		where E0 : IMarshalable
		where E1 : IMarshalable
		where E2 : IMarshalable
	{
		return new Tuple<E0, E1, E2>(e0, e1, e2);
	}

	public static Tuple<E0, E1, E2, E3> New<E0, E1, E2, E3>(E0 e0, E1 e1, E2 e2, E3 e3)
		where E0 : IMarshalable
		where E1 : IMarshalable
		where E2 : IMarshalable
		where E3 : IMarshalable
	{
		return new Tuple<E0, E1, E2, E3>(e0, e1, e2, e3);
	}
}

public struct Tuple<E0> : ITuple
	where E0 : IMarshalable
{
	public E0 e0;

	public Tuple(E0 e0)
	{
		this.e0 = e0;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		e0.Marshal(ref marshaler);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		return new TupleDefinitionMarshaler<Tuple<E0>>(chunk).GetDefinedType();
	}
}

public struct Tuple<E0, E1> : ITuple
	where E0 : IMarshalable
	where E1 : IMarshalable
{
	public E0 e0;
	public E1 e1;

	public Tuple(E0 e0, E1 e1)
	{
		this.e0 = e0;
		this.e1 = e1;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		e0.Marshal(ref marshaler);
		e1.Marshal(ref marshaler);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		return new TupleDefinitionMarshaler<Tuple<E0, E1>>(chunk).GetDefinedType();
	}
}

public struct Tuple<E0, E1, E2> : ITuple
	where E0 : IMarshalable
	where E1 : IMarshalable
	where E2 : IMarshalable
{
	public E0 e0;
	public E1 e1;
	public E2 e2;

	public Tuple(E0 e0, E1 e1, E2 e2)
	{
		this.e0 = e0;
		this.e1 = e1;
		this.e2 = e2;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		e0.Marshal(ref marshaler);
		e1.Marshal(ref marshaler);
		e2.Marshal(ref marshaler);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		return new TupleDefinitionMarshaler<Tuple<E0, E1, E2>>(chunk).GetDefinedType();
	}
}

public struct Tuple<E0, E1, E2, E3> : ITuple
	where E0 : IMarshalable
	where E1 : IMarshalable
	where E2 : IMarshalable
	where E3 : IMarshalable
{
	public E0 e0;
	public E1 e1;
	public E2 e2;
	public E3 e3;

	public Tuple(E0 e0, E1 e1, E2 e2, E3 e3)
	{
		this.e0 = e0;
		this.e1 = e1;
		this.e2 = e2;
		this.e3 = e3;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		e0.Marshal(ref marshaler);
		e1.Marshal(ref marshaler);
		e2.Marshal(ref marshaler);
		e3.Marshal(ref marshaler);
	}

	public ValueType GetType(ByteCodeChunk chunk)
	{
		return new TupleDefinitionMarshaler<Tuple<E0, E1, E2, E3>>(chunk).GetDefinedType();
	}
}