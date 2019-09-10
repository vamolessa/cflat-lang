public readonly struct Empty : ITuple
{
	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
	}
}

public readonly struct Unit : IMarshalable
{
	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(null);
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

	public static implicit operator string(String self)
	{
		return self.value;
	}
}

public struct Object : IMarshalable
{
	public object value;

	public Object(object value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		marshaler.Marshal(ref value, null);
	}
}

public struct Array<T> : IMarshalable, IReflectable where T : struct, IMarshalable
{
	internal VirtualMachine vm;
	internal int heapStartIndex;

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		vm = marshaler.VirtualMachine;
		marshaler.Marshal(ref heapStartIndex, null);
	}

	public int Length
	{
		get { return vm.valueHeap.buffer[heapStartIndex - 1].asInt; }
	}

	public T this[int index]
	{
		get
		{
			var size = global::Marshal.ReflectOn<T>(vm.chunk).size;
			var marshaler = new HeapReadMarshaler(vm, heapStartIndex + size * index);
			var value = default(T);
			value.Marshal(ref marshaler);
			return value;
		}

		set
		{
			var size = global::Marshal.ReflectOn<T>(vm.chunk).size;
			var marshaler = new HeapWriteMarshaler(vm, heapStartIndex + size * index);
			value.Marshal(ref marshaler);
		}
	}

	Marshal.ReflectionData IReflectable.GetReflectionData(ByteCodeChunk chunk)
	{
		global::Marshal.SizeOf<Array<T>>.size = 1;
		return new Marshal.ReflectionData(
			global::Marshal.ReflectOn<T>(chunk).type.ToArrayType(),
			1
		);
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
}