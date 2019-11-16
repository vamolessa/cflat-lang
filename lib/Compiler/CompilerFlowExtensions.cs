namespace cflat
{
	internal static class CompilerFlowExtensions
	{
		public static Scope BeginScope(this CompilerIO self)
		{
			self.scopeDepth += 1;
			return new Scope(self.localVariables.count);
		}

		public static void EndScope(this CompilerIO self, Scope scope, int sizeLeftOnStack)
		{
			self.scopeDepth -= 1;

			for (var i = scope.localVariablesStartIndex; i < self.localVariables.count; i++)
			{
				var variable = self.localVariables.buffer[i];
				if (!variable.IsUsed)
					self.AddSoftError(variable.slice, "Unused variable '{0}'", CompilerHelper.GetSlice(self, variable.slice));
				if (variable.IsMutable && !variable.IsChanged)
					self.AddSoftError(variable.slice, "Mutable variable '{0}' never changes", CompilerHelper.GetSlice(self, variable.slice));
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

		public static void BeginLoop(this CompilerIO self, Slice labelSlice)
		{
			self.loopNesting.PushBack(labelSlice);
		}

		public static void EndLoop(this CompilerIO self)
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
}