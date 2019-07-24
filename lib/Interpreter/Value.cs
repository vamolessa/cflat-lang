using System.Runtime.InteropServices;

public readonly struct Value
{
	public enum Type
	{
		IntegerNumber,
		RealNumber,
	}

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct Data
	{
		[FieldOffset(0)]
		public readonly int asInteger;
		[FieldOffset(0)]
		public readonly float asFloat;

		public Data(int value)
		{
			asFloat = 0.0f;
			asInteger = value;
		}

		public Data(float value)
		{
			asInteger = 0;
			asFloat = value;
		}
	}

	public readonly Type type;
	public readonly Data data;

	public Value(int value)
	{
		type = Type.IntegerNumber;
		data = new Data(value);
	}

	public Value(float value)
	{
		type = Type.IntegerNumber;
		data = new Data(value);
	}

	public override string ToString()
	{
		switch (type)
		{
		case Type.IntegerNumber:
			return data.asInteger.ToString();
		case Type.RealNumber:
			return data.asFloat.ToString();
		default:
			return "<invalid value>";
		}
	}
}