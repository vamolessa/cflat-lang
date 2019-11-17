namespace cflat
{
	internal static class CompilerDebugExtensions
	{
		public static void DebugEmitPushFrame(this CompilerIO self)
		{
			if (self.mode == Mode.Debug)
				self.EmitInstruction(Instruction.DebugPushFrame);
		}

		public static void DebugEmitPopFrame(this CompilerIO self)
		{
			if (self.mode == Mode.Debug)
				self.EmitInstruction(Instruction.DebugPopFrame);
		}

		public static void DebugEmitPushType(this CompilerIO self, ValueType type)
		{
			if (self.mode == Mode.Debug)
			{
				self.EmitInstruction(Instruction.DebugPushType);
				self.EmitType(type);
			}
		}

		public static void DebugEmitPopTypes(this CompilerIO self, byte count)
		{
			if (self.mode == Mode.Debug)
			{
				self.EmitInstruction(Instruction.DebugPopTypeMultiple);
				self.EmitByte(count);
			}
		}

		public static void DebugPushLocalVariableName(this CompilerIO self, string name)
		{
			if (self.mode == Mode.Debug)
			{
				var stringIndex = self.chunk.AddStringLiteral(name);
				self.EmitInstruction(Instruction.DebugPushLocalVariableName);
				self.EmitUShort((ushort)stringIndex);
			}
		}

		public static void DebugPopLocalVariableNames(this CompilerIO self, byte count)
		{
			if (self.mode == Mode.Debug)
			{
				self.EmitInstruction(Instruction.DebugPopLocalVariableNameMultiple);
				self.EmitByte(count);
			}
		}
	}
}