public static class PepperRegisterExtensions
{
	public static void AddRegisterError(this Pepper self, string errorFormat, params object[] args)
	{
		self.registerErrors.PushBack(new CompileError(new Slice(), string.Format(errorFormat, args)));
	}

	public static bool AddFunction(this Pepper self, NativeFunction.Callback<DefinitionContext> definitionFunction, NativeFunction.Callback<RuntimeContext> runtimeFunction)
	{
		var context = new DefinitionContext(self.virtualMachine);
		try
		{
			definitionFunction(ref context);
			context.builder.Cancel();
			self.registerErrors.PushBack(new CompileError(
				new Slice(),
				"No native function body found"
			));
		}
		catch (DefinitionContext.Definition definition)
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
		Marshal.ReflectOnStruct<T>(self.virtualMachine);
	}
}