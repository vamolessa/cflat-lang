using System.Text;

public static class VirtualMachineHelper
{
	public static void Return(VirtualMachine vm, int size)
	{
		vm.callframeStack.count -= 1;
		var stackTop = vm.callframeStack.buffer[vm.callframeStack.count].baseStackIndex - 1;

		var dstIdx = stackTop;
		var srcIdx = vm.valueStack.count - size;

		while (srcIdx < vm.valueStack.count)
		{
			vm.valueStack.buffer[dstIdx] = vm.valueStack.buffer[srcIdx];
			vm.typeStack.buffer[dstIdx++] = vm.typeStack.buffer[srcIdx++];
		}

		stackTop += size;
		vm.valueStack.count = stackTop;
		vm.typeStack.count = stackTop;
	}

	public static void ValueToString(VirtualMachine vm, int index, Option<ValueType> overrideType, StringBuilder sb)
	{
		var type = overrideType.isSome ?
			overrideType.value :
			vm.typeStack.buffer[index];

		switch (ValueTypeHelper.GetKind(type))
		{
		case ValueType.Unit:
			sb.Append("{}");
			return;
		case ValueType.Bool:
			sb.Append(vm.valueStack.buffer[index].asBool ? "true" : "false");
			return;
		case ValueType.Int:
			sb.Append(vm.valueStack.buffer[index].asInt);
			return;
		case ValueType.Float:
			sb.Append(vm.valueStack.buffer[index].asFloat);
			return;
		case ValueType.String:
			{
				var idx = vm.valueStack.buffer[index].asInt;
				sb.Append('"');
				sb.Append(vm.heap.buffer[idx]);
				sb.Append('"');
				return;
			}
		case ValueType.Function:
			{
				var idx = vm.valueStack.buffer[index].asInt;
				vm.chunk.FormatFunction(idx, sb);
				return;
			}
		case ValueType.NativeFunction:
			{
				var idx = vm.valueStack.buffer[index].asInt;
				sb.Append("native ");
				vm.chunk.FormatNativeFunction(idx, sb);
				return;
			}
		case ValueType.Struct:
			{
				var idx = ValueTypeHelper.GetIndex(type);
				var structType = vm.chunk.structTypes.buffer[idx];
				sb.Append(structType.name);
				sb.Append('{');
				for (var i = 0; i < structType.fields.length; i++)
				{
					var fieldIndex = structType.fields.index + i;
					var field = vm.chunk.structTypeFields.buffer[fieldIndex];
					sb.Append(field.name);
					sb.Append('=');
					ValueToString(vm, index + i, Option.Some(field.type), sb);
					if (i < structType.fields.length - 1)
						sb.Append(' ');
				}
				sb.Append('}');
				return;
			}
		case ValueType.Custom:
			{
				var idx = vm.valueStack.buffer[index].asInt;
				var obj = vm.heap.buffer[idx];
				sb.Append("CustomType [");
				sb.Append(obj.GetType().Name);
				sb.Append("] ");
				sb.Append(obj);
				return;
			}
		default:
			sb.Append("<invalid type '");
			sb.Append(type);
			sb.Append("'>");
			return;
		}
	}

	public static void TraceStack(VirtualMachine vm, StringBuilder sb)
	{
		sb.Append("          ");
		for (var i = 0; i < vm.valueStack.count;)
		{
			sb.Append("[");
			ValueToString(vm, i, Option.None, sb);
			i += vm.chunk.GetTypeSize(vm.typeStack.buffer[i]);
			sb.Append("]");
		}
		sb.AppendLine();
	}

	public static string TraceCallStack(VirtualMachine vm, string source)
	{
		var sb = new StringBuilder();

		sb.AppendLine("callstack:");
		for (var i = vm.callframeStack.count - 1; i >= 0; i--)
		{
			var callframe = vm.callframeStack.buffer[i];
			var sourceIndex = vm.chunk.slices.buffer[callframe.codeIndex - 1].index;

			var pos = CompilerHelper.GetLineAndColumn(source, sourceIndex, 1);
			sb.Append("[line ");
			sb.Append(pos.line);
			sb.Append("] ");

			vm.chunk.FormatFunction(callframe.functionIndex, sb);

			sb.Append(" => ");
			var line = CompilerHelper.GetLines(source, pos.line - 1, pos.line - 1);
			sb.AppendLine(line.TrimStart());
		}

		return sb.ToString();
	}

	public static string FormatError(string source, RuntimeError error, int contextSize, int tabSize)
	{
		var sb = new StringBuilder();

		var position = CompilerHelper.GetLineAndColumn(source, error.slice.index, tabSize);

		sb.Append(error.message);
		sb.Append(" (line: ");
		sb.Append(position.line);
		sb.Append(", column: ");
		sb.Append(position.column);
		sb.AppendLine(")");

		sb.Append(CompilerHelper.GetLines(
			source,
			System.Math.Max(position.line - contextSize, 0),
			System.Math.Max(position.line - 1, 0)
		));
		sb.AppendLine();
		sb.Append(' ', position.column - 1);
		sb.Append('^', error.slice.length > 0 ? error.slice.length : 1);
		sb.Append(" here\n");

		return sb.ToString();
	}
}