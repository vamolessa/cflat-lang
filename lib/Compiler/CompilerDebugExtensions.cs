public static class CompilerDebugExtensions
{
	public static void DebugEmitSaveTypeStack(this Compiler self)
	{
		if (self.mode != Mode.Debug)
			return;

		var size = 0;
		for (var i = 0; i < self.typeStack.count; i++)
			size += self.typeStack.buffer[i].GetSize(self.chunk);

		self.EmitInstruction(Instruction.DebugSaveTypeStack);
		self.EmitUShort((ushort)self.typeStack.count);
		self.EmitUShort((ushort)size);

		for (var i = 0; i < self.typeStack.count; i++)
			self.EmitType(self.typeStack.buffer[i]);
	}
}