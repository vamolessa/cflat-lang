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
	Unit = 0b0000,
	Bool,
	Int,
	Float,
	String,
	Function,
	NativeFunction,
	Tuple,
	Struct,
	NativeClass,
}

[System.Flags]
public enum TypeFlags : byte
{
	None = 0b0000 << 4,
	Array = 0b0001 << 4,
	Reference = 0b0010 << 4,
	MutableReference = 0b0100 << 4,
}

public readonly struct ValueType
{
	public readonly byte chunkIndex;
	private readonly byte kindAndFlags;
	public readonly ushort index;

	public TypeKind Kind
	{
		get { return (TypeKind)(kindAndFlags & 0b00001111); }
	}

	public TypeFlags Flags
	{
		get { return (TypeFlags)(kindAndFlags & 0b11110000); }
	}

	public bool IsSimple
	{
		get { return index == 0 && Flags == TypeFlags.None; }
	}

	public bool IsArray
	{
		get { return (Flags & TypeFlags.Array) != 0; }
	}

	public static ValueType Read(byte b0, byte b1, byte b2, byte b3)
	{
		return new ValueType(
			b0,
			b1,
			BytesHelper.BytesToUShort(b2, b3)
		);
	}

	public ValueType(TypeKind kind)
	{
		this.chunkIndex = 0;
		this.kindAndFlags = (byte)kind;
		this.index = 0;
	}

	public ValueType(TypeKind kind, int index)
	{
		this.chunkIndex = 0;
		this.kindAndFlags = (byte)kind;
		this.index = (ushort)index;
	}

	public ValueType(TypeKind kind, TypeFlags flags, ushort index)
	{
		this.chunkIndex = 0;
		this.kindAndFlags = (byte)((byte)kind | (byte)flags);
		this.index = index;
	}

	public ValueType(byte chunkIndex, byte metadata, ushort index)
	{
		this.chunkIndex = chunkIndex;
		this.kindAndFlags = metadata;
		this.index = index;
	}

	public bool IsEqualTo(ValueType other)
	{
		return
			index == other.index &&
			chunkIndex == other.chunkIndex &&
			kindAndFlags == other.kindAndFlags;
	}

	public bool Accepts(ValueType other)
	{
		return IsEqualTo(other);
	}

	public bool IsKind(TypeKind kind)
	{
		return this.Kind == kind && Flags == 0 && index == 0;
	}

	public void Write(out byte b0, out byte b1, out byte b2, out byte b3)
	{
		b0 = chunkIndex;
		b1 = kindAndFlags;

		BytesHelper.UShortToBytes(
			index,
			out b2,
			out b3
		);
	}

	public byte GetSize(ByteCodeChunk chunk)
	{
		if (IsArray)
			return 1;

		switch (Kind)
		{
		case TypeKind.Tuple:
			return chunk.tupleTypes.buffer[index].size;
		case TypeKind.Struct:
			return chunk.structTypes.buffer[index].size;
		default:
			return 1;
		}
	}

	public ValueType ToArrayElementType()
	{
		return new ValueType(Kind, index);
	}

	public ValueType ToArrayType()
	{
		return new ValueType(Kind, Flags | TypeFlags.Array, index);
	}

	public void Format(ByteCodeChunk chunk, StringBuilder sb)
	{
		if (IsArray)
		{
			sb.Append('[');
			ToArrayElementType().Format(chunk, sb);
			sb.Append(']');
			return;
		}

		switch (Kind)
		{
		case TypeKind.Unit:
			sb.Append("{}");
			break;
		case TypeKind.Bool:
			sb.Append("bool");
			break;
		case TypeKind.Int:
			sb.Append("int");
			break;
		case TypeKind.Float:
			sb.Append("float");
			break;
		case TypeKind.String:
			sb.Append("string");
			break;
		case TypeKind.Function:
			chunk.FormatFunctionType(index, sb);
			break;
		case TypeKind.NativeFunction:
			sb.Append("native ");
			chunk.FormatFunctionType(index, sb);
			break;
		case TypeKind.Tuple:
			chunk.FormatTupleType(index, sb);
			break;
		case TypeKind.Struct:
			chunk.FormatStructType(index, sb);
			break;
		case TypeKind.NativeClass:
			sb.Append("native ");
			sb.Append(chunk.nativeClassTypes.buffer[index].name);
			break;
		default:
			sb.Append("unreachable");
			break;
		}
	}

	public string ToString(ByteCodeChunk chunk)
	{
		var sb = new StringBuilder();
		Format(chunk, sb);
		return sb.ToString();
	}
}

public readonly struct FunctionType
{
	public readonly Slice parameters;
	public readonly ValueType returnType;
	public readonly byte parametersSize;

	public FunctionType(Slice parameters, ValueType returnType, byte parametersTotalSize)
	{
		this.parameters = parameters;
		this.returnType = returnType;
		this.parametersSize = parametersTotalSize;
	}
}

public readonly struct Function
{
	public readonly string name;
	public readonly int codeIndex;
	public readonly ushort typeIndex;

	public Function(string name, int codeIndex, ushort typeIndex)
	{
		this.name = name;
		this.codeIndex = codeIndex;
		this.typeIndex = typeIndex;
	}
}

public delegate Return NativeCallback<C>(ref C context) where C : IContext;

public readonly struct NativeFunction
{
	public readonly string name;
	public readonly ushort typeIndex;
	public readonly byte returnSize;
	public readonly NativeCallback<RuntimeContext> callback;

	public NativeFunction(string name, ushort typeIndex, byte returnSize, NativeCallback<RuntimeContext> callback)
	{
		this.name = name;
		this.returnSize = returnSize;
		this.typeIndex = typeIndex;
		this.callback = callback;
	}
}

public readonly struct TupleType
{
	public readonly Slice elements;
	public readonly byte size;

	public TupleType(Slice elements, byte size)
	{
		this.elements = elements;
		this.size = size;
	}
}

public readonly struct StructType
{
	public readonly string name;
	public readonly Slice fields;
	public readonly byte size;

	public StructType(string name, Slice fields, byte size)
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

public readonly struct NativeClassType
{
	public readonly string name;
	public readonly System.Type type;

	public NativeClassType(string name, System.Type type)
	{
		this.name = name;
		this.type = type;
	}
}