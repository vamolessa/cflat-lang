public static class CompilerDebugExtensions
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

	public static void DebugEmitPopType(this Compiler self)
	{
		if (self.mode == Mode.Debug)
			self.EmitInstruction(Instruction.DebugPopType);
	}
}