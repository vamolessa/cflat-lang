public static class PepperFunctionExtensions
{
	public static void AddFunction(this Pepper self, string functionName, NativeFunction.Callback functionCallback, ValueType returnType, params ValueType[] paramTypes)
	{
		var builder = self.byteCode.BeginAddFunctionType();
		builder.returnType = returnType;
		foreach (var paramType in paramTypes)
			builder.AddParam(paramType);
		var functionTypeIndex = self.byteCode.EndAddFunctionType(builder);
		self.byteCode.nativeFunctions.PushBack(new NativeFunction(
			functionName,
			functionTypeIndex,
			functionCallback
		));
	}
}