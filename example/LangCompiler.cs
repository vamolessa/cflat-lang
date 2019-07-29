using System.Collections.Generic;

public sealed class LangCompiler
{
	public Result<ByteCodeChunk, List<CompileError>> Compile(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();

		tokenizer.Begin(LangScanners.scanners, source);
		compiler.Begin(tokenizer, LangParseRules.rules, OnParseWithPrecedence);

		compiler.Next();

		while (!compiler.Match(Token.EndKind))
		{
			compiler.Consume((int)TokenKind.OpenCurlyBrackets, "");
			BlockStatement(compiler, (int)Precedence.Primary);
		}

		// end compiler
		compiler.EmitInstruction(Instruction.Halt);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.GetByteCodeChunk());
	}

	public static void OnParseWithPrecedence(Compiler compiler, int precedence)
	{
		var canAssign = precedence <= (int)Precedence.Assignment;
		if (canAssign && compiler.Match((int)TokenKind.Equal))
		{
			compiler.AddHardError(compiler.previousToken, "Invalid assignment target");
			Expression(compiler, precedence);
		}
	}

	public static void Statement(Compiler compiler, int precedence)
	{
		if (compiler.Match((int)TokenKind.OpenCurlyBrackets))
			BlockStatement(compiler, precedence);
		else if (compiler.Match((int)TokenKind.Let))
			VariableDeclaration(compiler, precedence);
		else if (compiler.Match((int)TokenKind.Print))
			PrintStatement(compiler, precedence);
		else
			ExpressionStatement(compiler, precedence);
	}

	public static void ExpressionStatement(Compiler compiler, int precedence)
	{
		Expression(compiler, precedence);
		compiler.EmitInstruction(Instruction.Pop);
		compiler.PopType();

		// sync here (global variables)
	}

	private static void VariableDeclaration(Compiler compiler, int precedence)
	{
		compiler.Consume((int)TokenKind.Identifier, "Expected variable name");
		var token = compiler.previousToken;

		compiler.Consume((int)TokenKind.Equal, "Expected assignment");
		Expression(compiler, precedence);

		compiler.DeclareLocalVariable(token);
	}

	private static void PrintStatement(Compiler compiler, int precedence)
	{
		Expression(compiler, precedence);
		compiler.EmitInstruction(Instruction.Print);
		compiler.PopType();
	}

	public static void BlockStatement(Compiler compiler, int precedence)
	{
		compiler.BeginScope();

		while (
			!compiler.Check((int)TokenKind.CloseCurlyBrackets) &&
			!compiler.Check(Token.EndKind)
		)
		{
			Statement(compiler, precedence);
		}

		compiler.Consume((int)TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		compiler.EndScope();
	}

	public static void Expression(Compiler compiler, int precedence)
	{
		compiler.ParseWithPrecedence((int)Precedence.Assignment);
	}

	public static void Grouping(Compiler compiler, int precedence)
	{
		Expression(compiler, precedence);
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void Literal(Compiler compiler, int precedence)
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
				compiler.previousToken,
				string.Format("Expected literal. Got {0}", compiler.previousToken.kind)
			);
			compiler.PushType(ValueType.Nil);
			break;
		}
	}

	public static void Variable(Compiler compiler, int precedence)
	{
		var token = compiler.previousToken;
		var index = compiler.ResolveToLocalVariableIndex();

		var canAssign = precedence <= (int)Precedence.Assignment;
		if (canAssign && compiler.Match((int)TokenKind.Equal))
		{
			Expression(compiler, precedence);

			if (index < 0)
			{
				compiler.AddSoftError(token, "Variable not declared");
			}
			else
			{
				compiler.EmitInstruction(Instruction.AssignLocal);
				compiler.EmitByte((byte)index);
			}

			compiler.PopType();
		}
		else
		{
			if (index < 0)
			{
				compiler.AddSoftError(token, "Variable not declared");
				compiler.PushType(ValueType.Nil);
			}
			else
			{
				compiler.EmitInstruction(Instruction.LoadLocal);
				compiler.EmitByte((byte)index);
				compiler.PushType(compiler.GetLocalVariable(index).type);
			}
		}
	}

	public static void Unary(Compiler compiler, int precedence)
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
				compiler.AddSoftError(opToken, "Unary minus operator can only be applied to ints or floats");
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
				compiler.PushType(ValueType.Bool);
				break;
			case ValueType.Bool:
				compiler.EmitInstruction(Instruction.Not);
				compiler.PushType(ValueType.Bool);
				break;
			case ValueType.Int:
			case ValueType.Float:
			case ValueType.String:
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.LoadFalse);
				compiler.PushType(ValueType.Bool);
				break;
			default:
				compiler.AddSoftError(opToken, "Not operator can only be applied to nil, bools, ints, floats or strings");
				compiler.PushType(ValueType.Bool);
				break;
			}
			break;
		default:
			compiler.AddHardError(
					opToken,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			compiler.PushType(ValueType.Nil);
			break;
		}
	}

	public static void Binary(Compiler compiler, int precedence)
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
				compiler.AddSoftError(opToken, "Plus operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Minus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.SubtractInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.SubtractFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken, "Minus operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Asterisk:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.MultiplyInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.MultiplyFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken, "Multiply operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Slash:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.DivideInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.DivideFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken, "Divide operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.EqualEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken, "Equal operator can only be applied to same type values");
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
				compiler.AddSoftError(opToken, "Equal operator can only be applied to nil, bools, ints and floats");
				break;
			}
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.BangEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken, "NotEqual operator can only be applied to same type values");
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
				compiler.AddSoftError(opToken, "NotEqual operator can only be applied to nil, bools, ints and floats");
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
				compiler.AddSoftError(opToken, "Greater operator can only be applied to ints or floats");
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
				compiler.AddSoftError(opToken, "GreaterOrEqual operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.Less:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.LessInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.LessFloat);
			else
				compiler.AddSoftError(opToken, "Less operator can only be applied to ints or floats");
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
				compiler.AddSoftError(opToken, "LessOrEqual operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		default:
			return;
		}
	}
}