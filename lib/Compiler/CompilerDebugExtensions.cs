internal static class CompilerDebugExtensions
{
	public static void DebugEmitPushFrame(this Compiler self)
	{
		if (self.mode == Mode.Debug)
			self.EmitInstruction(Instruction.DebugPushFrame);
	}

	public static void DebugEmitPopFrame(this Compiler self)
	{
		if (self.mode == Mode.Debug)
			self.EmitInstruction(Instruction.DebugPopFrame);
	}

	public static void DebugEmitPushType(this Compiler self, ValueType type)
	{
		if (self.mode == Mode.Debug)
		{
			self.EmitInstruction(Instruction.DebugPushType);
			self.EmitType(type);
		}
	}

	public static void DebugEmitPopType(this Compiler self, byte count)
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