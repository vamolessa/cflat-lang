public struct Point : IMarshalable
{
	public int x;
	public int y;
	public int z;

	public void Read(VirtualMachine vm, ref int index)
	{
		x = Marshaler.ReadInt(vm, ref index);
		y = Marshaler.ReadInt(vm, ref index);
		z = Marshaler.ReadInt(vm, ref index);
	}
}

public interface IMarshalable
{
	void Read(VirtualMachine vm, ref int index);
}

public static class Marshaler
{
	public static bool ReadBool(VirtualMachine vm, ref int index)
	{
		return vm.valueStack.buffer[index++].asBool;
	}

	public static int ReadInt(VirtualMachine vm, ref int index)
	{
		return vm.valueStack.buffer[index++].asInt;
	}

	public static float ReadFloat(VirtualMachine vm, ref int index)
	{
		return vm.valueStack.buffer[index++].asFloat;
	}

	public static string ReadString(VirtualMachine vm, ref int index)
	{
		return vm.heap.buffer[vm.valueStack.buffer[index++].asInt] as string;
	}

	public static T ReadStruct<T>(VirtualMachine vm, ref int index) where T : struct, IMarshalable
	{
		var value = new T();
		value.Read(vm, ref index);
		return value;
	}
}