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

public enum TypeKind : byte
{
	Unit,
	Bool,
	Int,
	Float,
	String,
	Function,
	NativeFunction,
	Struct,
	Custom,
}

public readonly struct ValueType
{
	public readonly ushort index;
	public readonly TypeKind kind;
	public readonly bool isReference;

	public static ValueType Read(byte b0, byte b1, byte b2, byte b3)
	{
		return new ValueType(
			BytesHelper.BytesToShort(b0, b1),
			(TypeKind)b2,
			b3 != 0
		);
	}

	public ValueType(TypeKind kind)
	{
		this.kind = kind;
		this.isReference = false;
		this.index = 0;
	}

	public ValueType(TypeKind kind, int index)
	{
		this.kind = kind;
		this.isReference = false;
		this.index = (ushort)index;
	}

	public ValueType(ushort index, TypeKind kind, bool isReference)
	{
		this.kind = kind;
		this.isReference = isReference;
		this.index = index;
	}

	public bool IsEqualTo(ValueType other)
	{
		return
			kind == other.kind &&
			isReference == other.isReference &&
			index == other.index;
	}

	public bool IsSimple()
	{
		return isReference == false && index == 0;
	}

	public bool IsKind(TypeKind kind)
	{
		return this.kind == kind && isReference == false && index == 0;
	}

	public void Write(out byte b0, out byte b1, out byte b2, out byte b3)
	{
		BytesHelper.ShortToBytes(
			index,
			out b0,
			out b1
		);

		b2 = (byte)kind;
		b3 = isReference ? (byte)1 : (byte)0;
	}

	public string ToString(ByteCodeChunk chunk)
	{
		switch (kind)
		{
		case TypeKind.Unit:
			return "{}";
		case TypeKind.Bool:
			return "bool";
		case TypeKind.Int:
			return "int";
		case TypeKind.Float:
			return "float";
		case TypeKind.String:
			return "string";
		case TypeKind.Function:
			{
				var sb = new StringBuilder();
				chunk.FormatFunctionType(index, sb);
				return sb.ToString();
			}
		case TypeKind.NativeFunction:
			{
				var sb = new StringBuilder();
				sb.Append("native ");
				chunk.FormatFunctionType(index, sb);
				return sb.ToString();
			}
		case TypeKind.Struct:
			{
				var sb = new StringBuilder();
				chunk.FormatStructType(index, sb);
				return sb.ToString();
			}
		case TypeKind.Custom:
			return "custom";
		default:
			return "unreachable";
		}
	}
}

public readonly struct FunctionType
{
	public readonly Slice parameters;
	public readonly ValueType returnType;
	public readonly int parametersTotalSize;

	public FunctionType(Slice parameters, ValueType returnType, int parametersTotalSize)
	{
		this.parameters = parameters;
		this.returnType = returnType;
		this.parametersTotalSize = parametersTotalSize;
	}
}

public readonly struct Function
{
	public readonly string name;
	public readonly int typeIndex;
	public readonly int codeIndex;

	public Function(string name, int typeIndex, int codeIndex)
	{
		this.name = name;
		this.typeIndex = typeIndex;
		this.codeIndex = codeIndex;
	}
}

public readonly struct NativeFunction
{
	public delegate int Callback(VirtualMachine vm);

	public readonly string name;
	public readonly int typeIndex;
	public readonly Callback callback;

	public NativeFunction(string name, int typeIndex, Callback callback)
	{
		this.name = name;
		this.typeIndex = typeIndex;
		this.callback = callback;
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