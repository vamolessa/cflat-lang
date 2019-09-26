public static class CompilerDeclarationExtensions
{
	// VARIABLES
	public static int AddLocalVariable(this Compiler self, Slice slice, ValueType type, VariableFlags flags)
	{
		var stackIndex = 0;
		if (self.localVariables.count > 0)
		{
			var lastVar = self.localVariables.buffer[self.localVariables.count - 1];
			stackIndex = lastVar.stackIndex + lastVar.type.GetSize(self.chunk);
		}

		if (CompilerHelper.AreEqual(self.parser.tokenizer.source, slice, "_"))
			flags |= VariableFlags.Used;

		self.localVariables.PushBack(new LocalVariable(
			slice,
			stackIndex,
			self.scopeDepth,
			type,
			flags
		));

		return self.localVariables.count - 1;
	}

	public static int DeclareLocalVariable(this Compiler self, Slice slice, bool mutable)
	{
		if (self.localVariables.count >= self.localVariables.buffer.Length)
		{
			self.AddSoftError(slice, "Too many local variables in function");
			return -1;
		}

		var type = new ValueType(TypeKind.Unit);
		if (self.typeStack.count > 0)
			type = self.typeStack.PopLast();

		return self.AddLocalVariable(slice, type, mutable ? VariableFlags.Mutable : VariableFlags.None);
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

	public static void EndFunctionDeclaration(this Compiler self, FunctionTypeBuilder builder, Slice slice)
	{
		var result = builder.Build(out var index);
		if (self.CheckFunctionBuild(result, slice))
		{
			var name = CompilerHelper.GetSlice(self, slice);
			self.chunk.AddFunction(name, index);
		}
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