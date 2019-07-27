using System.Runtime.InteropServices;

public enum ValueType
{
	ForeignObject,
	Nil,
	Bool,
	Int,
	Float,
	String,
	Boxed,
	Struct,
}

[StructLayout(LayoutKind.Explicit)]
public struct ValueData
{
	[FieldOffset(0)]
	public bool asBool;
	[FieldOffset(0)]
	public int asInt;
	[FieldOffset(0)]
	public float asFloat;

	public ValueData(bool value)
	{
		this = default(ValueData);
		asBool = value;
	}

	public ValueData(int value)
	{
		this = default(ValueData);
		asInt = value;
	}

	public ValueData(float value)
	{
		this = default(ValueData);
		asFloat = value;
	}
}