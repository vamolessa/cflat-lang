public static class PepperInterfaceExtensions
{
	public static Option<Function<A, R>> GetFunction<A, R>(this Pepper self, string functionName)
		where A : struct, ITuple
		where R : struct, IMarshalable
	{
		var context = new DefinitionContext(self.byteCode);
		try
		{
			var arguments = default(A);
			context.CallFunction<A, R>(null, ref arguments);
		}
		catch (DefinitionContext.ReflectionReturn reflection)
		{
			var data = reflection.reflectionData;
			for (var i = 0; i < self.byteCode.functions.count; i++)
			{
				var function = self.byteCode.functions.buffer[i];
				if (function.name == functionName && function.typeIndex == data.type.index)
				{
					return Option.Some(new Function<A, R>(
						function.codeIndex,
						(ushort)i,
						self.byteCode.functionTypes.buffer[function.typeIndex].parametersSize
					));
				}
			}
		}
		catch (Marshal.InvalidReflectionException)
		{
		}

		return Option.None;
	}

	public static void AddRegisterError(this Pepper self, string errorFormat, params object[] args)
	{
		self.registerErrors.PushBack(new CompileError(new Slice(), string.Format(errorFormat, args)));
	}

	public static bool AddFunction(this Pepper self, NativeCallback<DefinitionContext> definitionFunction, NativeCallback<RuntimeContext> runtimeFunction)
	{
		var context = new DefinitionContext(self.byteCode);
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
				self.byteCode.nativeFunctions.PushBack(new NativeFunction(
						definition.functionName,
						typeIndex,
						context.builder.returnType.GetSize(self.byteCode),
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

	public static void AddStruct<T>(this Pepper self) where T : struct, IStruct
	{
		Marshal.ReflectOnStruct<T>(self.byteCode);
	}
}