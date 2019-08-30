public static class CompilerFlowExtensions
{
	public static Scope BeginScope(this Compiler self)
	{
		self.scopeDepth += 1;
		return new Scope(self.localVariables.count);
	}

	public static void EndScope(this Compiler self, Scope scope, int sizeLeftOnStack)
	{
		self.scopeDepth -= 1;

		for (var i = scope.localVariablesStartIndex; i < self.localVariables.count; i++)
		{
			var variable = self.localVariables.buffer[i];
			if (!variable.isUsed)
				self.AddSoftError(variable.slice, "Unused variable");
		}

		var localCount = self.localVariables.count - scope.localVariablesStartIndex;
		if (localCount == 0)
			return;

		var localVarsSize = 0;
		for (var i = scope.localVariablesStartIndex; i < self.localVariables.count; i++)
		{
			var type = self.localVariables.buffer[i].type;
			localVarsSize += type.GetSize(self.chunk);
		}

		if (sizeLeftOnStack > 0)
		{
			self.EmitInstruction(Instruction.Move);
			self.EmitByte((byte)localVarsSize);
			self.EmitByte((byte)sizeLeftOnStack);
		}
		else
		{
			self.EmitPop(localVarsSize);
		}

		self.localVariables.count -= localCount;

		self.DebugEmitPopType((byte)localCount);
	}

	public static void BeginLoop(this Compiler self, Slice labelSlice)
	{
		self.loopNesting.PushBack(labelSlice);
	}

	public static void EndLoop(this Compiler self)
	{
		self.loopNesting.count -= 1;

		for (var i = self.loopBreaks.count - 1; i >= 0; i--)
		{
			var loopBreak = self.loopBreaks.buffer[i];
			if (loopBreak.nesting == self.loopNesting.count)
			{
				self.EndEmitForwardJump(loopBreak.jump);
				self.loopBreaks.SwapRemove(i);
			}
		}
	}
}