public interface IStruct : IMarshalable
{
}

public interface IMarshalable
{
	void Marshal<M>(ref M marshaler) where M : IMarshaler;
}

internal static class Marshal
{
	public sealed class InvalidReflectionException : System.Exception { }

	internal readonly struct ReflectionData
	{
		public readonly ValueType type;
		public readonly byte size;

		public ReflectionData(ValueType type, byte size)
		{
			this.type = type;
			this.size = size;
		}
	}

	internal static class SizeOf<T> where T : IMarshalable
	{
		public static byte size;
	}
	internal static class BasicTypeOf<T> where T : IMarshalable
	{
		public static Option<ValueType> type;

		static BasicTypeOf()
		{
			BasicTypeOf<Unit>.type = Option.Some(new ValueType(TypeKind.Unit));
			BasicTypeOf<Bool>.type = Option.Some(new ValueType(TypeKind.Bool));
			BasicTypeOf<Int>.type = Option.Some(new ValueType(TypeKind.Int));
			BasicTypeOf<Float>.type = Option.Some(new ValueType(TypeKind.Float));
			BasicTypeOf<String>.type = Option.Some(new ValueType(TypeKind.String));
			BasicTypeOf<Object>.type = Option.Some(new ValueType(TypeKind.NativeObject));

			SizeOf<Unit>.size = 1;
			SizeOf<Bool>.size = 1;
			SizeOf<Int>.size = 1;
			SizeOf<Float>.size = 1;
			SizeOf<String>.size = 1;
			SizeOf<Object>.size = 1;
		}
	}

	public static ReflectionData ReflectOn<T>(ByteCodeChunk chunk) where T : struct, IMarshalable
	{
		var type = typeof(T);

		var marshalType = Marshal.BasicTypeOf<T>.type;
		if (marshalType.isSome)
		{
			return new ReflectionData(marshalType.value, Marshal.SizeOf<T>.size);
		}
		else if (typeof(IStruct).IsAssignableFrom(type))
		{
			var marshaler = new StructDefinitionMarshaler(chunk);
			return marshaler.GetReflectionData<T>();
		}
		else if (typeof(ITuple).IsAssignableFrom(type))
		{
			var marshaler = new TupleDefinitionMarshaler(chunk);
			return marshaler.GetReflectionData<T>();
		}

		throw new InvalidReflectionException();
	}

	public static ReflectionData ReflectOnStruct<T>(ByteCodeChunk chunk) where T : struct, IStruct
	{
		var marshaler = new StructDefinitionMarshaler(chunk);
		return marshaler.GetReflectionData<T>();
	}

	public static ReflectionData ReflectOnTuple<T>(ByteCodeChunk chunk) where T : struct, ITuple
	{
		var marshaler = new TupleDefinitionMarshaler(chunk);
		return marshaler.GetReflectionData<T>();
	}

	public static object GetObject(ref ReadMarshaler marshaler, ValueType type)
	{
		if (type.IsKind(TypeKind.Bool))
		{
			var v = default(Bool);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.Int))
		{
			var v = default(Int);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.Float))
		{
			var v = default(Float);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.String))
		{
			var v = default(String);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else if (type.IsKind(TypeKind.NativeObject))
		{
			var v = default(Object);
			v.Marshal(ref marshaler);
			return v.value;
		}
		else
		{
			return null;
		}
	}
}

public interface IMarshaler
{
	void Marshal(string name);
	void Marshal(ref bool value, string name);
	void Marshal(ref int value, string name);
	void Marshal(ref float value, string name);
	void Marshal(ref string value, string name);
	void Marshal<T>(ref T value, string name) where T : struct, IMarshalable;
	void Marshal(ref object value, string name);
}

internal interface IDefinitionMarshaler : IMarshaler
{
	Marshal.ReflectionData GetReflectionData<T>() where T : struct, IMarshalable;
}

internal struct ReadMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public ReadMarshaler(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public void Marshal(string name) => stackIndex++;
	public void Marshal(ref bool value, string name) => value = vm.valueStack.buffer[stackIndex++].asBool;
	public void Marshal(ref int value, string name) => value = vm.valueStack.buffer[stackIndex++].asInt;
	public void Marshal(ref float value, string name) => value = vm.valueStack.buffer[stackIndex++].asFloat;
	public void Marshal(ref string value, string name) => value = vm.nativeObjects.buffer[vm.valueStack.buffer[stackIndex++].asInt] as string;
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable
	{
		value = default;
		value.Marshal(ref this);
	}
	public void Marshal(ref object value, string name) => value = vm.nativeObjects.buffer[vm.valueStack.buffer[stackIndex++].asInt];
}

internal struct WriteMarshaler : IMarshaler
{
	private VirtualMachine vm;
	private int stackIndex;

	public WriteMarshaler(VirtualMachine vm, int stackIndex)
	{
		this.vm = vm;
		this.stackIndex = stackIndex;
	}

	public void Marshal(string name) => stackIndex++;
	public void Marshal(ref bool value, string name) => vm.valueStack.buffer[stackIndex++].asBool = value;
	public void Marshal(ref int value, string name) => vm.valueStack.buffer[stackIndex++].asInt = value;
	public void Marshal(ref float value, string name) => vm.valueStack.buffer[stackIndex++].asFloat = value;
	public void Marshal(ref string value, string name)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.nativeObjects.count;
		vm.nativeObjects.PushBack(value);
	}
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => value.Marshal(ref this);
	public void Marshal(ref object value, string name)
	{
		vm.valueStack.buffer[stackIndex++].asInt = vm.nativeObjects.count;
		vm.nativeObjects.PushBack(value);
	}
}

internal struct TupleDefinitionMarshaler : IDefinitionMarshaler
{
	internal ByteCodeChunk chunk;
	internal TupleTypeBuilder builder;

	public TupleDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginTupleType();
	}

	public void Marshal(string name) => builder.WithElement(new ValueType(TypeKind.Unit));
	public void Marshal(ref bool value, string name) => builder.WithElement(new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithElement(new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithElement(new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithElement(new ValueType(TypeKind.String));
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithElement(global::Marshal.ReflectOn<T>(chunk).type);
	public void Marshal(ref object value, string name) => builder.WithElement(new ValueType(TypeKind.NativeObject));

	public Marshal.ReflectionData GetReflectionData<T>() where T : struct, IMarshalable
	{
		default(T).Marshal(ref this);
		var result = builder.Build(out var typeIndex);
		if (result == TupleTypeBuilder.Result.Success)
		{
			global::Marshal.SizeOf<T>.size = chunk.tupleTypes.buffer[typeIndex].size;
			return new Marshal.ReflectionData(
				new ValueType(TypeKind.Tuple, typeIndex),
				global::Marshal.SizeOf<T>.size
			);
		}

		throw new Marshal.InvalidReflectionException();
	}
}

internal struct StructDefinitionMarshaler : IDefinitionMarshaler
{
	internal ByteCodeChunk chunk;
	internal StructTypeBuilder builder;

	public StructDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginStructType();
	}

	public void Marshal(string name) => builder.WithField(name, new ValueType(TypeKind.Unit));
	public void Marshal(ref bool value, string name) => builder.WithField(name, new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithField(name, new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithField(name, new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithField(name, new ValueType(TypeKind.String));
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithField(name, global::Marshal.ReflectOn<T>(chunk).type);
	public void Marshal(ref object value, string name) => builder.WithField(name, new ValueType(TypeKind.NativeObject));

	public Marshal.ReflectionData GetReflectionData<T>() where T : struct, IMarshalable
	{
		default(T).Marshal(ref this);
		var result = builder.Build(typeof(T).Name, out var typeIndex);
		if (
			result == StructTypeBuilder.Result.Success ||
			(
				result == StructTypeBuilder.Result.DuplicatedName &&
				global::Marshal.SizeOf<T>.size > 0
			)
		)
		{
			global::Marshal.SizeOf<T>.size = chunk.structTypes.buffer[typeIndex].size;
			return new Marshal.ReflectionData(
				new ValueType(TypeKind.Struct, typeIndex),
				global::Marshal.SizeOf<T>.size
			);
		}

		throw new Marshal.InvalidReflectionException();
	}
}

internal struct FunctionDefinitionMarshaler : IDefinitionMarshaler
{
	internal ByteCodeChunk chunk;
	internal FunctionTypeBuilder builder;

	public FunctionDefinitionMarshaler(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		this.builder = chunk.BeginFunctionType();
	}

	public void Marshal(string name) => builder.WithParam(new ValueType(TypeKind.Unit));
	public void Marshal(ref bool value, string name) => builder.WithParam(new ValueType(TypeKind.Bool));
	public void Marshal(ref int value, string name) => builder.WithParam(new ValueType(TypeKind.Int));
	public void Marshal(ref float value, string name) => builder.WithParam(new ValueType(TypeKind.Float));
	public void Marshal(ref string value, string name) => builder.WithParam(new ValueType(TypeKind.String));
	public void Marshal<T>(ref T value, string name) where T : struct, IMarshalable => builder.WithParam(global::Marshal.ReflectOn<T>(chunk).type);
	public void Marshal(ref object value, string name) => builder.WithParam(new ValueType(TypeKind.NativeObject));

	public void Returns<T>() where T : struct, IMarshalable
	{
		builder.returnType = global::Marshal.ReflectOn<T>(chunk).type;
	}

	public Marshal.ReflectionData GetReflectionData<T>() where T : struct, IMarshalable
	{
		default(T).Marshal(ref this);
		var result = builder.Build(out var typeIndex);
		if (result == FunctionTypeBuilder.Result.Success)
		{
			return new Marshal.ReflectionData(
				new ValueType(TypeKind.Function, typeIndex),
				chunk.functionTypes.buffer[typeIndex].parametersSize
			);
		}

		throw new Marshal.InvalidReflectionException();
	}
}
