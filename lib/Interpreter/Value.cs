using System.Runtime.InteropServices;

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
		asInt = 0;
		asFloat = 0;
		asBool = value;
	}

	public ValueData(int value)
	{
		asBool = false;
		asFloat = 0.0f;
		asInt = value;
	}

	public ValueData(float value)
	{
		asBool = false;
		asInt = 0;
		asFloat = value;
	}
}

public enum ValueType : int
{
	ForeignObject = 0b001,
	Unit = 0b001,
	Bool = 0b010,
	Int = 0b011,
	Float = 0b100,
	String = 0b101,
	Function = 0b110,
	Custom = 0b111,
}

public static class ValueTypeHelper
{
	public static ValueType GetKind(ValueType type)
	{
		return (ValueType)((int)type & 0b111);
	}

	public static int GetIndex(ValueType type)
	{
		return (int)type >> 3;
	}

	public static ValueType SetIndex(ValueType type, int index)
	{
		return (ValueType)(((int)type & 0b111) | (index << 3));
	}
}

public readonly struct FunctionDefinition
{
	public readonly string name;
	public readonly int codeIndex;
	public readonly Slice parameters;
	public readonly ValueType returnType;

	public FunctionDefinition(string name, int codeIndex, Slice parameters, ValueType returnType)
	{
		this.name = name;
		this.codeIndex = codeIndex;
		this.parameters = parameters;
		this.returnType = returnType;
	}
}
