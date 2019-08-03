using System.Collections.Generic;

public readonly struct LoopBreak
{
	public readonly int nesting;
	public readonly int jump;

	public LoopBreak(int nesting, int jump)
	{
		this.nesting = nesting;
		this.jump = jump;
	}
}

public sealed class LangCompiler
{
	public readonly ParseRule[] rules = new ParseRule[(int)TokenKind.COUNT];
	public Buffer<ValueType> functionReturnTypeStack = new Buffer<ValueType>(4);
	public Buffer<LoopBreak> loopBreaks = new Buffer<LoopBreak>(4);
	public int loopNesting;

	public LangCompiler()
	{
		LangParseRules.InitRulesFor(this);
	}

	public Result<ByteCodeChunk, List<CompileError>> Compile(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();

		tokenizer.Reset(LangScanners.scanners, source);
		compiler.Reset(tokenizer, rules, OnParseWithPrecedence);

		compiler.Next();

		while (!compiler.Match(Token.EndKind))
		{
			compiler.Consume((int)TokenKind.Function, "Expected 'fn' before function name");
			FunctionDeclaration(compiler);
			Syncronize(compiler);
		}

		compiler.EmitInstruction(Instruction.Halt);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.chunk);
	}

	public Result<ByteCodeChunk, List<CompileError>> CompileExpression(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();
		tokenizer.Reset(LangScanners.scanners, source);
		compiler.Reset(tokenizer, rules, OnParseWithPrecedence);

		compiler.Next();
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Halt);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.chunk);
	}

	private void Syncronize(Compiler compiler)
	{
		if (!compiler.isInPanicMode)
			return;

		while (compiler.currentToken.kind != Token.EndKind)
		{
			switch ((TokenKind)compiler.currentToken.kind)
			{
			case TokenKind.Function:
				compiler.isInPanicMode = false;
				return;
			default:
				break;
			}

			compiler.Next();
		}
	}

	public void OnParseWithPrecedence(Compiler compiler, int precedence)
	{
		var canAssign = precedence <= (int)Precedence.Assignment;
		if (canAssign && compiler.Match((int)TokenKind.Equal))
		{
			compiler.AddHardError(compiler.previousToken.slice, "Invalid assignment target");
			Expression(compiler);
		}
	}

	public void FunctionDeclaration(Compiler compiler)
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
				var paramType = this.ConsumeType(compiler, "Expected parameter type", 0);

				if (declaration.parameterCount >= MaxParamCount)
				{
					compiler.AddSoftError(paramSlice, "Function can not have more than {0} parameters", MaxParamCount);
					continue;
				}

				compiler.localVariables.PushBack(new LocalVariable(
					paramSlice,
					compiler.scopeDepth,
					paramType,
					false,
					true
				));

				declaration.AddParam(paramType);
			} while (compiler.Match((int)TokenKind.Comma));
		}
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (compiler.Match((int)TokenKind.Colon))
			declaration.returnType = this.ConsumeType(compiler, "Expected function return type", 0);

		compiler.EndFunctionDeclaration(slice, declaration);
		functionReturnTypeStack.PushBack(declaration.returnType);

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' before function body");

		if (declaration.returnType == ValueType.Unit)
		{
			BlockStatement(compiler);
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(compiler, (int)Precedence.None);
			var type = compiler.typeStack.PopLast();
			if (declaration.returnType != type)
				compiler.AddSoftError(compiler.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", declaration.returnType.ToString(compiler.chunk), type.ToString(compiler.chunk));
		}

		compiler.EmitInstruction(Instruction.Return);

		functionReturnTypeStack.PopLast();
		compiler.localVariables.count -= declaration.parameterCount;
	}

	public Option<ValueType> Statement(Compiler compiler)
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
			var type = ReturnStatement(compiler);
			return Option.Some(type);
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

	public ValueType ExpressionStatement(Compiler compiler)
	{
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Pop);
		return compiler.typeStack.PopLast();
	}

	public void BlockStatement(Compiler compiler)
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

	private int VariableDeclaration(Compiler compiler, bool mutable)
	{
		compiler.Consume((int)TokenKind.Identifier, "Expected variable name");
		var slice = compiler.previousToken.slice;

		compiler.Consume((int)TokenKind.Equal, "Expected assignment");
		Expression(compiler);

		return compiler.DeclareLocalVariable(slice, mutable);
	}

	public void WhileStatement(Compiler compiler)
	{
		var loopJump = compiler.BeginEmitBackwardJump();
		Expression(compiler);

		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression as while condition");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		LangCompilerHelper.BeginLoop(this);
		BlockStatement(compiler);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		LangCompilerHelper.EndLoop(this, compiler);
	}

	public void ForStatement(Compiler compiler)
	{
		var scope = compiler.BeginScope();
		var itVarIndex = VariableDeclaration(compiler, true);
		compiler.localVariables.buffer[itVarIndex].isUsed = true;
		var itVar = compiler.localVariables.buffer[itVarIndex];
		if (itVar.type != ValueType.Int)
			compiler.AddSoftError(itVar.slice, "Expected variable of type int in for loop");

		compiler.Consume((int)TokenKind.Comma, "Expected comma after begin expression");
		Expression(compiler);
		var toVarIndex = compiler.DeclareLocalVariable(compiler.previousToken.slice, false);
		compiler.localVariables.buffer[toVarIndex].isUsed = true;
		if (compiler.localVariables.buffer[toVarIndex].type != ValueType.Int)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected expression of type int");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var loopJump = compiler.BeginEmitBackwardJump();
		compiler.EmitInstruction(Instruction.ForLoopCheck);
		compiler.EmitByte((byte)itVarIndex);

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		LangCompilerHelper.BeginLoop(this);
		BlockStatement(compiler);

		compiler.EmitInstruction(Instruction.IncrementLocal);
		compiler.EmitByte((byte)itVarIndex);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		LangCompilerHelper.EndLoop(this, compiler);

		compiler.EndScope(scope);
	}

	private void BreakStatement(Compiler compiler)
	{
		var breakJump = compiler.BeginEmitForwardJump(Instruction.JumpForward);

		var nestingCount = 1;
		if (compiler.Match((int)TokenKind.Colon))
		{
			if (compiler.Consume((int)TokenKind.IntLiteral, "Expected loop nesting count as int literal"))
				nestingCount = CompilerHelper.GetInt(compiler);

			if (nestingCount <= 0)
			{
				compiler.AddSoftError(compiler.previousToken.slice, "Nesting count must be at least 1");
				nestingCount = 1;
			}

			if (nestingCount > loopNesting)
			{
				compiler.AddSoftError(compiler.previousToken.slice, "Nesting count can not exceed loop nesting count which is {0}", loopNesting);
				nestingCount = loopNesting;
			}
		}

		if (!LangCompilerHelper.BreakLoop(this, nestingCount, breakJump))
		{
			compiler.AddSoftError(compiler.previousToken.slice, "Not inside a loop");
			return;
		}
	}

	private ValueType ReturnStatement(Compiler compiler)
	{
		var expectedType = functionReturnTypeStack.buffer[functionReturnTypeStack.count - 1];
		var returnType = ValueType.Unit;

		if (compiler.Match((int)TokenKind.Colon))
		{
			Expression(compiler);
			returnType = compiler.typeStack.PopLast();
		}
		else
		{
			compiler.EmitInstruction(Instruction.LoadUnit);
		}

		compiler.EmitInstruction(Instruction.Return);
		if (expectedType != returnType)
			compiler.AddSoftError(compiler.previousToken.slice, "Wrong return type. Expected {0}. Got {1}. Did you forget to add ':' before the expression?", expectedType.ToString(compiler.chunk), returnType.ToString(compiler.chunk));

		return returnType;
	}

	private void PrintStatement(Compiler compiler)
	{
		Expression(compiler);
		compiler.EmitInstruction(Instruction.Print);
		compiler.typeStack.PopLast();
	}

	public void Expression(Compiler compiler)
	{
		compiler.ParseWithPrecedence(rules, (int)Precedence.Assignment);
	}

	public void Grouping(Compiler compiler, int precedence)
	{
		Expression(compiler);
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public void Block(Compiler compiler, int precedence)
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
			compiler.chunk.bytes.count -= 1;

			var varCount = compiler.localVariables.count - scope.localVarStartIndex;
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
			compiler.typeStack.PushBack(maybeType.value);
		}
		else
		{
			compiler.typeStack.PushBack(ValueType.Unit);
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
	}

	public void If(Compiler compiler, int precedence)
	{
		Expression(compiler);

		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression as if condition");

		compiler.Consume((int)TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

		var elseJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		Block(compiler, precedence);
		var thenType = compiler.typeStack.PopLast();

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

			var elseType = compiler.typeStack.PopLast();
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
		compiler.typeStack.PushBack(thenType);
	}

	public void And(Compiler compiler, int precedence)
	{
		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression before and");

		var jump = compiler.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		compiler.EmitInstruction(Instruction.Pop);
		compiler.ParseWithPrecedence(rules, (int)Precedence.And);
		compiler.EndEmitForwardJump(jump);

		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression after and");

		compiler.typeStack.PushBack(ValueType.Bool);
	}

	public void Or(Compiler compiler, int precedence)
	{
		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression before or");

		var jump = compiler.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		compiler.EmitInstruction(Instruction.Pop);
		compiler.ParseWithPrecedence(rules, (int)Precedence.Or);
		compiler.EndEmitForwardJump(jump);

		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.previousToken.slice, "Expected bool expression after or");

		compiler.typeStack.PushBack(ValueType.Bool);
	}

	public void Literal(Compiler compiler, int precedence)
	{
		switch ((TokenKind)compiler.previousToken.kind)
		{
		case TokenKind.True:
			compiler.EmitInstruction(Instruction.LoadTrue);
			compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.False:
			compiler.EmitInstruction(Instruction.LoadFalse);
			compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.IntLiteral:
			compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(compiler)),
				ValueType.Int
			);
			compiler.typeStack.PushBack(ValueType.Int);
			break;
		case TokenKind.FloatLiteral:
			compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(compiler)),
				ValueType.Float
			);
			compiler.typeStack.PushBack(ValueType.Float);
			break;
		case TokenKind.StringLiteral:
			compiler.EmitLoadStringLiteral(CompilerHelper.GetString(compiler));
			compiler.typeStack.PushBack(ValueType.String);
			break;
		default:
			compiler.AddHardError(
				compiler.previousToken.slice,
				string.Format("Expected literal. Got {0}", compiler.previousToken.kind)
			);
			compiler.typeStack.PushBack(ValueType.Unit);
			break;
		}
	}

	public void Variable(Compiler compiler, int precedence)
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
				if (!compiler.localVariables.buffer[index].isMutable)
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
					compiler.typeStack.PushBack(ValueType.Unit);
				}
				else
				{
					compiler.EmitLoadFunction(functionIndex);
					var function = compiler.chunk.functions.buffer[functionIndex];
					var type = ValueTypeHelper.SetIndex(ValueType.Function, function.typeIndex);
					compiler.typeStack.PushBack(type);
				}
			}
			else
			{
				ref var localVar = ref compiler.localVariables.buffer[index];
				localVar.isUsed = true;

				compiler.EmitInstruction(Instruction.LoadLocal);
				compiler.EmitByte((byte)index);
				compiler.typeStack.PushBack(localVar.type);
			}
		}
	}

	public void Call(Compiler compiler, int precedence)
	{
		var slice = compiler.previousToken.slice;

		var functionType = new FunctionType();
		var type = compiler.typeStack.PopLast();

		var hasFunction = false;
		if (ValueTypeHelper.GetKind(type) == ValueType.Function)
		{
			functionType = compiler.chunk.functionTypes.buffer[ValueTypeHelper.GetIndex(type)];
			hasFunction = true;
		}
		else
		{
			compiler.AddSoftError(slice, "Callee must be a function");
		}

		var argIndex = 0;
		if (!compiler.Check((int)TokenKind.CloseParenthesis))
		{
			do
			{
				Expression(compiler);
				var argType = compiler.typeStack.PopLast();
				if (
					hasFunction &&
					argIndex < functionType.parameters.length
				)
				{
					var paramType = compiler.chunk.functionTypeParams.buffer[functionType.parameters.index + argIndex];
					if (argType != paramType)
					{
						compiler.AddSoftError(
							compiler.previousToken.slice,
							"Wrong type for argument {0}. Expected {1}. Got {2}",
							argIndex + 1,
							paramType.ToString(compiler.chunk),
							argType.ToString(compiler.chunk)
						);
					}
				}

				argIndex += 1;
			} while (compiler.Match((int)TokenKind.Comma));
		}

		compiler.Consume((int)TokenKind.CloseParenthesis, "Expect ')' after function argument list");

		if (hasFunction && argIndex != functionType.parameters.length)
			compiler.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", functionType.parameters.length, argIndex);

		compiler.EmitInstruction(Instruction.Call);
		compiler.EmitByte((byte)(hasFunction ? functionType.parameters.length : 0));
		compiler.typeStack.PushBack(
			hasFunction ? functionType.returnType : ValueType.Unit
		);
	}

	public void Unary(Compiler compiler, int precedence)
	{
		var opToken = compiler.previousToken;

		compiler.ParseWithPrecedence(rules, (int)Precedence.Unary);
		var type = compiler.typeStack.PopLast();

		switch ((TokenKind)opToken.kind)
		{
		case TokenKind.Minus:
			switch (type)
			{
			case ValueType.Int:
				compiler.EmitInstruction(Instruction.NegateInt);
				compiler.typeStack.PushBack(ValueType.Int);
				break;
			case ValueType.Float:
				compiler.EmitInstruction(Instruction.NegateFloat);
				compiler.typeStack.PushBack(ValueType.Float);
				break;
			default:
				compiler.AddSoftError(opToken.slice, "Unary minus operator can only be applied to ints or floats");
				compiler.typeStack.PushBack(type);
				break;
			}
			break;
		case TokenKind.Bang:
			switch (type)
			{
			case ValueType.Bool:
				compiler.EmitInstruction(Instruction.Not);
				compiler.typeStack.PushBack(ValueType.Bool);
				break;
			case ValueType.Int:
			case ValueType.Float:
			case ValueType.String:
				compiler.EmitInstruction(Instruction.Pop);
				compiler.EmitInstruction(Instruction.LoadFalse);
				compiler.typeStack.PushBack(ValueType.Bool);
				break;
			default:
				compiler.AddSoftError(opToken.slice, "Not operator can only be applied to bools, ints, floats or strings");
				compiler.typeStack.PushBack(ValueType.Bool);
				break;
			}
			break;
		default:
			compiler.AddHardError(
					opToken.slice,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			compiler.typeStack.PushBack(ValueType.Unit);
			break;
		}
	}

	public void Binary(Compiler compiler, int precedence)
	{
		var opToken = compiler.previousToken;

		var opPrecedence = rules[opToken.kind].precedence;
		compiler.ParseWithPrecedence(rules, opPrecedence + 1);

		var bType = compiler.typeStack.PopLast();
		var aType = compiler.typeStack.PopLast();

		switch ((TokenKind)opToken.kind)
		{
		case TokenKind.Plus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.AddInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.AddFloat).typeStack.PushBack(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Plus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Minus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.SubtractInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.SubtractFloat).typeStack.PushBack(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Minus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Asterisk:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.MultiplyInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.MultiplyFloat).typeStack.PushBack(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Multiply operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Slash:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.DivideInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.DivideFloat).typeStack.PushBack(ValueType.Float);
			else
				compiler.AddSoftError(opToken.slice, "Divide operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.EqualEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken.slice, "Equal operator can only be applied to same type values");
				compiler.typeStack.PushBack(ValueType.Bool);
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
			compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.BangEqual:
			if (aType != bType)
			{
				compiler.AddSoftError(opToken.slice, "NotEqual operator can only be applied to same type values");
				compiler.typeStack.PushBack(ValueType.Bool);
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
			compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.Greater:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.GreaterInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.GreaterFloat);
			else
				compiler.AddSoftError(opToken.slice, "Greater operator can only be applied to ints or floats");
			compiler.typeStack.PushBack(ValueType.Bool);
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
			compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.Less:
			if (aType == ValueType.Int && bType == ValueType.Int)
				compiler.EmitInstruction(Instruction.LessInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				compiler.EmitInstruction(Instruction.LessFloat);
			else
				compiler.AddSoftError(opToken.slice, "Less operator can only be applied to ints or floats");
			compiler.typeStack.PushBack(ValueType.Bool);
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
			compiler.typeStack.PushBack(ValueType.Bool);
			break;
		default:
			return;
		}
	}
}