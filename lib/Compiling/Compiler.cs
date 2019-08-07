using System.Collections.Generic;

public sealed class Compiler
{
	public CompilerCommon common;
	public readonly ParseRule[] parseRules = new ParseRule[(int)TokenKind.COUNT];
	public Buffer<ValueType> functionReturnTypeStack = new Buffer<ValueType>(4);
	public Buffer<LoopBreak> loopBreaks = new Buffer<LoopBreak>(4);
	public int loopNesting;

	public Compiler()
	{
		ParseRules.InitRulesFor(this);

		var tokenizer = new Tokenizer(PepperScanners.scanners);
		var parser = new Parser(tokenizer, (s, m) => common.AddHardError(s, m));
		common = new CompilerCommon(parser);
	}

	public Result<ByteCodeChunk, List<CompileError>> Compile(string source)
	{
		common.Reset();
		common.parser.Reset();
		common.parser.tokenizer.Reset(source);

		common.parser.Next();

		while (!common.parser.Match(TokenKind.End))
			Declaration();

		common.EmitInstruction(Instruction.Halt);

		if (common.errors.Count > 0)
			return Result.Error(common.errors);
		return Result.Ok(common.chunk);
	}

	public Result<ByteCodeChunk, List<CompileError>> CompileExpression(string source)
	{
		common.parser.Next();
		Expression();
		common.EmitInstruction(Instruction.Halt);

		if (common.errors.Count > 0)
			return Result.Error(common.errors);
		return Result.Ok(common.chunk);
	}

	private void Syncronize()
	{
		if (!common.isInPanicMode)
			return;

		while (common.parser.currentToken.kind != TokenKind.End)
		{
			switch ((TokenKind)common.parser.currentToken.kind)
			{
			case TokenKind.Function:
				common.isInPanicMode = false;
				return;
			default:
				break;
			}

			common.parser.Next();
		}
	}

	public void Declaration()
	{
		if (common.parser.Match(TokenKind.Function))
			FunctionDeclaration();
		else if (common.parser.Match(TokenKind.Struct))
			StructDeclaration();
		else
			common.AddHardError(common.parser.previousToken.slice, "Expected function or struct declaration");
		Syncronize();
	}

	public void FunctionDeclaration()
	{
		common.parser.Consume(TokenKind.Identifier, "Expected function name");
		ConsumeFunction(common.parser.previousToken.slice);
	}

	public void FunctionExpression(Precedence precedence)
	{
		ConsumeFunction(new Slice());
		var functionIndex = common.chunk.functions.count - 1;
		var function = common.chunk.functions.buffer[functionIndex];

		common.EmitLoadFunction(functionIndex);
		common.typeStack.PushBack(ValueTypeHelper.SetIndex(ValueType.Function, function.typeIndex));
	}

	private void ConsumeFunction(Slice slice)
	{
		const int MaxParamCount = 8;

		var source = common.parser.tokenizer.source;
		var declaration = common.BeginFunctionDeclaration();
		var paramStartIndex = common.localVariables.count;

		common.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name");
		if (!common.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				common.parser.Consume(TokenKind.Identifier, "Expected parameter name");
				var paramSlice = common.parser.previousToken.slice;
				common.parser.Consume(TokenKind.Colon, "Expected ':' after parameter name");
				var paramType = this.ConsumeType("Expected parameter type", 0);

				if (declaration.parameterCount >= MaxParamCount)
				{
					common.AddSoftError(paramSlice, "Function can not have more than {0} parameters", MaxParamCount);
					continue;
				}

				var hasDuplicate = false;
				for (var i = 0; i < declaration.parameterCount; i++)
				{
					var otherSlice = common.localVariables.buffer[paramStartIndex + i].slice;
					if (CompilerHelper.AreEqual(source, paramSlice, otherSlice))
					{
						hasDuplicate = true;
						break;
					}
				}

				if (hasDuplicate)
				{
					common.AddSoftError(paramSlice, "Function already has a parameter with this name");
					continue;
				}

				common.AddLocalVariable(paramSlice, paramType, false, true);
				declaration.AddParam(paramType);
			} while (common.parser.Match(TokenKind.Comma));
		}
		common.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (common.parser.Match(TokenKind.Colon))
			declaration.returnType = this.ConsumeType("Expected function return type", 0);

		common.EndFunctionDeclaration(declaration, slice);
		functionReturnTypeStack.PushBack(declaration.returnType);

		common.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before function body");

		if (declaration.returnType == ValueType.Unit)
		{
			BlockStatement();
			common.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(Precedence.None);
			var type = common.typeStack.PopLast();
			if (declaration.returnType != type)
				common.AddSoftError(common.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", declaration.returnType.ToString(common.chunk), type.ToString(common.chunk));
		}

		common.EmitInstruction(Instruction.Return);

		functionReturnTypeStack.PopLast();
		common.localVariables.count -= declaration.parameterCount;
	}

	public void StructDeclaration()
	{
		common.parser.Consume(TokenKind.Identifier, "Expected struct name");
		var slice = common.parser.previousToken.slice;

		var source = common.parser.tokenizer.source;
		var declaration = common.BeginStructDeclaration();
		var fieldStartIndex = common.chunk.structTypeFields.count;

		common.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before struct fields");
		while (
			!common.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!common.parser.Check(TokenKind.End)
		)
		{
			common.parser.Consume(TokenKind.Identifier, "Expected field name");
			var fieldSlice = common.parser.previousToken.slice;
			common.parser.Consume(TokenKind.Colon, "Expected ':' after field name");
			var fieldType = this.ConsumeType("Expected field type", 0);

			var hasDuplicate = false;
			for (var i = 0; i < declaration.fieldCount; i++)
			{
				var otherName = common.chunk.structTypeFields.buffer[fieldStartIndex + i].name;
				if (CompilerHelper.AreEqual(source, fieldSlice, otherName))
				{
					hasDuplicate = true;
					break;
				}
			}

			if (hasDuplicate)
			{
				common.AddSoftError(fieldSlice, "Struct already has a field with this name");
				continue;
			}

			var fieldName = CompilerHelper.GetSlice(common, fieldSlice);
			declaration.AddField(fieldName, fieldType);
		}
		common.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct fields");

		common.EndStructDeclaration(declaration, slice);
	}

	public Option<ValueType> Statement()
	{
		if (common.parser.Match(TokenKind.OpenCurlyBrackets))
		{
			BlockStatement();
			return Option.None;
		}
		else if (common.parser.Match(TokenKind.Let))
		{
			VariableDeclaration(false);
			return Option.None;
		}
		else if (common.parser.Match(TokenKind.Mut))
		{
			VariableDeclaration(true);
			return Option.None;
		}
		else if (common.parser.Match(TokenKind.While))
		{
			WhileStatement();
			return Option.None;
		}
		else if (common.parser.Match(TokenKind.For))
		{
			ForStatement();
			return Option.None;
		}
		else if (common.parser.Match(TokenKind.Break))
		{
			BreakStatement();
			return Option.None;
		}
		else if (common.parser.Match(TokenKind.Return))
		{
			var type = ReturnStatement();
			return Option.Some(type);
		}
		else if (common.parser.Match(TokenKind.Print))
		{
			PrintStatement();
			return Option.None;
		}
		else
		{
			var type = ExpressionStatement();
			return Option.Some(type);
		}
	}

	public ValueType ExpressionStatement()
	{
		Expression();
		common.EmitInstruction(Instruction.Pop);
		return common.typeStack.count > 0 ?
			common.typeStack.PopLast() :
			ValueType.Unit;
	}

	public void BlockStatement()
	{
		var scope = common.BeginScope();
		while (
			!common.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!common.parser.Check(TokenKind.End)
		)
		{
			Statement();
		}

		common.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");
		common.EndScope(scope);
	}

	private int VariableDeclaration(bool mutable)
	{
		common.parser.Consume(TokenKind.Identifier, "Expected variable name");
		var slice = common.parser.previousToken.slice;

		common.parser.Consume(TokenKind.Equal, "Expected assignment");
		Expression();

		return common.DeclareLocalVariable(slice, mutable);
	}

	public void WhileStatement()
	{
		var loopJump = common.BeginEmitBackwardJump();
		Expression();

		if (common.typeStack.PopLast() != ValueType.Bool)
			common.AddSoftError(common.parser.previousToken.slice, "Expected bool expression as while condition");

		common.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var breakJump = common.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		this.BeginLoop();
		BlockStatement();

		common.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		common.EndEmitForwardJump(breakJump);
		this.EndLoop();
	}

	public void ForStatement()
	{
		var scope = common.BeginScope();
		var itVarIndex = VariableDeclaration(true);
		common.localVariables.buffer[itVarIndex].isUsed = true;
		var itVar = common.localVariables.buffer[itVarIndex];
		if (itVar.type != ValueType.Int)
			common.AddSoftError(itVar.slice, "Expected variable of type int in for loop");

		common.parser.Consume(TokenKind.Comma, "Expected comma after begin expression");
		Expression();
		var toVarIndex = common.DeclareLocalVariable(common.parser.previousToken.slice, false);
		common.localVariables.buffer[toVarIndex].isUsed = true;
		if (common.localVariables.buffer[toVarIndex].type != ValueType.Int)
			common.AddSoftError(common.parser.previousToken.slice, "Expected expression of type int");

		common.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var loopJump = common.BeginEmitBackwardJump();
		common.EmitInstruction(Instruction.ForLoopCheck);
		common.EmitByte((byte)itVar.stackIndex);

		var breakJump = common.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		this.BeginLoop();
		BlockStatement();

		common.EmitInstruction(Instruction.IncrementLocalInt);
		common.EmitByte((byte)itVar.stackIndex);

		common.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		common.EndEmitForwardJump(breakJump);
		this.EndLoop();

		common.EndScope(scope);
	}

	private void BreakStatement()
	{
		var breakJump = common.BeginEmitForwardJump(Instruction.JumpForward);

		var nestingCount = 1;
		if (common.parser.Match(TokenKind.IntLiteral))
		{
			nestingCount = CompilerHelper.GetInt(common);

			if (nestingCount <= 0)
			{
				common.AddSoftError(common.parser.previousToken.slice, "Nesting count must be at least 1");
				nestingCount = 1;
			}

			if (nestingCount > loopNesting)
			{
				common.AddSoftError(common.parser.previousToken.slice, "Nesting count can not exceed loop nesting count which is {0}", loopNesting);
				nestingCount = loopNesting;
			}
		}

		if (!this.BreakLoop(nestingCount, breakJump))
		{
			common.AddSoftError(common.parser.previousToken.slice, "Not inside a loop");
			return;
		}
	}

	private ValueType ReturnStatement()
	{
		var expectedType = functionReturnTypeStack.buffer[functionReturnTypeStack.count - 1];
		var returnType = ValueType.Unit;

		if (expectedType != ValueType.Unit)
		{
			Expression();
			if (common.typeStack.count > 0)
				returnType = common.typeStack.PopLast();
		}
		else
		{
			common.EmitInstruction(Instruction.LoadUnit);
		}

		common.EmitInstruction(Instruction.Return);
		if (expectedType != returnType)
			common.AddSoftError(common.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", expectedType.ToString(common.chunk), returnType.ToString(common.chunk));

		return returnType;
	}

	private void PrintStatement()
	{
		Expression();
		common.EmitInstruction(Instruction.Print);
		common.typeStack.PopLast();
	}

	public void Expression()
	{
		this.ParseWithPrecedence(Precedence.Assignment);
	}

	public void Grouping(Precedence precedence)
	{
		Expression();
		common.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public void Block(Precedence precedence)
	{
		var scope = common.BeginScope();
		var maybeType = new Option<ValueType>();

		while (
			!common.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!common.parser.Check(TokenKind.End)
		)
		{
			maybeType = Statement();
		}

		if (maybeType.isSome)
		{
			common.chunk.bytes.count -= 1;

			var varCount = common.localVariables.count - scope.localVarStartIndex;
			if (varCount > 0)
			{
				common.EmitInstruction(Instruction.CopyTo);
				common.EmitByte((byte)varCount);
			}
		}

		common.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		common.EndScope(scope);

		if (maybeType.isSome)
		{
			common.typeStack.PushBack(maybeType.value);
		}
		else
		{
			common.typeStack.PushBack(ValueType.Unit);
			common.EmitInstruction(Instruction.LoadUnit);
		}
	}

	public void If(Precedence precedence)
	{
		Expression();

		if (common.typeStack.PopLast() != ValueType.Bool)
			common.AddSoftError(common.parser.previousToken.slice, "Expected bool expression as if condition");

		common.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

		var elseJump = common.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		Block(precedence);
		var thenType = common.typeStack.PopLast();

		var thenJump = common.BeginEmitForwardJump(Instruction.JumpForward);
		common.EndEmitForwardJump(elseJump);

		if (common.parser.Match(TokenKind.Else))
		{
			if (common.parser.Match(TokenKind.If))
			{
				If(precedence);
			}
			else
			{
				common.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after else");
				Block(precedence);
			}

			var elseType = common.typeStack.PopLast();
			if (thenType != elseType)
				common.AddSoftError(common.parser.previousToken.slice, "If expression must produce values of the same type on both branches. Found types: {0} and {1}", thenType, elseType);
		}
		else
		{
			common.EmitInstruction(Instruction.LoadUnit);
			if (thenType != ValueType.Unit)
				common.AddSoftError(common.parser.previousToken.slice, "If expression must not produce a value when there is no else branch. Found type: {0}. Try ending with '{}'", thenType);
		}

		common.EndEmitForwardJump(thenJump);
		common.typeStack.PushBack(thenType);
	}

	public void And(Precedence precedence)
	{
		if (common.typeStack.PopLast() != ValueType.Bool)
			common.AddSoftError(common.parser.previousToken.slice, "Expected bool expression before and");

		var jump = common.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		common.EmitInstruction(Instruction.Pop);
		this.ParseWithPrecedence(Precedence.And);
		common.EndEmitForwardJump(jump);

		if (common.typeStack.PopLast() != ValueType.Bool)
			common.AddSoftError(common.parser.previousToken.slice, "Expected bool expression after and");

		common.typeStack.PushBack(ValueType.Bool);
	}

	public void Or(Precedence precedence)
	{
		if (common.typeStack.PopLast() != ValueType.Bool)
			common.AddSoftError(common.parser.previousToken.slice, "Expected bool expression before or");

		var jump = common.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		common.EmitInstruction(Instruction.Pop);
		this.ParseWithPrecedence(Precedence.Or);
		common.EndEmitForwardJump(jump);

		if (common.typeStack.PopLast() != ValueType.Bool)
			common.AddSoftError(common.parser.previousToken.slice, "Expected bool expression after or");

		common.typeStack.PushBack(ValueType.Bool);
	}

	public void Literal(Precedence precedence)
	{
		switch ((TokenKind)common.parser.previousToken.kind)
		{
		case TokenKind.True:
			common.EmitInstruction(Instruction.LoadTrue);
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.False:
			common.EmitInstruction(Instruction.LoadFalse);
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.IntLiteral:
			common.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(common)),
				ValueType.Int
			);
			common.typeStack.PushBack(ValueType.Int);
			break;
		case TokenKind.FloatLiteral:
			common.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(common)),
				ValueType.Float
			);
			common.typeStack.PushBack(ValueType.Float);
			break;
		case TokenKind.StringLiteral:
			common.EmitLoadStringLiteral(CompilerHelper.GetString(common));
			common.typeStack.PushBack(ValueType.String);
			break;
		default:
			common.AddHardError(
				common.parser.previousToken.slice,
				string.Format("Expected literal. Got {0}", common.parser.previousToken.kind)
			);
			common.typeStack.PushBack(ValueType.Unit);
			break;
		}
	}

	public void Variable(Precedence precedence)
	{
		var slice = common.parser.previousToken.slice;
		var index = common.ResolveToLocalVariableIndex();

		var canAssign = precedence <= Precedence.Assignment;
		if (canAssign && common.parser.Match(TokenKind.Equal))
		{
			Expression();

			if (index < 0)
			{
				common.AddSoftError(slice, "Can not write to undeclared variable. Try declaring it with 'let' or 'mut'");
			}
			else
			{
				var localVar = common.localVariables.buffer[index];
				if (!localVar.isMutable)
					common.AddSoftError(slice, "Can not write to immutable variable. Try using 'mut' instead of 'let'");

				common.EmitInstruction(Instruction.AssignLocal);
				common.EmitByte((byte)localVar.stackIndex);
			}
		}
		else
		{
			if (index < 0)
			{
				var functionIndex = common.ResolveToFunctionIndex();
				if (functionIndex < 0)
				{
					common.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let'");
					common.typeStack.PushBack(ValueType.Unit);
				}
				else
				{
					common.EmitLoadFunction(functionIndex);
					var function = common.chunk.functions.buffer[functionIndex];
					var type = ValueTypeHelper.SetIndex(ValueType.Function, function.typeIndex);
					common.typeStack.PushBack(type);
				}
			}
			else
			{
				ref var localVar = ref common.localVariables.buffer[index];
				localVar.isUsed = true;

				common.EmitInstruction(Instruction.LoadLocal);
				common.EmitByte((byte)localVar.stackIndex);
				common.typeStack.PushBack(localVar.type);
			}
		}
	}

	public void Call(Precedence precedence)
	{
		var slice = common.parser.previousToken.slice;

		var functionType = new FunctionType();
		var type = common.typeStack.PopLast();

		var hasFunction = false;
		if (ValueTypeHelper.GetKind(type) == ValueType.Function)
		{
			functionType = common.chunk.functionTypes.buffer[ValueTypeHelper.GetIndex(type)];
			hasFunction = true;
		}
		else
		{
			common.AddSoftError(slice, "Callee must be a function");
		}

		var argIndex = 0;
		if (!common.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				Expression();
				var argType = common.typeStack.PopLast();
				if (
					hasFunction &&
					argIndex < functionType.parameters.length
				)
				{
					var paramType = common.chunk.functionTypeParams.buffer[functionType.parameters.index + argIndex];
					if (argType != paramType)
					{
						common.AddSoftError(
							common.parser.previousToken.slice,
							"Wrong type for argument {0}. Expected {1}. Got {2}",
							argIndex + 1,
							paramType.ToString(common.chunk),
							argType.ToString(common.chunk)
						);
					}
				}

				argIndex += 1;
			} while (common.parser.Match(TokenKind.Comma));
		}

		common.parser.Consume(TokenKind.CloseParenthesis, "Expect ')' after function argument list");

		if (hasFunction && argIndex != functionType.parameters.length)
			common.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", functionType.parameters.length, argIndex);

		common.EmitInstruction(Instruction.Call);
		common.EmitByte((byte)(hasFunction ? functionType.parameters.length : 0));
		common.typeStack.PushBack(
			hasFunction ? functionType.returnType : ValueType.Unit
		);
	}

	public void Unary(Precedence precedence)
	{
		var opToken = common.parser.previousToken;

		this.ParseWithPrecedence(Precedence.Unary);
		var type = common.typeStack.PopLast();

		switch ((TokenKind)opToken.kind)
		{
		case TokenKind.Minus:
			switch (type)
			{
			case ValueType.Int:
				common.EmitInstruction(Instruction.NegateInt);
				common.typeStack.PushBack(ValueType.Int);
				break;
			case ValueType.Float:
				common.EmitInstruction(Instruction.NegateFloat);
				common.typeStack.PushBack(ValueType.Float);
				break;
			default:
				common.AddSoftError(opToken.slice, "Unary minus operator can only be applied to ints or floats");
				common.typeStack.PushBack(type);
				break;
			}
			break;
		case TokenKind.Bang:
			switch (type)
			{
			case ValueType.Bool:
				common.EmitInstruction(Instruction.Not);
				common.typeStack.PushBack(ValueType.Bool);
				break;
			case ValueType.Int:
			case ValueType.Float:
			case ValueType.String:
				common.EmitInstruction(Instruction.Pop);
				common.EmitInstruction(Instruction.LoadFalse);
				common.typeStack.PushBack(ValueType.Bool);
				break;
			default:
				common.AddSoftError(opToken.slice, "Not operator can only be applied to bools, ints, floats or strings");
				common.typeStack.PushBack(ValueType.Bool);
				break;
			}
			break;
		default:
			common.AddHardError(
					opToken.slice,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			common.typeStack.PushBack(ValueType.Unit);
			break;
		}
	}

	public void Binary(Precedence precedence)
	{
		var opToken = common.parser.previousToken;

		var opPrecedence = parseRules[(int)opToken.kind].precedence;
		this.ParseWithPrecedence(opPrecedence + 1);

		var bType = common.typeStack.PopLast();
		var aType = common.typeStack.PopLast();

		switch ((TokenKind)opToken.kind)
		{
		case TokenKind.Plus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common.EmitInstruction(Instruction.AddInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common.EmitInstruction(Instruction.AddFloat).typeStack.PushBack(ValueType.Float);
			else
				common.AddSoftError(opToken.slice, "Plus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Minus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common.EmitInstruction(Instruction.SubtractInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common.EmitInstruction(Instruction.SubtractFloat).typeStack.PushBack(ValueType.Float);
			else
				common.AddSoftError(opToken.slice, "Minus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Asterisk:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common.EmitInstruction(Instruction.MultiplyInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common.EmitInstruction(Instruction.MultiplyFloat).typeStack.PushBack(ValueType.Float);
			else
				common.AddSoftError(opToken.slice, "Multiply operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Slash:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common.EmitInstruction(Instruction.DivideInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common.EmitInstruction(Instruction.DivideFloat).typeStack.PushBack(ValueType.Float);
			else
				common.AddSoftError(opToken.slice, "Divide operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.EqualEqual:
			if (aType != bType)
			{
				common.AddSoftError(opToken.slice, "Equal operator can only be applied to same type values");
				common.typeStack.PushBack(ValueType.Bool);
				break;
			}

			switch (aType)
			{
			case ValueType.Bool:
				common.EmitInstruction(Instruction.EqualBool);
				break;
			case ValueType.Int:
				common.EmitInstruction(Instruction.EqualInt);
				break;
			case ValueType.Float:
				common.EmitInstruction(Instruction.EqualFloat);
				break;
			case ValueType.String:
				common.EmitInstruction(Instruction.EqualString);
				break;
			default:
				common.AddSoftError(opToken.slice, "Equal operator can only be applied to bools, ints and floats");
				break;
			}
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.BangEqual:
			if (aType != bType)
			{
				common.AddSoftError(opToken.slice, "NotEqual operator can only be applied to same type values");
				common.typeStack.PushBack(ValueType.Bool);
				break;
			}

			switch (aType)
			{
			case ValueType.Bool:
				common.EmitInstruction(Instruction.EqualBool);
				break;
			case ValueType.Int:
				common.EmitInstruction(Instruction.EqualInt);
				break;
			case ValueType.Float:
				common.EmitInstruction(Instruction.EqualFloat);
				break;
			case ValueType.String:
				common.EmitInstruction(Instruction.EqualString);
				break;
			default:
				common.AddSoftError(opToken.slice, "NotEqual operator can only be applied to bools, ints and floats");
				break;
			}
			common.EmitInstruction(Instruction.Not);
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.Greater:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common.EmitInstruction(Instruction.GreaterInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common.EmitInstruction(Instruction.GreaterFloat);
			else
				common.AddSoftError(opToken.slice, "Greater operator can only be applied to ints or floats");
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.GreaterEqual:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common
					.EmitInstruction(Instruction.LessInt)
					.EmitInstruction(Instruction.Not);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common
					.EmitInstruction(Instruction.LessFloat)
					.EmitInstruction(Instruction.Not);
			else
				common.AddSoftError(opToken.slice, "GreaterOrEqual operator can only be applied to ints or floats");
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.Less:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common.EmitInstruction(Instruction.LessInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common.EmitInstruction(Instruction.LessFloat);
			else
				common.AddSoftError(opToken.slice, "Less operator can only be applied to ints or floats");
			common.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.LessEqual:
			if (aType == ValueType.Int && bType == ValueType.Int)
				common
					.EmitInstruction(Instruction.GreaterInt)
					.EmitInstruction(Instruction.Not);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				common
					.EmitInstruction(Instruction.GreaterFloat)
					.EmitInstruction(Instruction.Not);
			else
				common.AddSoftError(opToken.slice, "LessOrEqual operator can only be applied to ints or floats");
			common.typeStack.PushBack(ValueType.Bool);
			break;
		default:
			return;
		}
	}
}