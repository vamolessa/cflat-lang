using System.Runtime.InteropServices;
using System.Text;

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
	Unit,
	Bool,
	Int,
	Float,
	String,
	Function,
	Struct,
	Custom,
}

public static class ValueTypeHelper
{
	public static ValueType GetKind(ValueType type)
	{
		return (ValueType)((int)type & 0b1111);
	}

	public static int GetIndex(ValueType type)
	{
		return (int)type >> 4;
	}

	public static ValueType SetIndex(ValueType type, int index)
	{
		return (ValueType)(((int)type & 0b1111) | (index << 4));
	}

	public static string ToString(this ValueType type, ByteCodeChunk chunk)
	{
		var kind = GetKind(type);
		if (kind == ValueType.Function)
		{
			var index = GetIndex(type);
			return chunk.FormatFunctionType(index, new StringBuilder()).ToString();
		}

		return type.ToString();
	}
}

public readonly struct Function
{
	public readonly string name;
	public readonly int codeIndex;
	public readonly int typeIndex;

	public Function(string name, int codeIndex, int typeIndex)
	{
		this.name = name;
		this.codeIndex = codeIndex;
		this.typeIndex = typeIndex;
	}
}

public readonly struct FunctionType
{
	public readonly Slice parameters;
	public readonly ValueType returnType;

	public FunctionType(Slice parameters, ValueType returnType)
	{
		this.parameters = parameters;
		this.returnType = returnType;
	}
}

public readonly struct StructType
{
	public readonly string name;
	public readonly Slice fields;
	public readonly int size;

	public StructType(string name, Slice fields, int size)
	{
		this.name = name;
		this.fields = fields;
		this.size = size;
	}
}

public readonly struct StructTypeField
{
	public readonly string name;
	public readonly ValueType type;

	public StructTypeField(string name, ValueType type)
	{
		this.name = name;
		this.type = type;
	}
}