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
		this = default;
		asBool = value;
	}

	public ValueData(int value)
	{
		this = default;
		asInt = value;
	}

	public ValueData(float value)
	{
		this = default;
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
	NativeObject,
}

public readonly struct ValueType
{
	public readonly ushort index;
	public readonly TypeKind kind;
	public readonly byte flags;

	public static ValueType Read(byte b0, byte b1, byte b2, byte b3)
	{
		return new ValueType(
			BytesHelper.BytesToShort(b0, b1),
			(TypeKind)b2,
			b3
		);
	}

	public ValueType(TypeKind kind)
	{
		this.kind = kind;
		this.flags = 0;
		this.index = 0;
	}

	public ValueType(TypeKind kind, int index)
	{
		this.kind = kind;
		this.flags = 0;
		this.index = (ushort)index;
	}

	public ValueType(ushort index, TypeKind kind, byte flags)
	{
		this.kind = kind;
		this.flags = flags;
		this.index = index;
	}

	public bool IsEqualTo(ValueType other)
	{
		return
			kind == other.kind &&
			flags == other.flags &&
			index == other.index;
	}

	public bool IsSimple()
	{
		return flags == 0 && index == 0;
	}

	public bool IsKind(TypeKind kind)
	{
		return this.kind == kind && flags == 0 && index == 0;
	}

	public void Write(out byte b0, out byte b1, out byte b2, out byte b3)
	{
		BytesHelper.ShortToBytes(
			index,
			out b0,
			out b1
		);

		b2 = (byte)kind;
		b3 = flags;
	}

	public int GetSize(ByteCodeChunk chunk)
	{
		return kind == TypeKind.Struct ?
			chunk.structTypes.buffer[index].size :
			1;
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
		case TypeKind.NativeObject:
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
	public readonly int parametersSize;

	public FunctionType(Slice parameters, ValueType returnType, int parametersTotalSize)
	{
		this.parameters = parameters;
		this.returnType = returnType;
		this.parametersSize = parametersTotalSize;
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
	public delegate Return Callback<C>(ref C context) where C : IContext;

	public readonly string name;
	public readonly int typeIndex;
	public readonly int returnSize;
	public readonly Callback<RuntimeContext> callback;

	public NativeFunction(string name, int typeIndex, int returnSize, Callback<RuntimeContext> callback)
	{
		this.name = name;
		this.typeIndex = typeIndex;
		this.returnSize = returnSize;
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