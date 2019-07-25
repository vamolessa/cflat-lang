using System.Runtime.InteropServices;

public readonly struct Value
{
	public enum Type
	{
		Nil,
		Boolean,
		IntegerNumber,
		RealNumber,
	}

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct Data
	{
		[FieldOffset(0)]
		public readonly bool asBool;
		[FieldOffset(0)]
		public readonly int asInt;
		[FieldOffset(0)]
		public readonly float asFloat;

		public Data(bool value)
		{
			this = default(Data);
			asBool = value;
		}

		public Data(int value)
		{
			this = default(Data);
			asInt = value;
		}

		public Data(float value)
		{
			this = default(Data);
			asFloat = value;
		}
	}

	public readonly Type type;
	public readonly Data data;

	public Value(bool value)
	{
		type = Type.Boolean;
		data = new Data(value);
	}

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
		case Type.Nil:
			return "nil";
		case Type.Boolean:
			return data.asBool ? "true" : "false";
		case Type.IntegerNumber:
			return data.asInt.ToString();
		case Type.RealNumber:
			return data.asFloat.ToString();
		default:
			return "<invalid value>";
		}
	}
}