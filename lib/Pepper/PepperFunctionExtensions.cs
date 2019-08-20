public static class PepperFunctionExtensions
{
	public static bool AddFunction(this Pepper self, NativeFunction.Callback<DefinitionContext> definitionFunction, NativeFunction.Callback<RuntimeContext> runtimeFunction)
	{
		var context = new DefinitionContext(self.byteCode);
		try
		{
			definitionFunction(ref context);
			context.builder.Cancel();
			self.errors.PushBack(new RuntimeError(
				0,
				new Slice(),
				"No native function body found"
			));
			return false;
		}
		catch (DefinitionContext.Definition definition)
		{
			var functionTypeIndex = context.builder.Build();
			self.byteCode.nativeFunctions.PushBack(new NativeFunction(
				definition.functionName,
				functionTypeIndex,
				context.builder.returnType.GetSize(self.byteCode),
				runtimeFunction
			));
			return true;
		}
		catch
		{
			context.builder.Cancel();
			self.errors.PushBack(new RuntimeError(
				0,
				new Slice(),
				"Could not add native function"
			));
			return false;
		}
	}

	public static void AddFunction(this Pepper self, string functionName, NativeFunction.Callback<RuntimeContext> functionCallback, ValueType returnType, params ValueType[] paramTypes)
	{
		var builder = self.byteCode.BeginFunctionType();
		builder.returnType = returnType;
		foreach (var paramType in paramTypes)
			builder.WithParam(paramType);
		var functionTypeIndex = builder.Build();
		self.byteCode.nativeFunctions.PushBack(new NativeFunction(
			functionName,
			functionTypeIndex,
			returnType.GetSize(self.byteCode),
			functionCallback
		));
	}
}