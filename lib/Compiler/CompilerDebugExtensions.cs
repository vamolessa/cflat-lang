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

		public static void DebugEmitPopType(this CompilerIO self, byte count)
		{
			if (self.mode == Mode.Debug)
			{
				if (count > 1)
				{
					self.EmitInstruction(Instruction.DebugPopTypeMultiple);
					self.EmitByte(count);
				}
				else
				{
					self.EmitInstruction(Instruction.DebugPopType);
				}
			}
		}
	}
}