using System.Collections.Generic;

public sealed class LangCompiler
{
	public Result<ByteCodeChunk, List<CompileError>> Compile(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();

		tokenizer.Begin(LangScanners.scanners, source);
		compiler.Begin(tokenizer, LangParseRules.rules);

		compiler.Next();

		while (!compiler.Match(Token.EndKind))
			Declaration(compiler);

		// end compiler
		compiler.EmitInstruction(Instruction.Return);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.GetByteCodeChunk());
	}

	public static void Declaration(Compiler compiler)
	{
		if (compiler.Match((int)TokenKind.Let))
			VarDeclaration(compiler);
		else
			Statement(compiler);

		// sync here (global variables)
	}

	public static void Statement(Compiler compiler)
	{
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Pop);
	}

	private static void VarDeclaration(Compiler compiler)
	{
		compiler.Consume((int)TokenKind.Identifier, "Expected variable name");
		var name = CompilerHelper.GetString(compiler);

		compiler.Consume((int)TokenKind.Equal, "Expected assignment");
		Expression(compiler);

		compiler.EmitInstruction(Instruction.Pop);
	}

	public static void ExpressionStatement(Compiler compiler)
	{
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Pop);
	}

	public static void Expression(Compiler compiler)
	{
		compiler.ParseWithPrecedence((int)Precedence.Assignment);
	}

	public static void Grouping(Compiler compiler)
	{
		Expression(compiler);
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void Literal(Compiler compiler)
	{
		switch ((TokenKind)compiler.previousToken.kind)
		{
		case TokenKind.Nil:
			compiler.EmitInstruction(Instruction.LoadNil);
			compiler.PushType(ValueType.Nil);
			break;
		case TokenKind.True:
			compiler.EmitInstruction(Instruction.LoadTrue);
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.False:
			compiler.EmitInstruction(Instruction.LoadFalse);
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.IntegerNumber:
			compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(compiler)),
				ValueType.Int
			);
			compiler.PushType(ValueType.Int);
			break;
		case TokenKind.RealNumber:
			compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(compiler)),
				ValueType.Float
			);
			compiler.PushType(ValueType.Float);
			break;
		case TokenKind.String:
			compiler.EmitLoadStringLiteral(CompilerHelper.GetString(compiler));
			compiler.PushType(ValueType.String);
			break;
		default:
			compiler.AddHardError(
				compiler.previousToken.index,
				string.Format("Expected literal. Got {0}", compiler.previousToken.kind)
			);
			break;
		}
	}

	public static void Variable(Compiler compiler)
	{
		var name = compiler.previousToken;
	}

	public static void Unary(Compiler compiler)
	{
		var opToken = compiler.previousToken;

		compiler.ParseWithPrecedence((int)Precedence.Unary);
		var type = compiler.PopType();

		switch ((TokenKind)opToken.kind)
		{
		case TokenKind.Minus:
			switch (type)
			{
			case ValueType.Int:
				compiler.EmitInstruction(Instruction.NegateInt);
				compiler.PushType(ValueType.Int);
				break;
			case ValueType.Float:
				compiler.EmitInstruction(Instruction.NegateFloat);
				compiler.PushType(ValueType.Float);
				break;
			default:
				compiler.AddSoftError(opToken.index, "Unary minus operator can only be applied to ints or floats");
				compiler.PushType(type);
				break;
			}
			break;
		case TokenKind.Bang:
			switch (type)
			{
			case ValueType.Nil:
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.LoadTrue);
				break;
			case ValueType.Bool:
				compiler.EmitInstruction(Instruction.Not);
				break;
			case ValueType.Int:
			case ValueType.Float:
			case ValueType.String:
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.LoadFalse);
				break;
			default:
				compiler.AddSoftError(opToken.index, "Not operator can only be applied to nil, bools, ints, floats or strings");
				break;
			}
			break;
		default:
			compiler.AddHardError(
					opToken.index,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			break;
		}
	}

	public static void Binary(Compiler compiler)
	{
		var opToken = compiler.previousToken;

		var opPrecedence = compiler.GetTokenPrecedence(opToken.kind);
		compiler.ParseWithPrecedence(opPrecedence + 1);

		var bType = compiler.PopType();
		var aType = compiler.PopType();

		switch ((TokenKind)opToken.kind)
		{
		case TokenKind.Plus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.AddInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.AddFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.index, "Plus operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Minus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.SubtractInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.SubtractFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.index, "Minus operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Asterisk:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.MultiplyInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.MultiplyFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.index, "Multiply operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Slash:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.DivideInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.DivideFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.index, "Divide operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.EqualEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken.index, "Equal operator can only be applied to same type values");
				compiler.PushType(ValueType.Bool);
				break;
			}

			switch (aType)
			{
			case ValueType.Nil:
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.LoadTrue);
				break;
			case ValueType.Bool:
				compiler.EmitInstruction(Instruction.EqualBool);
				break;
			case ValueType.Int:
				compiler.EmitInstruction(Instruction.EqualInt);
				break;
			case ValueType.Float:
				compiler.EmitInstruction(Instruction.EqualFloat);
				break;
			case ValueType.String:
				compiler.EmitInstruction(Instruction.EqualString);
				break;
			default:
				compiler.AddSoftError(opToken.index, "Equal operator can only be applied to nil, bools, ints and floats");
				break;
			}
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.BangEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken.index, "NotEqual operator can only be applied to same type values");
				compiler.PushType(ValueType.Bool);
				break;
			}

			switch (aType)
			{
			case ValueType.Nil:
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.LoadFalse);
				break;
			case ValueType.Bool:
				compiler.EmitInstruction(Instruction.EqualBool);
				break;
			case ValueType.Int:
				compiler.EmitInstruction(Instruction.EqualInt);
				break;
			case ValueType.Float:
				compiler.EmitInstruction(Instruction.EqualFloat);
				break;
			case ValueType.String:
				compiler.EmitInstruction(Instruction.EqualString);
				break;
			default:
				compiler.AddSoftError(opToken.index, "NotEqual operator can only be applied to nil, bools, ints and floats");
				break;
			}
			compiler.EmitInstruction(Instruction.Not);
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.Greater:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.GreaterInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.GreaterFloat);
			else
				compiler.AddSoftError(opToken.index, "Greater operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.GreaterEqual:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler
					.EmitInstruction(Instruction.LessInt)
					.EmitInstruction(Instruction.Not);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler
					.EmitInstruction(Instruction.LessFloat)
					.EmitInstruction(Instruction.Not);
			else
				compiler.AddSoftError(opToken.index, "GreaterOrEqual operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.Less:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.LessInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.LessFloat);
			else
				compiler.AddSoftError(opToken.index, "Less operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.LessEqual:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler
					.EmitInstruction(Instruction.GreaterInt)
					.EmitInstruction(Instruction.Not);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler
					.EmitInstruction(Instruction.GreaterFloat)
					.EmitInstruction(Instruction.Not);
			else
				compiler.AddSoftError(opToken.index, "LessOrEqual operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		default:
			return;
		}
	}
}