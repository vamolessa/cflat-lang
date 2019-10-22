internal static class CompilerEmitExtensions
{
	public static Compiler EmitByte(this Compiler self, byte value)
	{
		self.chunk.WriteByte(value, self.parser.previousToken.slice);
		return self;
	}

	public static Compiler EmitUShort(this Compiler self, ushort value)
	{
		BytesHelper.UShortToBytes(value, out var b0, out var b1);
		self.chunk.WriteByte(b0, self.parser.previousToken.slice);
		self.chunk.WriteByte(b1, self.parser.previousToken.slice);
		return self;
	}

	public static Compiler EmitInstruction(this Compiler self, Instruction instruction)
	{
		return self.EmitByte((byte)instruction);
	}

	public static Compiler EmitPop(this Compiler self, int size)
	{
		if (size > 1)
		{
			self.EmitInstruction(Instruction.PopMultiple);
			self.EmitByte((byte)size);
		}
		else if (size == 1)
		{
			self.EmitInstruction(Instruction.Pop);
		}

		return self;
	}

	public static Compiler EmitLoadLiteral(this Compiler self, ValueData value, TypeKind type)
	{
		var index = self.chunk.AddValueLiteral(value, type);
		self.EmitInstruction(Instruction.LoadLiteral);
		return self.EmitUShort((ushort)index);
	}

	public static Compiler EmitLoadStringLiteral(this Compiler self, string value)
	{
		var index = self.chunk.AddStringLiteral(value);
		self.EmitInstruction(Instruction.LoadLiteral);
		return self.EmitUShort((ushort)index);
	}

	public static Compiler EmitSetLocal(this Compiler self, int stackIndex, ValueType type)
	{
		var typeSize = type.GetSize(self.chunk);
		if (typeSize > 1)
		{
			self.EmitInstruction(Instruction.SetLocalMultiple);
			self.EmitByte((byte)stackIndex);
			return self.EmitByte(typeSize);
		}
		else
		{
			self.EmitInstruction(Instruction.SetLocal);
			return self.EmitByte((byte)stackIndex);
		}
	}

	public static Compiler EmitLoadLocal(this Compiler self, int stackIndex, ValueType type)
	{
		var typeSize = type.GetSize(self.chunk);
		if (typeSize > 1)
		{
			self.EmitInstruction(Instruction.LoadLocalMultiple);
			self.EmitByte((byte)stackIndex);
			return self.EmitByte(typeSize);
		}
		else
		{
			self.EmitInstruction(Instruction.LoadLocal);
			return self.EmitByte((byte)stackIndex);
		}
	}

	public static Compiler EmitLoadFunction(this Compiler self, Instruction instruction, int functionIndex)
	{
		self.EmitInstruction(instruction);
		return self.EmitUShort((ushort)functionIndex);
	}

	public static int BeginEmitBackwardJump(this Compiler self)
	{
		return self.chunk.bytes.count;
	}

	public static void EndEmitBackwardJump(this Compiler self, Instruction instruction, int jumpIndex)
	{
		self.EmitInstruction(instruction);

		var offset = self.chunk.bytes.count - jumpIndex + 2;
		if (offset > ushort.MaxValue)
		{
			self.AddSoftError(self.parser.previousToken.slice, "Too much code to jump over");
			return;
		}

		self.EmitUShort((ushort)offset);
	}

	public static int BeginEmitForwardJump(this Compiler self, Instruction instruction)
	{
		self.EmitInstruction(instruction);
		self.EmitUShort(0);

		return self.chunk.bytes.count - 2;
	}

	public static void EndEmitForwardJump(this Compiler self, int jumpIndex)
	{
		var offset = self.chunk.bytes.count - jumpIndex - 2;
		if (offset > ushort.MaxValue)
			self.AddSoftError(self.parser.previousToken.slice, "Too much code to jump over");

		BytesHelper.UShortToBytes(
			(ushort)offset,
			out self.chunk.bytes.buffer[jumpIndex],
			out self.chunk.bytes.buffer[jumpIndex + 1]
		);
	}

	public static void EmitType(this Compiler self, ValueType type)
	{
		type.Write(out var b0, out var b1, out var b2, out var b3);
		self.EmitByte(b0);
		self.EmitByte(b1);
		self.EmitByte(b2);
		self.EmitByte(b3);
	}
}