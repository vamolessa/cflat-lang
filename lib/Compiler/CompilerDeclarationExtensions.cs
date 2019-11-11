internal static class CompilerDeclarationExtensions
{
	// VARIABLES
	public static int AddLocalVariable(this Compiler self, Slice slice, ValueType type, VariableFlags flags)
	{
		byte stackIndex = 0;
		if (self.localVariables.count > 0)
		{
			var lastVar = self.localVariables.buffer[self.localVariables.count - 1];
			stackIndex = (byte)(lastVar.stackIndex + lastVar.type.GetSize(self.chunk));
		}

		if (self.parser.tokenizer.source[slice.index] == '_')
			flags |= VariableFlags.Used | VariableFlags.Changed;

		self.localVariables.PushBack(new LocalVariable(
			slice,
			stackIndex,
			self.scopeDepth,
			type,
			flags
		));

		return self.localVariables.count - 1;
	}

	public static int DeclareLocalVariable(this Compiler self, CompilerController.ExpressionResult expression, bool mutable)
	{
		if (self.localVariables.count >= self.localVariables.buffer.Length)
		{
			self.AddSoftError(expression.slice, "Too many local variables in function");
			return -1;
		}

		return self.AddLocalVariable(expression.slice, expression.type, mutable ? VariableFlags.Mutable : VariableFlags.None);
	}

	public static bool ResolveToLocalVariableIndex(this Compiler self, Slice slice, out int index)
	{
		var source = self.parser.tokenizer.source;

		for (var i = self.localVariables.count - 1; i >= 0; i--)
		{
			var local = self.localVariables.buffer[i];
			if (CompilerHelper.AreEqual(source, slice, local.slice))
			{
				index = i;
				return true;
			}
		}

		if (self.loopNesting.count > 0 || CompilerHelper.AreEqual(source, slice, "it"))
		{
			for (var i = self.localVariables.count - 1; i >= 0; i--)
			{
				if (self.localVariables.buffer[i].IsIteration)
				{
					index = i;
					return true;
				}
			}
		}

		index = 0;
		return false;
	}

	// FUNCTIONS
	public static FunctionTypeBuilder BeginFunctionDeclaration(this Compiler self)
	{
		return self.chunk.BeginFunctionType();
	}

	public static int EndFunctionDeclaration(this Compiler self, FunctionTypeBuilder builder, Slice slice, bool hasBody)
	{
		var name = CompilerHelper.GetSlice(self, slice);
		var result = builder.Build(out var index);
		var functionIndex = -1;

		if (self.CheckFunctionBuild(result, slice))
		{
			switch (self.chunk.AddFunction(name, index, hasBody, slice, out functionIndex))
			{
			case ByteCodeChunk.AddFunctionResult.AlreadyDefined:
				self.AddSoftError(slice, "There's already a function named '{0}'", name);
				break;
			case ByteCodeChunk.AddFunctionResult.TypeMismatch:
				if (self.ResolveToFunctionIndex(slice, out int prototypeIndex))
				{
					var typeIndex = self.chunk.functions.buffer[prototypeIndex].typeIndex;
					var prototypeType = new ValueType(TypeKind.Function, typeIndex);
					var functionType = new ValueType(TypeKind.Function, index);

					self.AddSoftError(
						slice,
						"Type mismatch between function '{0}' prototype and its body. Expected {1}. Got {2}",
						name,
						prototypeType.ToString(self.chunk),
						functionType.ToString(self.chunk)
					);
				}
				else
				{
					self.AddSoftError(slice, "Type mismatch between function '{0}' prototype and its body", name);
				}
				break;
			default:
				break;
			}
		}

		if (functionIndex < 0)
		{
			functionIndex = self.chunk.functions.count;
			if (self.chunk.functionTypes.count < ushort.MaxValue)
				self.chunk.functionTypes.PushBack(new FunctionType(new Slice(), new ValueType(TypeKind.Unit), 0));
			var typeIndex = self.chunk.functionTypes.count - 1;

			self.chunk.functions.PushBack(new Function(name, -slice.index, (ushort)typeIndex));
		}

		return functionIndex;
	}

	public static bool ResolveToFunctionIndex(this Compiler self, Slice slice, out int index)
	{
		var source = self.parser.tokenizer.source;

		for (var i = 0; i < self.chunk.functions.count; i++)
		{
			var f = self.chunk.functions.buffer[i];
			if (CompilerHelper.AreEqual(source, slice, f.name))
			{
				index = i;
				return true;
			}
		}

		index = 0;
		return false;
	}

	// NATIVE FUNCTIONS
	public static bool ResolveToNativeFunctionIndex(this Compiler self, Slice slice, out int index)
	{
		var source = self.parser.tokenizer.source;

		for (var i = 0; i < self.chunk.nativeFunctions.count; i++)
		{
			var f = self.chunk.nativeFunctions.buffer[i];
			if (CompilerHelper.AreEqual(source, slice, f.name))
			{
				index = i;
				return true;
			}
		}

		index = 0;
		return false;
	}

	// STRUCTS
	public static StructTypeBuilder BeginStructDeclaration(this Compiler self)
	{
		return self.chunk.BeginStructType();
	}

	public static void EndStructDeclaration(this Compiler self, StructTypeBuilder builder, Slice slice)
	{
		var name = CompilerHelper.GetSlice(self, slice);
		var result = builder.Build(name, out var index);
		self.CheckStructBuild(result, slice, name);
	}

	public static bool ResolveToStructTypeIndex(this Compiler self, Slice slice, out int index)
	{
		var source = self.parser.tokenizer.source;

		for (var i = 0; i < self.chunk.structTypes.count; i++)
		{
			var s = self.chunk.structTypes.buffer[i];
			if (CompilerHelper.AreEqual(source, slice, s.name))
			{
				index = i;
				return true;
			}
		}

		index = 0;
		return false;
	}
}
