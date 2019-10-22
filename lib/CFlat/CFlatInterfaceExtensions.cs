public static class ClefInterfaceExtensions
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
		return FinishAddFunction(self, builder, functionName, vm =>
		{
			var stackTop = vm.callframeStack.buffer[vm.callframeStack.count - 1].baseStackIndex;
			var ret = function(vm);
			Runtime.Return(vm, ret);
		});
	}

	public static bool AddFunction<A0, R>(this CFlat self, string functionName, System.Func<VirtualMachine, A0, R> function)
		where A0 : struct, IMarshalable
		where R : struct, IMarshalable
	{
		var builder = new FunctionTypeBuilder(self.chunk);
		builder.WithParam(Marshal.TypeOf<A0>(self.chunk));
		builder.returnType = Marshal.TypeOf<R>(self.chunk);
		return FinishAddFunction(self, builder, functionName, vm =>
		{
			var stackTop = vm.callframeStack.buffer[vm.callframeStack.count - 1].baseStackIndex;
			var reader = new MemoryReadMarshaler(vm, stackTop);
			var ret = function(
				vm,
				Runtime.Arg<A0>(ref reader)
			);
			Runtime.Return(vm, ret);
		});
	}

	private static bool FinishAddFunction(CFlat self, FunctionTypeBuilder builder, string functionName, NativeFunction.Callback function)
	{
		var result = builder.Build(out var typeIndex);
		if (self.compiler.compiler.CheckFunctionBuild(result, new Slice()))
		{
			self.chunk.nativeFunctions.PushBack(new NativeFunction(
				functionName,
				typeIndex,
				builder.returnType.GetSize(self.chunk),
				function
			));
			return true;
		}

		return false;
	}
}