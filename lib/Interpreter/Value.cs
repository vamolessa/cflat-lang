using System.Runtime.InteropServices;

public enum ValueType
{
	Unknown,
	Nil,
	Bool,
	Int,
	Float,
	String,
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

public readonly struct Value
{
	public readonly ValueData data;
	public readonly ValueType type;

	public Value(ValueType type, ValueData data)
	{
		this.type = type;
		this.data = data;
	}

	public Value(bool value)
	{
		type = ValueType.Bool;
		data = new ValueData(value);
	}

	public Value(int value)
	{
		type = ValueType.Int;
		data = new ValueData(value);
	}

	public Value(float value)
	{
		type = ValueType.Int;
		data = new ValueData(value);
	}

	public bool IsTruthy()
	{
		return type != ValueType.Nil && (type != ValueType.Bool || data.asBool);
	}

	public static string AsString(object[] objs, ValueData data, ValueType type)
	{
		switch (type)
		{
		case ValueType.Nil:
			return "nil";
		case ValueType.Bool:
			return data.asBool ? "true" : "false";
		case ValueType.Int:
			return data.asInt.ToString();
		case ValueType.Float:
			return data.asFloat.ToString();
		case ValueType.String:
			return objs[data.asInt].ToString();
		default:
			return "<invalid value>";
		}
	}
}