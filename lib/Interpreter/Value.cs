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
	Tuple,
	Struct,
	NativeClass,
}

[System.Flags]
public enum TypeFlags : byte
{
	None = 0b0000,
	Array = 0b0001,
	Reference = 0b0010,
	MutableReference = 0b0100,
}

public readonly struct ValueType
{
	public readonly ChunkId chunkId;
	private readonly TypeKind kind;
	private readonly TypeFlags flags;
	public readonly ushort index;

	public bool IsSimple
	{
		get { return index == 0 && flags == TypeFlags.None; }
	}

	public bool IsArray
	{
		get { return (flags & TypeFlags.Array) != 0; }
	}

	public static ValueType Read(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7)
	{
		return new ValueType(
			new ChunkId(BytesHelper.BytesToUInt(b0, b1, b2, b3)),
			(TypeKind)b4,
			(TypeFlags)b5,
			BytesHelper.BytesToUShort(b6, b6)
		);
	}

	public ValueType(TypeKind kind)
	{
		this.chunkId = new ChunkId();
		this.kind = kind;
		this.flags = TypeFlags.None;
		this.index = 0;
	}

	public ValueType(ChunkId chunkId, TypeKind kind, int index)
	{
		this.chunkId = chunkId;
		this.kind = kind;
		this.flags = TypeFlags.None;
		this.index = (ushort)index;
	}

	public ValueType(ChunkId chunkId, TypeKind kind, TypeFlags flags, ushort index)
	{
		this.chunkId = chunkId;
		this.kind = kind;
		this.flags = flags;
		this.index = (ushort)index;
	}

	public bool IsEqualTo(ValueType other)
	{
		return
			chunkId.IsEqualTo(other.chunkId) &&
			index == other.index &&
			kind == other.kind &&
			flags == other.flags;
	}

	public bool Accepts(ValueType other)
	{
		return IsEqualTo(other);
	}

	public bool IsKind(TypeKind kind)
	{
		return this.kind == kind && flags == 0 && index == 0;
	}

	public void Write(out byte b0, out byte b1, out byte b2, out byte b3, out byte b4, out byte b5, out byte b6, out byte b7)
	{
		BytesHelper.UIntToBytes(
			chunkId.raw,
			out b0,
			out b1,
			out b2,
			out b3
		);

		b4 = (byte)kind;
		b5 = (byte)flags;

		BytesHelper.UShortToBytes(
			index,
			out b6,
			out b7
		);
	}

	public byte GetSize(ByteCodeChunk chunk)
	{
		if (IsArray)
			return 1;

		switch (kind)
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
		return new ValueType(chunkId, kind, flags & ~TypeFlags.Array, index);
	}

	public ValueType ToArrayType()
	{
		return new ValueType(chunkId, kind, flags | TypeFlags.Array, index);
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

		switch (kind)
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