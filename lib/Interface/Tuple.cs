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
}

public struct Object<T> : IMarshalable
	where T : class
{
	public T value;

	public Object(T value)
	{
		this.value = value;
	}

	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
		object obj = value;
		marshaler.Marshal(ref obj, null);
		value = obj as T;
	}
}

public interface ITuple : IMarshalable { }

public struct Tuple : ITuple
{
	public void Marshal<M>(ref M marshaler) where M : IMarshaler
	{
	}

	public static Tuple New()
	{
		return new Tuple();
	}

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