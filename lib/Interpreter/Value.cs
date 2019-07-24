using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
public readonly struct Value
{
	[FieldOffset(0)]
	public readonly int asInteger;
	[FieldOffset(0)]
	public readonly float asFloat;

	public override string ToString()
	{
		return asInteger.ToString();
	}
}