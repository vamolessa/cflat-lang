using System.Reflection;
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
			BytesHelper.BytesToUShort(b0, b1),
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
		BytesHelper.UShortToBytes(
			index,
			out b0,
			out b1
		);

		b2 = (byte)kind;
		b3 = flags;
	}

	public byte GetSize(ByteCodeChunk chunk)
	{
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

	public void Format(ByteCodeChunk chunk, StringBuilder sb)
	{
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
		case TypeKind.NativeObject:
			sb.Append("native object");
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

	public bool IsCompatibleWithNativeType(System.Type type)
	{
		switch (kind)
		{
		case TypeKind.Unit:
			return type == typeof(void);
		case TypeKind.Bool:
			return type == typeof(bool);
		case TypeKind.Int:
			return type == typeof(int);
		case TypeKind.Float:
			return type == typeof(float);
		case TypeKind.String:
			return type == typeof(string);
		case TypeKind.NativeObject:
			return type.IsClass;
		default:
			return false;
		}
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

public readonly struct NativeCall
{
	public readonly MethodInfo methodInfo;
	public readonly ValueType returnType;
	public readonly ValueType[] argumentTypes;
	public readonly byte argumentsSize;

	public NativeCall(MethodInfo methodInfo, ValueType returnType, ValueType[] argumentTypes, byte argumentsSize)
	{
		this.methodInfo = methodInfo;
		this.returnType = returnType;
		this.argumentTypes = argumentTypes;
		this.argumentsSize = argumentsSize;
	}
}