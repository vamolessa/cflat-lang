public static class ClefInterfaceExtensions
{
	public static Option<Function<A, R>> GetFunction<A, R>(this CFlat self, string functionName)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		if (self.compiler.compiler.errors.count > 0)
			return Option.None;

		var context = new DefinitionContext(self.chunk);
		try
		{
			var arguments = default(A);
			context.CallFunction<A, R>(null, ref arguments);
		}
		catch (DefinitionContext.TypeReturn ret)
		{
			var type = ret.type;
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
		}
		catch (Marshal.InvalidReflectionException)
		{
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

	public static bool AddFunction(this CFlat self, NativeCallback<DefinitionContext> definitionFunction, NativeCallback<RuntimeContext> runtimeFunction)
	{
		var context = new DefinitionContext(self.chunk);
		try
		{
			definitionFunction(ref context);
			context.builder.Cancel();
			self.compileErrors.PushBack(new CompileError(
				new Slice(),
				"No native function body found"
			));
		}
		catch (DefinitionContext.DefinitionReturn definition)
		{
			var result = context.builder.Build(out var typeIndex);
			if (self.compiler.compiler.CheckFunctionBuild(result, new Slice()))
			{
				self.chunk.nativeFunctions.PushBack(new NativeFunction(
					definition.functionName,
					typeIndex,
					context.builder.returnType.GetSize(self.chunk),
					runtimeFunction
				));
				return true;
			}
		}
		catch (System.Exception e)
		{
			context.builder.Cancel();
			self.compileErrors.PushBack(new CompileError(
				new Slice(),
				"Error when adding native function\n" + e.Message
			));
		}

		return false;
	}
}