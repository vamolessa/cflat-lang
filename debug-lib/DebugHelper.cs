using System.Globalization;
using System.Text;

namespace cflat.debug
{
	internal static class DebugHelper
	{
		public static void WriteValue(VirtualMachine vm, int memoryIndex, string name, ValueType type, StringBuilder sb, JsonWriter.ObjectScope writer)
		{
			writer.String("name", name);

			sb.Clear();
			type.Format(vm.chunk, sb);
			writer.String("type", sb.ToString());

			sb.Clear();
			DebugHelper.ValueToString(
				vm,
				memoryIndex,
				type,
				sb
			);
			writer.String("value", sb.ToString());
		}

		private static void ValueToString(VirtualMachine vm, int index, ValueType type, StringBuilder sb)
		{
			if (type.IsReference)
			{
				sb.Append(type.IsMutable ? "&mut " : "&");
				sb.Append(vm.memory.values[index].asInt);
				return;
			}

			if (type.IsArray)
			{
				var heapStartIndex = vm.memory.values[index].asInt;
				if (heapStartIndex < vm.memory.heapStart || heapStartIndex >= vm.memory.values.Length)
				{
					sb.Append("<!>");
					return;
				}

				sb.Append('[');
				type.ToArrayElementType().Format(vm.chunk, sb);
				sb.Append(':');
				var arrayLength = vm.memory.values[heapStartIndex - 1].asInt;
				sb.Append(arrayLength);
				sb.Append(']');
				return;
			}

			switch (type.kind)
			{
			case TypeKind.Unit:
				sb.Append("{}");
				return;
			case TypeKind.Bool:
				sb.Append(vm.memory.values[index].asBool ? "true" : "false");
				return;
			case TypeKind.Int:
				sb.Append(vm.memory.values[index].asInt);
				return;
			case TypeKind.Float:
				sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", vm.memory.values[index].asFloat);
				return;
			case TypeKind.String:
				{
					var idx = vm.memory.values[index].asInt;
					if (idx >= vm.nativeObjects.count)
					{
						sb.Append("<!>");
						return;
					}

					sb.Append('"');
					sb.Append(vm.nativeObjects.buffer[idx] as string);
					sb.Append('"');
					return;
				}
			case TypeKind.Function:
				{
					var idx = vm.memory.values[index].asInt;
					vm.chunk.FormatFunction(idx, sb);
					return;
				}
			case TypeKind.NativeFunction:
				{
					var idx = vm.memory.values[index].asInt;
					sb.Append("native ");
					vm.chunk.FormatNativeFunction(idx, sb);
					return;
				}
			case TypeKind.Tuple:
			case TypeKind.Struct:
				{
					type.Format(vm.chunk, sb);
					return;
				}
			case TypeKind.NativeClass:
				{
					var idx = vm.memory.values[index].asInt;
					if (idx >= vm.nativeObjects.count)
					{
						sb.Append("<!>");
						return;
					}

					var obj = vm.nativeObjects.buffer[idx];
					sb.Append("native [");
					sb.Append(obj != null ? obj.GetType().Name : "null");
					sb.Append("] ");
					sb.Append(obj);
					return;
				}
			default:
				sb.Append("<!>");
				return;
			}
		}
	}
}