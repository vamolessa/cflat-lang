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

		var chunk = compiler.GetByteCodeChunk();
		//Optimizer.Optimize(chunk);

		return Result.Ok(chunk);
	}

	public Result<ByteCodeChunk, List<CompileError>> CompileExpression(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();
		tokenizer.Begin(LangScanners.scanners, source);
		compiler.Begin(tokenizer, LangParseRules.rules, OnParseWithPrecedence);

		compiler.Next();
		Expression(compiler, (int)Precedence.Assignment);
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
			compiler.AddHardError(compiler.previousToken.slice, "Invalid assignment target");
			Expression(compiler, precedence);
		}
	}

	public static Option<ValueType> Statement(Compiler compiler, int precedence)
	{
		if (compiler.Match((int)TokenKind.OpenCurlyBrackets))
		{
			BlockStatement(compiler, precedence);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Let))
		{
			VariableDeclarationStatement(compiler, precedence, false);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Mut))
		{
			VariableDeclarationStatement(compiler, precedence, true);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.While))
		{
			WhileStatement(compiler, precedence);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.For))
		{
			ForStatement(compiler, precedence);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Print))
		{
			PrintStatement(compiler, precedence);
			return Option.None;
		}
		else
		{
			var type = ExpressionStatement(compiler, precedence);
			return Option.Some(type);
		}
	}

	public static ValueType ExpressionStatement(Compiler compiler, int precedence)
	{
		Expression(compiler, precedence);
		compiler.EmitInstruction(Instruction.Pop);
		var type = compiler.PopType();

		// sync here (global variables)

		return type;
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
		compiler.EndScope(false);
	}

	private static int VariableDeclarationStatement(Compiler compiler, int precedence, bool mutable)
	{
		compiler.Consume((int)TokenKind.Identifier, "Expected variable name");
		var slice = compiler.previousToken.slice;

		compiler.Consume((int)TokenKind.Equal, "Expected assignment");
		Expression(compiler, precedence);

		return compiler.DeclareLocalVariable(slice, mutable);
	}

	public static void WhileStatement(Compiler compiler, int precedence)
	{
		var loopJump = compiler.BeginEmitBackwardJump();
		Expression(compiler, precedence);

		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression as while condition");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		BlockStatement(compiler, precedence);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
	}

	public static void ForStatement(Compiler compiler, int precedence)
	{
		compiler.BeginScope();
		var itVarIndex = VariableDeclarationStatement(compiler, precedence, true);
		compiler.UseVariable(itVarIndex);
		var itVar = compiler.GetLocalVariable(itVarIndex);
		if (itVar.type != ValueType.Int)
			compiler.AddSoftError(itVar.slice, "Expected int variable in for loop");

		compiler.Consume((int)TokenKind.Comma, "Expected comma after begin expression");
		Expression(compiler, precedence);
		var toVarIndex = compiler.DeclareLocalVariable(compiler.previousToken.slice, false);
		compiler.UseVariable(toVarIndex);
		if (compiler.GetLocalVariable(toVarIndex).type != ValueType.Int)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected int expression");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var loopJump = compiler.BeginEmitBackwardJump();
		compiler.EmitInstruction(Instruction.ForLoopCheck);
		compiler.EmitByte((byte)itVarIndex);

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		BlockStatement(compiler, precedence);

		compiler.EmitInstruction(Instruction.IncrementLocal);
		compiler.EmitByte((byte)itVarIndex);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);

		compiler.EndScope(false);
	}

	private static void PrintStatement(Compiler compiler, int precedence)
	{
		Expression(compiler, precedence);
		compiler.EmitInstruction(Instruction.Print);
		compiler.PopType();
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

	public static void Block(Compiler compiler, int precedence)
	{
		compiler.BeginScope();
		var maybeType = new Option<ValueType>();

		while (
			!compiler.Check((int)TokenKind.CloseCurlyBrackets) &&
			!compiler.Check(Token.EndKind)
		)
		{
			maybeType = Statement(compiler, precedence);
		}

		if (maybeType.isSome)
		{
			compiler.RemoveLastEmittedByte();
			compiler.PushType(maybeType.value);
		}
		else
		{
			compiler.EmitInstruction(Instruction.LoadNil);
			compiler.PushType(ValueType.Nil);
		}

		compiler.Consume((int)TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		compiler.EndScope(true);
	}

	public static void If(Compiler compiler, int precedence)
	{
		Expression(compiler, precedence);

		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression as if condition");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

		var elseJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		Block(compiler, precedence);
		var thenType = compiler.PopType();

		var thenJump = compiler.BeginEmitForwardJump(Instruction.JumpForward);
		compiler.EndEmitForwardJump(elseJump);

		if (compiler.Match((int)TokenKind.Else))
		{
			if (compiler.Match((int)TokenKind.If))
			{
				If(compiler, precedence);
			}
			else
			{
				compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after else");
				Block(compiler, precedence);
			}

			var elseType = compiler.PopType();
			if (thenType != elseType)
				compiler.AddSoftError(compiler.previousToken.slice, "If expression must produce values of the same type on both branches. Found types: {0} and {1}", thenType, elseType);
		}
		else
		{
			compiler.EmitInstruction(Instruction.LoadNil);
			if (thenType != ValueType.Nil)
				compiler.AddSoftError(compiler.previousToken.slice, "If expression must produce nil when there is no else branch. Found type: {0}", thenType);
		}

		compiler.EndEmitForwardJump(thenJump);
		compiler.PushType(thenType);
	}

	public static void And(Compiler compiler, int precedence)
	{
		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression before and");

		var jump = compiler.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		compiler.EmitInstruction(Instruction.Pop);
		compiler.ParseWithPrecedence((int)Precedence.And);
		compiler.EndEmitForwardJump(jump);

		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression after and");

		compiler.PushType(ValueType.Bool);
	}

	public static void Or(Compiler compiler, int precedence)
	{
		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression before or");

		var jump = compiler.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		compiler.EmitInstruction(Instruction.Pop);
		compiler.ParseWithPrecedence((int)Precedence.Or);
		compiler.EndEmitForwardJump(jump);

		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression after or");

		compiler.PushType(ValueType.Bool);
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
				compiler.previousToken.slice,
				string.Format("Expected literal. Got {0}", compiler.previousToken.kind)
			);
			compiler.PushType(ValueType.Nil);
			break;
		}
	}

	public static void Variable(Compiler compiler, int precedence)
	{
		var slice = compiler.previousToken.slice;
		var index = compiler.ResolveToLocalVariableIndex();

		var canAssign = precedence <= (int)Precedence.Assignment;
		if (canAssign && compiler.Match((int)TokenKind.Equal))
		{
			Expression(compiler, precedence);

			if (index < 0)
			{
				compiler.AddSoftError(slice, "Can not write to undeclared variable");
			}
			else
			{
				if (!compiler.GetLocalVariable(index).isMutable)
					compiler.AddSoftError(slice, "Can not write to immutable variable");

				compiler.EmitInstruction(Instruction.AssignLocal);
				compiler.EmitByte((byte)index);
			}
		}
		else
		{
			if (index < 0)
			{
				compiler.AddSoftError(slice, "Can not read undeclared variable");
				compiler.PushType(ValueType.Nil);
			}
			else
			{
				compiler.UseVariable(index);

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
				compiler.AddSoftError(opToken.slice, "Unary minus operator can only be applied to ints or floats");
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
				compiler.AddSoftError(opToken.slice, "Not operator can only be applied to nil, bools, ints, floats or strings");
				compiler.PushType(ValueType.Bool);
				break;
			}
			break;
		default:
			compiler.AddHardError(
					opToken.slice,
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
				compiler.AddSoftError(opToken.slice, "Plus operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Minus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.SubtractInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.SubtractFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Minus operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Asterisk:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.MultiplyInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.MultiplyFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Multiply operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.Slash:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.DivideInt).PushType(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.DivideFloat).PushType(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Divide operator can only be applied to ints or floats").PushType(aType);
			break;
		case TokenKind.EqualEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken.slice, "Equal operator can only be applied to same type values");
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
				compiler.AddSoftError(opToken.slice, "Equal operator can only be applied to nil, bools, ints and floats");
				break;
			}
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.BangEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken.slice, "NotEqual operator can only be applied to same type values");
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
				compiler.AddSoftError(opToken.slice, "NotEqual operator can only be applied to nil, bools, ints and floats");
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
				compiler.AddSoftError(opToken.slice, "Greater operator can only be applied to ints or floats");
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
				compiler.AddSoftError(opToken.slice, "GreaterOrEqual operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.Less:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.LessInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.LessFloat);
			else
				compiler.AddSoftError(opToken.slice, "Less operator can only be applied to ints or floats");
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
				compiler.AddSoftError(opToken.slice, "LessOrEqual operator can only be applied to ints or floats");
			compiler.PushType(ValueType.Bool);
			break;
		default:
			return;
		}
	}
}