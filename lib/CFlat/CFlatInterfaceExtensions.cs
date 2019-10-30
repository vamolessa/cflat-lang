public static class CFlatInterfaceExtensions
{
	public static Option<Function<A, R>> GetFunction<A, R>(this CFlat self, string functionName)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		if (self.compiler.compiler.errors.count > 0)
			return Option.None;

		var marshaler = new FunctionDefinitionMarshaler<A, R>(self.chunk);

		ValueType type;
		try
		{
			type = marshaler.GetDefinedType();
		}
		catch (Marshal.InvalidReflectionException)
		{
			return Option.None;
		}

		for (var i = 0; i < self.chunk.functions.count; i++)
		{
			var function = self.chunk.functions.buffer[i];
			if (function.name == functionName && function.typeIndex == type.index)
			{
				return Option.Some(new Function<A, R>(
					function.codeIndex,
					(ushort)i,
					self.chunk.functionTypes.buffer[function.typeIndex].parametersSize
				));
			}
		}

		return Option.None;
	}

	public static void AddStruct<T>(this CFlat self) where T : struct, IStruct
	{
		Marshal.TypeOf<Struct<T>>(self.chunk);
	}

	public static void AddClass<T>(this CFlat self) where T : class
	{
		Marshal.TypeOf<Class<T>>(self.chunk);
	}

	public static bool AddFunction<R>(this CFlat self, string functionName, System.Func<VirtualMachine, R> function)
		where R : struct, IMarshalable
	{
		var builder = new FunctionTypeBuilder(self.chunk);
		builder.returnType = Marshal.TypeOf<R>(self.chunk);
		return FinishAddFunction(self, builder, functionName, (vm, top) =>
		{
			FunctionInterface.Return(vm, function(vm));
		});
	}

	public static bool AddFunction<A0, R>(this CFlat self, string functionName, System.Func<VirtualMachine, A0, R> function)
		where A0 : struct, IMarshalable
		where R : struct, IMarshalable
	{
		var builder = new FunctionTypeBuilder(self.chunk);
		builder.WithParam(Marshal.TypeOf<A0>(self.chunk));
		builder.returnType = Marshal.TypeOf<R>(self.chunk);
		return FinishAddFunction(self, builder, functionName, (vm, top) =>
		{
			var functionInterface = new FunctionInterface(vm, top);
			FunctionInterface.Return(vm, function(
				vm,
				functionInterface.Arg<A0>()
			));
		});
	}

	public static bool AddFunction<A0, A1, R>(this CFlat self, string functionName, System.Func<VirtualMachine, A0, A1, R> function)
		where A0 : struct, IMarshalable
		where A1 : struct, IMarshalable
		where R : struct, IMarshalable
	{
		var builder = new FunctionTypeBuilder(self.chunk);
		builder.WithParam(Marshal.TypeOf<A0>(self.chunk));
		builder.WithParam(Marshal.TypeOf<A1>(self.chunk));
		builder.returnType = Marshal.TypeOf<R>(self.chunk);
		return FinishAddFunction(self, builder, functionName, (vm, top) =>
		{
			var functionInterface = new FunctionInterface(vm, top);
			FunctionInterface.Return(vm, function(
				vm,
				functionInterface.Arg<A0>(),
				functionInterface.Arg<A1>()
			));
		});
	}

	public static bool AddFunction<A0, A1, A2, R>(this CFlat self, string functionName, System.Func<VirtualMachine, A0, A1, A2, R> function)
		where A0 : struct, IMarshalable
		where A1 : struct, IMarshalable
		where A2 : struct, IMarshalable
		where R : struct, IMarshalable
	{
		var builder = new FunctionTypeBuilder(self.chunk);
		builder.WithParam(Marshal.TypeOf<A0>(self.chunk));
		builder.WithParam(Marshal.TypeOf<A1>(self.chunk));
		builder.WithParam(Marshal.TypeOf<A2>(self.chunk));
		builder.returnType = Marshal.TypeOf<R>(self.chunk);
		return FinishAddFunction(self, builder, functionName, (vm, top) =>
		{
			var functionInterface = new FunctionInterface(vm, top);
			FunctionInterface.Return(vm, function(
				vm,
				functionInterface.Arg<A0>(),
				functionInterface.Arg<A1>(),
				functionInterface.Arg<A2>()
			));
		});
	}

	public static bool AddFunction<A0, A1, A2, A3, R>(this CFlat self, string functionName, System.Func<VirtualMachine, A0, A1, A2, A3, R> function)
		where A0 : struct, IMarshalable
		where A1 : struct, IMarshalable
		where A2 : struct, IMarshalable
		where A3 : struct, IMarshalable
		where R : struct, IMarshalable
	{
		var builder = new FunctionTypeBuilder(self.chunk);
		builder.WithParam(Marshal.TypeOf<A0>(self.chunk));
		builder.WithParam(Marshal.TypeOf<A1>(self.chunk));
		builder.WithParam(Marshal.TypeOf<A2>(self.chunk));
		builder.WithParam(Marshal.TypeOf<A3>(self.chunk));
		builder.returnType = Marshal.TypeOf<R>(self.chunk);
		return FinishAddFunction(self, builder, functionName, (vm, top) =>
		{
			var functionInterface = new FunctionInterface(vm, top);
			FunctionInterface.Return(vm, function(
				vm,
				functionInterface.Arg<A0>(),
				functionInterface.Arg<A1>(),
				functionInterface.Arg<A2>(),
				functionInterface.Arg<A3>()
			));
		});
	}

	private static bool FinishAddFunction(CFlat self, FunctionTypeBuilder builder, string functionName, NativeFunction.Callback function)
	{
		var result = builder.Build(out var typeIndex);
		if (!self.compiler.compiler.CheckFunctionBuild(result, new Slice()))
			return false;

		self.chunk.nativeFunctions.PushBack(new NativeFunction(
			functionName,
			typeIndex,
			builder.returnType.GetSize(self.chunk),
			function
		));
		return true;
	}
}