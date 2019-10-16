public static class ClefInterfaceExtensions
{
	public static Option<Function<A, R>> GetFunction<A, R>(this CFlat self, string functionName)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		if (self.compiler.compiler.errors.count > 0)
			return Option.None;

		var context = new DefinitionContext(self.linking.byteCodeChunk);
		try
		{
			var arguments = default(A);
			context.CallFunction<A, R>(null, ref arguments);
		}
		catch (DefinitionContext.ReflectionReturn reflection)
		{
			var data = reflection.reflectionData;
			for (var i = 0; i < self.linking.byteCodeChunk.functions.count; i++)
			{
				var function = self.linking.byteCodeChunk.functions.buffer[i];
				if (function.name == functionName && function.typeIndex == data.type.index)
				{
					return Option.Some(new Function<A, R>(
						function.codeIndex,
						(ushort)i,
						self.linking.byteCodeChunk.functionTypes.buffer[function.typeIndex].parametersSize
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
		Marshal.ReflectOnStruct<T>(self.linking.byteCodeChunk);
	}

	public static void AddClass<T>(this CFlat self) where T : class
	{
		Marshal.ReflectOnClass<Class<T>>(self.linking.byteCodeChunk);
	}

	public static bool AddFunction(this CFlat self, NativeCallback<DefinitionContext> definitionFunction, NativeCallback<RuntimeContext> runtimeFunction)
	{
		var context = new DefinitionContext(self.linking.byteCodeChunk);
		try
		{
			definitionFunction(ref context);
			context.builder.Cancel();
			self.registerErrors.PushBack(new CompileError(
				new Slice(),
				"No native function body found"
			));
		}
		catch (DefinitionContext.DefinitionReturn definition)
		{
			var result = context.builder.Build(out var typeIndex);
			if (self.compiler.compiler.CheckFunctionBuild(result, new Slice()))
			{
				self.linking.byteCodeChunk.nativeFunctions.PushBack(new NativeFunction(
					definition.functionName,
					typeIndex,
					context.builder.returnType.GetSize(self.linking.byteCodeChunk),
					runtimeFunction
				));
				return true;
			}
		}
		catch (System.Exception e)
		{
			context.builder.Cancel();
			self.registerErrors.PushBack(new CompileError(
				new Slice(),
				"Error when adding native function\n" + e.Message
			));
		}

		return false;
	}
}