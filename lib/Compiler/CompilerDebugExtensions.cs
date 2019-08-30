public static class CompilerTypeStackExtensions
{
	public static void PushType(this Compiler self, ValueType type)
	{
		self.typeStack.PushBack(type);
		self.DebugOnPushType(type);
	}

	public static ValueType PopType(this Compiler self)
	{
		var type = self.typeStack.PopLast();
		self.DebugOnPopType();
		return type;
	}
}

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

	public static void DebugOnPushType(this Compiler self, ValueType type)
	{
		if (self.mode == Mode.Debug)
		{
			self.EmitInstruction(Instruction.DebugPushType);
			self.EmitType(type);
		}
	}

	public static void DebugOnPopType(this Compiler self)
	{
		if (self.mode == Mode.Debug)
			self.EmitInstruction(Instruction.DebugPopType);
	}
}