public static class ClefInterfaceExtensions
{
	public static Option<Function<A, R>> GetFunction<A, R>(this CFlat self, string functionName)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		if (self.compiler.compiler.errors.count > 0)
			return Option.None;

		for (var i = 1; i < self.linking.chunks.count; i++)
		{
			var chunk = self.linking.chunks.buffer[i];
			var context = new DefinitionContext(chunk);

			try
			{
				var arguments = default(A);
				context.CallFunction<A, R>(null, ref arguments);
			}
			catch (DefinitionContext.ReflectionReturn reflection)
			{
				var type = reflection.reflectionData.type;
				for (var j = 0; j < chunk.functions.count; j++)
				{
					var function = chunk.functions.buffer[j];
					if (function.name == functionName && function.typeIndex == type.index)
					{
						return Option.Some(new Function<A, R>(
							(byte)i,
							function.codeIndex,
							(ushort)j,
							chunk.functionTypes.buffer[function.typeIndex].parametersSize
						));
					}
				}
			}
			catch (Marshal.InvalidReflectionException)
			{
			}
		}

		return Option.None;
	}

	public static void AddStruct<T>(this CFlat self) where T : struct, IStruct
	{
		Marshal.ReflectOnStruct<T>(self.linking.BindingChunk);
	}

	public static void AddClass<T>(this CFlat self) where T : class
	{
		Marshal.ReflectOnClass<Class<T>>(self.linking.BindingChunk);
	}

	public static bool AddFunction(this CFlat self, NativeCallback<DefinitionContext> definitionFunction, NativeCallback<RuntimeContext> runtimeFunction)
	{
		var chunk = self.linking.BindingChunk;
		var context = new DefinitionContext(chunk);

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
				chunk.nativeFunctions.PushBack(new NativeFunction(
					definition.functionName,
					typeIndex,
					context.builder.returnType.GetSize(chunk),
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