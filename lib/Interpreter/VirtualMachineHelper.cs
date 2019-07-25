using System.Text;

internal static class VirtualMachineHelper
{
	public static void TraceStack(VirtualMachine vm, StringBuilder sb)
	{
		sb.Append("                 ");
		for (var i = 0; i < vm.stack.count; i++)
		{
			sb.Append("[");
			sb.Append(vm.stack.buffer[i].ToString());
			sb.Append("]");
		}
		sb.AppendLine();
	}

	public static Value ReadConstant(VirtualMachine vm)
	{
		var index = vm.chunk.bytes.buffer[vm.programCount++];
		return vm.chunk.constants.buffer[index];
	}
}