using System.Globalization;
using System.Text;

namespace cflat
{
	internal static class VirtualMachineHelper
	{
		public static void ValueToString(VirtualMachine vm, int index, ValueType type, StringBuilder sb)
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
					sb.Append(vm.nativeObjects.buffer[idx]);
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
				{
					if (type.index >= vm.chunk.tupleTypes.count)
					{
						sb.Append("<!>");
						return;
					}

					var tupleType = vm.chunk.tupleTypes.buffer[type.index];
					sb.Append('{');
					var offset = 0;
					for (var i = 0; i < tupleType.elements.length; i++)
					{
						var elementIndex = tupleType.elements.index + i;
						var elementType = vm.chunk.tupleElementTypes.buffer[elementIndex];
						ValueToString(vm, index + offset, elementType, sb);
						if (i < tupleType.elements.length - 1)
							sb.Append(',');

						offset += elementType.GetSize(vm.chunk);
					}
					sb.Append('}');
					return;
				}
			case TypeKind.Struct:
				{
					if (type.index >= vm.chunk.structTypes.count)
					{
						sb.Append("<!>");
						return;
					}

					var structType = vm.chunk.structTypes.buffer[type.index];
					sb.Append(structType.name);
					sb.Append('{');
					var offset = 0;
					for (var i = 0; i < structType.fields.length; i++)
					{
						var fieldIndex = structType.fields.index + i;
						var field = vm.chunk.structTypeFields.buffer[fieldIndex];
						sb.Append(field.name);
						sb.Append('=');
						ValueToString(vm, index + offset, field.type, sb);
						if (i < structType.fields.length - 1)
							sb.Append(',');

						offset += field.type.GetSize(vm.chunk);
					}
					sb.Append('}');
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
				sb.Append("<invalid type '");
				sb.Append(type);
				sb.Append("'>");
				return;
			}
		}

		public static void TraceStack(VirtualMachine vm, StringBuilder sb)
		{
			sb.Append("          ");

			var valueIndex = 0;
			var typeIndex = 0;
			while (valueIndex < vm.memory.stackCount)
			{
				sb.Append("[");
				if (typeIndex < vm.debugData.stackTypes.count)
				{
					var type = vm.debugData.stackTypes.buffer[typeIndex++];
					ValueToString(vm, valueIndex, type, sb);
					valueIndex += type.GetSize(vm.chunk);
				}
				else
				{
					sb.Append("?");
					valueIndex += 1;
				}
				sb.Append("]");
			}

			for (var i = typeIndex; i < vm.debugData.stackTypes.count; i++)
				sb.Append("[+]");

			sb.AppendLine();
		}
	}
}