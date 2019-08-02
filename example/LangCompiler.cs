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
			compiler.Consume((int)TokenKind.Function, "Expected 'fn' before function name");
			FunctionDeclaration(compiler);

			compiler.Synchronize(token =>
			{
				switch ((TokenKind)token)
				{
				case TokenKind.Function:
					return true;
				default:
					return false;
				}
			});
		}

		compiler.EmitInstruction(Instruction.Halt);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.GetByteCodeChunk());
	}

	public Result<ByteCodeChunk, List<CompileError>> CompileExpression(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();
		tokenizer.Begin(LangScanners.scanners, source);
		compiler.Begin(tokenizer, LangParseRules.rules, OnParseWithPrecedence);

		compiler.Next();
		Expression(compiler);
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
			Expression(compiler);
		}
	}

	public static void FunctionDeclaration(Compiler compiler)
	{
		const int MaxParamCount = 8;

		compiler.Consume((int)TokenKind.Identifier, "Expected function name");
		var slice = compiler.previousToken.slice;

		var declaration = compiler.BeginFunctionDeclaration();

		compiler.Consume((int)TokenKind.OpenParenthesis, "Expected '(' after function name");
		if (!compiler.Check((int)TokenKind.CloseParenthesis))
		{
			do
			{
				compiler.Consume((int)TokenKind.Identifier, "Expected parameter name");
				var paramSlice = compiler.previousToken.slice;
				compiler.Consume((int)TokenKind.Colon, "Expected ':' after parameter name");
				compiler.Consume((int)TokenKind.Identifier, "Expected parameter type");

				var paramType = compiler.ResolveType();
				if (!paramType.isSome)
				{
					compiler.AddSoftError(compiler.previousToken.slice, "Could not find type");
					paramType = Option.Some(ValueType.Unit);
				}

				if (declaration.parameterCount >= MaxParamCount)
				{
					compiler.AddSoftError(paramSlice, "Function can not have more than {0} parameters", MaxParamCount);
					continue;
				}

				compiler.PushType(paramType.value);
				var paramIndex = compiler.DeclareLocalVariable(paramSlice, false);
				compiler.UseVariable(paramIndex);
				compiler.PopType();

				declaration.AddParam(paramType.value);
			} while (compiler.Match((int)TokenKind.Comma));
		}
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (compiler.Match((int)TokenKind.Colon))
		{
			compiler.Consume((int)TokenKind.Identifier, "Expected function return type");
			var returnType = compiler.ResolveType();
			if (!returnType.isSome)
				compiler.AddSoftError(compiler.previousToken.slice, "Could not find type");
			else
				declaration.returnType = returnType.value;
		}

		compiler.EndFunctionDeclaration(slice, declaration);

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' before function body");

		if (declaration.returnType == ValueType.Unit)
		{
			BlockStatement(compiler);
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(compiler, (int)Precedence.None);
			var type = compiler.PopType();
			if (declaration.returnType != type)
				compiler.AddSoftError(compiler.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", declaration.returnType, type);
		}

		compiler.EmitInstruction(Instruction.Return);
		compiler.EndFunctionBody();
	}

	public static Option<ValueType> Statement(Compiler compiler)
	{
		if (compiler.Match((int)TokenKind.OpenCurlyBrackets))
		{
			BlockStatement(compiler);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Let))
		{
			VariableDeclaration(compiler, false);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Mut))
		{
			VariableDeclaration(compiler, true);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.While))
		{
			WhileStatement(compiler);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.For))
		{
			ForStatement(compiler);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Break))
		{
			BreakStatement(compiler);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Return))
		{
			ReturnStatement(compiler);
			return Option.None;
		}
		else if (compiler.Match((int)TokenKind.Print))
		{
			PrintStatement(compiler);
			return Option.None;
		}
		else
		{
			var type = ExpressionStatement(compiler);
			return Option.Some(type);
		}
	}

	public static ValueType ExpressionStatement(Compiler compiler)
	{
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Pop);
		return compiler.PopType();
	}

	public static void BlockStatement(Compiler compiler)
	{
		var scope = compiler.BeginScope();
		while (
			!compiler.Check((int)TokenKind.CloseCurlyBrackets) &&
			!compiler.Check(Token.EndKind)
		)
		{
			Statement(compiler);
		}

		compiler.Consume((int)TokenKind.CloseCurlyBrackets, "Expected '}' after block.");
		compiler.EndScope(scope);
	}

	private static int VariableDeclaration(Compiler compiler, bool mutable)
	{
		compiler.Consume((int)TokenKind.Identifier, "Expected variable name");
		var slice = compiler.previousToken.slice;

		compiler.Consume((int)TokenKind.Equal, "Expected assignment");
		Expression(compiler);

		return compiler.DeclareLocalVariable(slice, mutable);
	}

	public static void WhileStatement(Compiler compiler)
	{
		var loopJump = compiler.BeginEmitBackwardJump();
		Expression(compiler);

		if (compiler.PopType() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression as while condition");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		compiler.BeginLoop();
		BlockStatement(compiler);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		compiler.EndLoop();
	}

	public static void ForStatement(Compiler compiler)
	{
		var scope = compiler.BeginScope();
		var itVarIndex = VariableDeclaration(compiler, true);
		compiler.UseVariable(itVarIndex);
		var itVar = compiler.GetLocalVariable(itVarIndex);
		if (itVar.type != ValueType.Int)
			compiler.AddSoftError(itVar.slice, "Expected int variable in for loop");

		compiler.Consume((int)TokenKind.Comma, "Expected comma after begin expression");
		Expression(compiler);
		var toVarIndex = compiler.DeclareLocalVariable(compiler.previousToken.slice, false);
		compiler.UseVariable(toVarIndex);
		if (compiler.GetLocalVariable(toVarIndex).type != ValueType.Int)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected int expression");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var loopJump = compiler.BeginEmitBackwardJump();
		compiler.EmitInstruction(Instruction.ForLoopCheck);
		compiler.EmitByte((byte)itVarIndex);

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		compiler.BeginLoop();
		BlockStatement(compiler);

		compiler.EmitInstruction(Instruction.IncrementLocal);
		compiler.EmitByte((byte)itVarIndex);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		compiler.EndLoop();

		compiler.EndScope(scope);
	}

	private static void BreakStatement(Compiler compiler)
	{
		var breakJump = compiler.BeginEmitForwardJump(Instruction.JumpForward);

		if (!compiler.BreakLoop(1, breakJump))
		{
			compiler.AddSoftError(compiler.previousToken.slice, "Not inside a loop");
			return;
		}
	}

	private static void ReturnStatement(Compiler compiler)
	{
		if (compiler.Match((int)TokenKind.CloseCurlyBrackets))
		{
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Expression(compiler);
			var type = compiler.PopType();
			var declaration = compiler.PeekFunctionBuilder();
			if (declaration.returnType != type)
				compiler.AddSoftError(compiler.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", declaration.returnType, type);
		}

		compiler.EmitInstruction(Instruction.Return);
	}

	private static void PrintStatement(Compiler compiler)
	{
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Print);
		compiler.PopType();
	}

	public static void Expression(Compiler compiler)
	{
		compiler.ParseWithPrecedence((int)Precedence.Assignment);
	}

	public static void Grouping(Compiler compiler, int precedence)
	{
		Expression(compiler);
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void Block(Compiler compiler, int precedence)
	{
		var scope = compiler.BeginScope();
		var maybeType = new Option<ValueType>();

		while (
			!compiler.Check((int)TokenKind.CloseCurlyBrackets) &&
			!compiler.Check(Token.EndKind)
		)
		{
			maybeType = Statement(compiler);
		}

		if (maybeType.isSome)
		{
			compiler.PopEmittedByte();

			var varCount = compiler.GetScopeLocalVariableCount(scope);
			if (varCount > 0)
			{
				compiler.EmitInstruction(Instruction.CopyTo);
				compiler.EmitByte((byte)varCount);
			}
		}

		compiler.Consume((int)TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		compiler.EndScope(scope);

		if (maybeType.isSome)
		{
			compiler.PushType(maybeType.value);
		}
		else
		{
			compiler.PushType(ValueType.Unit);
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
	}

	public static void If(Compiler compiler, int precedence)
	{
		Expression(compiler);

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
			compiler.EmitInstruction(Instruction.LoadUnit);
			if (thenType != ValueType.Unit)
				compiler.AddSoftError(compiler.previousToken.slice, "If expression must not produce a value when there is no else branch. Found type: {0}. Try ending with '{}'", thenType);
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
		case TokenKind.True:
			compiler.EmitInstruction(Instruction.LoadTrue);
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.False:
			compiler.EmitInstruction(Instruction.LoadFalse);
			compiler.PushType(ValueType.Bool);
			break;
		case TokenKind.IntLiteral:
			compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(compiler)),
				ValueType.Int
			);
			compiler.PushType(ValueType.Int);
			break;
		case TokenKind.FloatLiteral:
			compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(compiler)),
				ValueType.Float
			);
			compiler.PushType(ValueType.Float);
			break;
		case TokenKind.StringLiteral:
			compiler.EmitLoadStringLiteral(CompilerHelper.GetString(compiler));
			compiler.PushType(ValueType.String);
			break;
		default:
			compiler.AddHardError(
				compiler.previousToken.slice,
				string.Format("Expected literal. Got {0}", compiler.previousToken.kind)
			);
			compiler.PushType(ValueType.Unit);
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
			Expression(compiler);

			if (index < 0)
			{
				compiler.AddSoftError(slice, "Can not write to undeclared variable. Declare it with 'let'");
			}
			else
			{
				if (!compiler.GetLocalVariable(index).isMutable)
					compiler.AddSoftError(slice, "Can not write to immutable variable. Try using 'mut' instead of 'let'");

				compiler.EmitInstruction(Instruction.AssignLocal);
				compiler.EmitByte((byte)index);
			}
		}
		else
		{
			if (index < 0)
			{
				var functionIndex = compiler.ResolveToFunctionIndex();
				if (functionIndex < 0)
				{
					compiler.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let'");
					compiler.PushType(ValueType.Unit);
				}
				else
				{
					compiler.EmitLoadFunction(functionIndex);
					var type = ValueTypeHelper.SetIndex(ValueType.Function, functionIndex);
					compiler.PushType(type);
				}
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

	public static void Call(Compiler compiler, int precedence)
	{
		var slice = compiler.previousToken.slice;

		var functionIndex = -1;
		var function = new FunctionDefinition();
		var type = compiler.PopType();

		if (ValueTypeHelper.GetKind(type) == ValueType.Function)
			functionIndex = ValueTypeHelper.GetIndex(type);
		else
			compiler.AddSoftError(slice, "Callee must be a function");

		var hasFunction = functionIndex >= 0;
		if (hasFunction)
			function = compiler.GetFunction(functionIndex);
		else
			compiler.AddSoftError(slice, "Could not find such function");

		var argIndex = 0;
		if (!compiler.Check((int)TokenKind.CloseParenthesis))
		{
			do
			{
				Expression(compiler);
				var argType = compiler.PopType();
				if (
					hasFunction &&
					argIndex < function.parameters.length &&
					argType != compiler.GetFunctionParamType(in function, argIndex)
				)
				{
					compiler.AddSoftError(
						compiler.previousToken.slice,
						"Wrong type for argument {0}. Expected {1}. Got {2}",
						argIndex + 1,
						compiler.GetFunctionParamType(in function, argIndex),
						argType
					);
				}

				argIndex += 1;
			} while (compiler.Match((int)TokenKind.Comma));
		}

		compiler.Consume((int)TokenKind.CloseParenthesis, "Expect ')' after function argument list");

		if (hasFunction && argIndex != function.parameters.length)
			compiler.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", function.parameters.length, argIndex);

		compiler.EmitInstruction(Instruction.Call);
		compiler.EmitByte((byte)(hasFunction ? function.parameters.length : 0));
		compiler.PushType(
			hasFunction ? function.returnType : ValueType.Unit
		);
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
				compiler.AddSoftError(opToken.slice, "Not operator can only be applied to bools, ints, floats or strings");
				compiler.PushType(ValueType.Bool);
				break;
			}
			break;
		default:
			compiler.AddHardError(
					opToken.slice,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			compiler.PushType(ValueType.Unit);
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
				compiler.AddSoftError(opToken.slice, "Equal operator can only be applied to bools, ints and floats");
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
				compiler.AddSoftError(opToken.slice, "NotEqual operator can only be applied to bools, ints and floats");
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