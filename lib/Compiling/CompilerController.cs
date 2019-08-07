using System.Collections.Generic;

public sealed class CompilerController
{
	public Compiler compiler = new Compiler();
	public readonly ParseRules parseRules = new ParseRules();

	public Result<ByteCodeChunk, List<CompileError>> Compile(string source)
	{
		compiler.Reset(source);
		compiler.parser.Next();

		while (!compiler.parser.Match(TokenKind.End))
			Declaration();

		compiler.EmitInstruction(Instruction.Halt);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.chunk);
	}

	public Result<ByteCodeChunk, List<CompileError>> CompileExpression(string source)
	{
		compiler.parser.Next();
		Expression(this);
		compiler.EmitInstruction(Instruction.Halt);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.chunk);
	}

	public static void ParseWithPrecedence(CompilerController self, Precedence precedence)
	{
		var parser = self.compiler.parser;
		parser.Next();
		if (parser.previousToken.kind == TokenKind.End)
			return;

		var prefixRule = self.parseRules.GetPrefixRule(parser.previousToken.kind);
		if (prefixRule == null)
		{
			self.compiler.AddHardError(parser.previousToken.slice, "Expected expression");
			return;
		}
		prefixRule(self, precedence);

		while (
			parser.currentToken.kind != TokenKind.End &&
			precedence <= self.parseRules.GetPrecedence(parser.currentToken.kind)
		)
		{
			parser.Next();
			var infixRule = self.parseRules.GetInfixRule(parser.previousToken.kind);
			infixRule(self, precedence);
		}

		var canAssign = precedence <= Precedence.Assignment;
		if (canAssign && self.compiler.parser.Match(TokenKind.Equal))
		{
			self.compiler.AddHardError(self.compiler.parser.previousToken.slice, "Invalid assignment target");
			Expression(self);
		}
	}

	private void Syncronize()
	{
		if (!compiler.isInPanicMode)
			return;

		while (compiler.parser.currentToken.kind != TokenKind.End)
		{
			switch (compiler.parser.currentToken.kind)
			{
			case TokenKind.Function:
			case TokenKind.Struct:
				compiler.isInPanicMode = false;
				return;
			default:
				break;
			}

			compiler.parser.Next();
		}
	}

	public void Declaration()
	{
		if (compiler.parser.Match(TokenKind.Function))
			FunctionDeclaration();
		else if (compiler.parser.Match(TokenKind.Struct))
			StructDeclaration();
		else
			compiler.AddHardError(compiler.parser.previousToken.slice, "Expected function or struct declaration");
		Syncronize();
	}

	public void FunctionDeclaration()
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected function name");
		ConsumeFunction(compiler.parser.previousToken.slice);
	}

	public static void FunctionExpression(CompilerController self, Precedence precedence)
	{
		self.ConsumeFunction(new Slice());
		var functionIndex = self.compiler.chunk.functions.count - 1;
		var function = self.compiler.chunk.functions.buffer[functionIndex];

		self.compiler.EmitLoadFunction(functionIndex);
		self.compiler.typeStack.PushBack(ValueTypeHelper.SetIndex(ValueType.Function, function.typeIndex));
	}

	private void ConsumeFunction(Slice slice)
	{
		const int MaxParamCount = 8;

		var source = compiler.parser.tokenizer.source;
		var declaration = compiler.BeginFunctionDeclaration();
		var paramStartIndex = compiler.localVariables.count;

		compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name");
		if (!compiler.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				compiler.parser.Consume(TokenKind.Identifier, "Expected parameter name");
				var paramSlice = compiler.parser.previousToken.slice;
				compiler.parser.Consume(TokenKind.Colon, "Expected ':' after parameter name");
				var paramType = compiler.ConsumeType("Expected parameter type", 0);

				if (declaration.parameterCount >= MaxParamCount)
				{
					compiler.AddSoftError(paramSlice, "Function can not have more than {0} parameters", MaxParamCount);
					continue;
				}

				var hasDuplicate = false;
				for (var i = 0; i < declaration.parameterCount; i++)
				{
					var otherSlice = compiler.localVariables.buffer[paramStartIndex + i].slice;
					if (CompilerHelper.AreEqual(source, paramSlice, otherSlice))
					{
						hasDuplicate = true;
						break;
					}
				}

				if (hasDuplicate)
				{
					compiler.AddSoftError(paramSlice, "Function already has a parameter with this name");
					continue;
				}

				compiler.AddLocalVariable(paramSlice, paramType, false, true);
				declaration.AddParam(paramType);
			} while (compiler.parser.Match(TokenKind.Comma));
		}
		compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (compiler.parser.Match(TokenKind.Colon))
			declaration.returnType = compiler.ConsumeType("Expected function return type", 0);

		compiler.EndFunctionDeclaration(declaration, slice);
		compiler.functionReturnTypeStack.PushBack(declaration.returnType);

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before function body");

		if (declaration.returnType == ValueType.Unit)
		{
			BlockStatement();
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(this, Precedence.None);
			var type = compiler.typeStack.PopLast();
			if (declaration.returnType != type)
				compiler.AddSoftError(compiler.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", declaration.returnType.ToString(compiler.chunk), type.ToString(compiler.chunk));
		}

		compiler.EmitInstruction(Instruction.Return);

		compiler.functionReturnTypeStack.PopLast();
		compiler.localVariables.count -= declaration.parameterCount;
	}

	public void StructDeclaration()
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected struct name");
		var slice = compiler.parser.previousToken.slice;

		var source = compiler.parser.tokenizer.source;
		var declaration = compiler.BeginStructDeclaration();
		var fieldStartIndex = compiler.chunk.structTypeFields.count;

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before struct fields");
		while (
			!compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!compiler.parser.Check(TokenKind.End)
		)
		{
			compiler.parser.Consume(TokenKind.Identifier, "Expected field name");
			var fieldSlice = compiler.parser.previousToken.slice;
			compiler.parser.Consume(TokenKind.Colon, "Expected ':' after field name");
			var fieldType = compiler.ConsumeType("Expected field type", 0);

			var hasDuplicate = false;
			for (var i = 0; i < declaration.fieldCount; i++)
			{
				var otherName = compiler.chunk.structTypeFields.buffer[fieldStartIndex + i].name;
				if (CompilerHelper.AreEqual(source, fieldSlice, otherName))
				{
					hasDuplicate = true;
					break;
				}
			}

			if (hasDuplicate)
			{
				compiler.AddSoftError(fieldSlice, "Struct already has a field with this name");
				continue;
			}

			var fieldName = CompilerHelper.GetSlice(compiler, fieldSlice);
			declaration.AddField(fieldName, fieldType);
		}
		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct fields");

		compiler.EndStructDeclaration(declaration, slice);
	}

	public Option<ValueType> Statement()
	{
		if (compiler.parser.Match(TokenKind.OpenCurlyBrackets))
		{
			BlockStatement();
			return Option.None;
		}
		else if (compiler.parser.Match(TokenKind.Let))
		{
			VariableDeclaration(false);
			return Option.None;
		}
		else if (compiler.parser.Match(TokenKind.Mut))
		{
			VariableDeclaration(true);
			return Option.None;
		}
		else if (compiler.parser.Match(TokenKind.While))
		{
			WhileStatement();
			return Option.None;
		}
		else if (compiler.parser.Match(TokenKind.For))
		{
			ForStatement();
			return Option.None;
		}
		else if (compiler.parser.Match(TokenKind.Break))
		{
			BreakStatement();
			return Option.None;
		}
		else if (compiler.parser.Match(TokenKind.Return))
		{
			var type = ReturnStatement();
			return Option.Some(type);
		}
		else if (compiler.parser.Match(TokenKind.Print))
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
		Expression(this);
		var type = compiler.typeStack.count > 0 ?
			compiler.typeStack.PopLast() :
			ValueType.Unit;

		var size = compiler.chunk.GetTypeSize(type);

		if (size > 1)
			compiler.EmitInstruction(Instruction.PopMultiple).EmitByte((byte)size);
		else
			compiler.EmitInstruction(Instruction.Pop);

		return type;
	}

	public void BlockStatement()
	{
		var scope = compiler.BeginScope();
		while (
			!compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!compiler.parser.Check(TokenKind.End)
		)
		{
			Statement();
		}

		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");
		compiler.EndScope(scope);
	}

	private int VariableDeclaration(bool mutable)
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected variable name");
		var slice = compiler.parser.previousToken.slice;

		compiler.parser.Consume(TokenKind.Equal, "Expected assignment");
		Expression(this);

		return compiler.DeclareLocalVariable(slice, mutable);
	}

	public void WhileStatement()
	{
		var loopJump = compiler.BeginEmitBackwardJump();
		Expression(this);

		if (compiler.typeStack.PopLast() != ValueType.Bool)
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Expected bool expression as while condition");

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		compiler.BeginLoop();
		BlockStatement();

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		compiler.EndLoop();
	}

	public void ForStatement()
	{
		var scope = compiler.BeginScope();
		var itVarIndex = VariableDeclaration(true);
		compiler.localVariables.buffer[itVarIndex].isUsed = true;
		var itVar = compiler.localVariables.buffer[itVarIndex];
		if (itVar.type != ValueType.Int)
			compiler.AddSoftError(itVar.slice, "Expected variable of type int in for loop");

		compiler.parser.Consume(TokenKind.Comma, "Expected comma after begin expression");
		Expression(this);
		var toVarIndex = compiler.DeclareLocalVariable(compiler.parser.previousToken.slice, false);
		compiler.localVariables.buffer[toVarIndex].isUsed = true;
		if (compiler.localVariables.buffer[toVarIndex].type != ValueType.Int)
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Expected expression of type int");

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var loopJump = compiler.BeginEmitBackwardJump();
		compiler.EmitInstruction(Instruction.ForLoopCheck);
		compiler.EmitByte((byte)itVar.stackIndex);

		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		compiler.BeginLoop();
		BlockStatement();

		compiler.EmitInstruction(Instruction.IncrementLocalInt);
		compiler.EmitByte((byte)itVar.stackIndex);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		compiler.EndLoop();

		compiler.EndScope(scope);
	}

	private void BreakStatement()
	{
		var breakJump = compiler.BeginEmitForwardJump(Instruction.JumpForward);

		var nestingCount = 1;
		if (compiler.parser.Match(TokenKind.IntLiteral))
		{
			nestingCount = CompilerHelper.GetInt(compiler);

			if (nestingCount <= 0)
			{
				compiler.AddSoftError(compiler.parser.previousToken.slice, "Nesting count must be at least 1");
				nestingCount = 1;
			}

			if (nestingCount > compiler.loopNesting)
			{
				compiler.AddSoftError(compiler.parser.previousToken.slice, "Nesting count can not exceed loop nesting count which is {0}", compiler.loopNesting);
				nestingCount = compiler.loopNesting;
			}
		}

		if (!compiler.BreakLoop(nestingCount, breakJump))
		{
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Not inside a loop");
			return;
		}
	}

	private ValueType ReturnStatement()
	{
		var expectedType = compiler.functionReturnTypeStack.buffer[compiler.functionReturnTypeStack.count - 1];
		var returnType = ValueType.Unit;

		if (expectedType != ValueType.Unit)
		{
			Expression(this);
			if (compiler.typeStack.count > 0)
				returnType = compiler.typeStack.PopLast();
		}
		else
		{
			compiler.EmitInstruction(Instruction.LoadUnit);
		}

		compiler.EmitInstruction(Instruction.Return);
		if (expectedType != returnType)
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", expectedType.ToString(compiler.chunk), returnType.ToString(compiler.chunk));

		return returnType;
	}

	private void PrintStatement()
	{
		Expression(this);
		compiler.EmitInstruction(Instruction.Print);
		compiler.typeStack.PopLast();
	}

	public static void Expression(CompilerController self)
	{
		ParseWithPrecedence(self, Precedence.Assignment);
	}

	public static void Grouping(CompilerController self, Precedence precedence)
	{
		Expression(self);
		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void Block(CompilerController self, Precedence precedence)
	{
		var scope = self.compiler.BeginScope();
		var maybeType = new Option<ValueType>();

		while (
			!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			maybeType = self.Statement();
		}

		if (maybeType.isSome)
		{
			self.compiler.chunk.bytes.count -= 1;

			var varCount = self.compiler.localVariables.count - scope.localVarStartIndex;
			if (varCount > 0)
			{
				self.compiler.EmitInstruction(Instruction.CopyTo);
				self.compiler.EmitByte((byte)varCount);
			}
		}

		self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		self.compiler.EndScope(scope);

		if (maybeType.isSome)
		{
			self.compiler.typeStack.PushBack(maybeType.value);
		}
		else
		{
			self.compiler.typeStack.PushBack(ValueType.Unit);
			self.compiler.EmitInstruction(Instruction.LoadUnit);
		}
	}

	public static void If(CompilerController self, Precedence precedence)
	{
		Expression(self);

		if (self.compiler.typeStack.PopLast() != ValueType.Bool)
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression as if condition");

		self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

		var elseJump = self.compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		Block(self, precedence);
		var thenType = self.compiler.typeStack.PopLast();

		var thenJump = self.compiler.BeginEmitForwardJump(Instruction.JumpForward);
		self.compiler.EndEmitForwardJump(elseJump);

		if (self.compiler.parser.Match(TokenKind.Else))
		{
			if (self.compiler.parser.Match(TokenKind.If))
			{
				If(self, precedence);
			}
			else
			{
				self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after else");
				Block(self, precedence);
			}

			var elseType = self.compiler.typeStack.PopLast();
			if (thenType != elseType)
				self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "If expression must produce values of the same type on both branches. Found types: {0} and {1}", thenType, elseType);
		}
		else
		{
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			if (thenType != ValueType.Unit)
				self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "If expression must not produce a value when there is no else branch. Found type: {0}. Try ending with '{}'", thenType);
		}

		self.compiler.EndEmitForwardJump(thenJump);
		self.compiler.typeStack.PushBack(thenType);
	}

	public static void And(CompilerController self, Precedence precedence)
	{
		if (self.compiler.typeStack.PopLast() != ValueType.Bool)
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression before and");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		self.compiler.EmitInstruction(Instruction.Pop);
		ParseWithPrecedence(self, Precedence.And);
		self.compiler.EndEmitForwardJump(jump);

		if (self.compiler.typeStack.PopLast() != ValueType.Bool)
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression after and");

		self.compiler.typeStack.PushBack(ValueType.Bool);
	}

	public static void Or(CompilerController self, Precedence precedence)
	{
		if (self.compiler.typeStack.PopLast() != ValueType.Bool)
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression before or");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		self.compiler.EmitInstruction(Instruction.Pop);
		ParseWithPrecedence(self, Precedence.Or);
		self.compiler.EndEmitForwardJump(jump);

		if (self.compiler.typeStack.PopLast() != ValueType.Bool)
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression after or");

		self.compiler.typeStack.PushBack(ValueType.Bool);
	}

	public static void Literal(CompilerController self, Precedence precedence)
	{
		switch (self.compiler.parser.previousToken.kind)
		{
		case TokenKind.True:
			self.compiler.EmitInstruction(Instruction.LoadTrue);
			self.compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.False:
			self.compiler.EmitInstruction(Instruction.LoadFalse);
			self.compiler.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.IntLiteral:
			self.compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(self.compiler)),
				ValueType.Int
			);
			self.compiler.typeStack.PushBack(ValueType.Int);
			break;
		case TokenKind.FloatLiteral:
			self.compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(self.compiler)),
				ValueType.Float
			);
			self.compiler.typeStack.PushBack(ValueType.Float);
			break;
		case TokenKind.StringLiteral:
			self.compiler.EmitLoadStringLiteral(CompilerHelper.GetString(self.compiler));
			self.compiler.typeStack.PushBack(ValueType.String);
			break;
		default:
			self.compiler.AddHardError(
				self.compiler.parser.previousToken.slice,
				string.Format("Expected literal. Got {0}", self.compiler.parser.previousToken.kind)
			);
			self.compiler.typeStack.PushBack(ValueType.Unit);
			break;
		}
	}

	public static void Identifier(CompilerController self, Precedence precedence)
	{
		var slice = self.compiler.parser.previousToken.slice;
		var index = self.compiler.ResolveToLocalVariableIndex();

		var canAssign = precedence <= Precedence.Assignment;
		if (canAssign && self.compiler.parser.Match(TokenKind.Equal))
		{
			Expression(self);

			if (index < 0)
			{
				self.compiler.AddSoftError(slice, "Can not write to undeclared variable. Try declaring it with 'let' or 'mut'");
			}
			else
			{
				var localVar = self.compiler.localVariables.buffer[index];
				if (!localVar.isMutable)
					self.compiler.AddSoftError(slice, "Can not write to immutable variable. Try using 'mut' instead of 'let'");

				self.compiler.EmitInstruction(Instruction.AssignLocal);
				self.compiler.EmitByte((byte)localVar.stackIndex);
			}
		}
		else
		{
			if (index < 0)
			{
				if (self.compiler.ResolveToFunctionIndex(out var functionIndex))
				{
					self.compiler.EmitLoadFunction(functionIndex);
					var function = self.compiler.chunk.functions.buffer[functionIndex];
					var type = ValueTypeHelper.SetIndex(ValueType.Function, function.typeIndex);
					self.compiler.typeStack.PushBack(type);
				}
				else if (self.compiler.ResolveToStructTypeIndex(out var structIndex))
				{
					var structType = self.compiler.chunk.structTypes.buffer[structIndex];
					self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before struct initializer");
					for (var i = 0; i < structType.fields.length; i++)
					{
						var fieldIndex = structType.fields.index + i;
						var field = self.compiler.chunk.structTypeFields.buffer[fieldIndex];
						self.compiler.parser.Consume(TokenKind.Identifier, "Expected field name '{0}'. Don't forget to initialize them in order of declaration", field.name);
						self.compiler.parser.Consume(TokenKind.Equal, "Expected '=' after field name");

						Expression(self);
						var expressionType = self.compiler.typeStack.PopLast();
						if (expressionType != field.type)
						{
							self.compiler.AddSoftError(
								self.compiler.parser.previousToken.slice,
								"Wrong type for field '{0}' initializer. Expected {1}. Got {2}",
								field.name,
								field.type.ToString(self.compiler.chunk),
								expressionType.ToString(self.compiler.chunk)
							);
						}
					}
					self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct initializer");

					self.compiler.EmitConvertToStruct(structIndex);
					var type = ValueTypeHelper.SetIndex(ValueType.Struct, structIndex);
					self.compiler.typeStack.PushBack(type);
				}
				else
				{
					self.compiler.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let'");
					self.compiler.typeStack.PushBack(ValueType.Unit);
				}
			}
			else
			{
				ref var localVar = ref self.compiler.localVariables.buffer[index];
				localVar.isUsed = true;

				if (ValueTypeHelper.GetKind(localVar.type) == ValueType.Struct)
				{
					var structIndex = ValueTypeHelper.GetIndex(localVar.type);
					var structType = self.compiler.chunk.structTypes.buffer[structIndex];
					for (var i = 0; i < structType.fields.length; i++)
					{
						self.compiler.EmitInstruction(Instruction.LoadLocal);
						self.compiler.EmitByte((byte)(localVar.stackIndex + i));
					}
				}
				else
				{
					self.compiler.EmitInstruction(Instruction.LoadLocal);
					self.compiler.EmitByte((byte)localVar.stackIndex);
				}

				self.compiler.typeStack.PushBack(localVar.type);
			}
		}
	}

	public static void Call(CompilerController self, Precedence precedence)
	{
		var slice = self.compiler.parser.previousToken.slice;

		var functionType = new FunctionType();
		var type = self.compiler.typeStack.PopLast();

		var hasFunction = false;
		if (ValueTypeHelper.GetKind(type) == ValueType.Function)
		{
			functionType = self.compiler.chunk.functionTypes.buffer[ValueTypeHelper.GetIndex(type)];
			hasFunction = true;
		}
		else
		{
			self.compiler.AddSoftError(slice, "Callee must be a function");
		}

		var argIndex = 0;
		if (!self.compiler.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				Expression(self);
				var argType = self.compiler.typeStack.PopLast();
				if (
					hasFunction &&
					argIndex < functionType.parameters.length
				)
				{
					var paramType = self.compiler.chunk.functionTypeParams.buffer[functionType.parameters.index + argIndex];
					if (argType != paramType)
					{
						self.compiler.AddSoftError(
							self.compiler.parser.previousToken.slice,
							"Wrong type for argument {0}. Expected {1}. Got {2}",
							argIndex + 1,
							paramType.ToString(self.compiler.chunk),
							argType.ToString(self.compiler.chunk)
						);
					}
				}

				argIndex += 1;
			} while (self.compiler.parser.Match(TokenKind.Comma));
		}

		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expect ')' after function argument list");

		if (hasFunction && argIndex != functionType.parameters.length)
			self.compiler.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", functionType.parameters.length, argIndex);

		self.compiler.EmitInstruction(Instruction.Call);
		self.compiler.EmitByte((byte)(hasFunction ? functionType.parameters.length : 0));
		self.compiler.typeStack.PushBack(
			hasFunction ? functionType.returnType : ValueType.Unit
		);
	}

	public static void Unary(CompilerController self, Precedence precedence)
	{
		var opToken = self.compiler.parser.previousToken;

		ParseWithPrecedence(self, Precedence.Unary);
		var type = self.compiler.typeStack.PopLast();

		switch (opToken.kind)
		{
		case TokenKind.Minus:
			switch (type)
			{
			case ValueType.Int:
				self.compiler.EmitInstruction(Instruction.NegateInt);
				self.compiler.typeStack.PushBack(ValueType.Int);
				break;
			case ValueType.Float:
				self.compiler.EmitInstruction(Instruction.NegateFloat);
				self.compiler.typeStack.PushBack(ValueType.Float);
				break;
			default:
				self.compiler.AddSoftError(opToken.slice, "Unary minus operator can only be applied to ints or floats");
				self.compiler.typeStack.PushBack(type);
				break;
			}
			break;
		case TokenKind.Bang:
			switch (type)
			{
			case ValueType.Bool:
				self.compiler.EmitInstruction(Instruction.Not);
				self.compiler.typeStack.PushBack(ValueType.Bool);
				break;
			case ValueType.Int:
			case ValueType.Float:
			case ValueType.String:
				self.compiler.EmitInstruction(Instruction.Pop);
				self.compiler.EmitInstruction(Instruction.LoadFalse);
				self.compiler.typeStack.PushBack(ValueType.Bool);
				break;
			default:
				self.compiler.AddSoftError(opToken.slice, "Not operator can only be applied to bools, ints, floats or strings");
				self.compiler.typeStack.PushBack(ValueType.Bool);
				break;
			}
			break;
		default:
			self.compiler.AddHardError(
					opToken.slice,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			self.compiler.typeStack.PushBack(ValueType.Unit);
			break;
		}
	}

	public static void Binary(CompilerController self, Precedence precedence)
	{
		var c = self.compiler;
		var opToken = c.parser.previousToken;

		var opPrecedence = self.parseRules.GetPrecedence(opToken.kind);
		ParseWithPrecedence(self, opPrecedence + 1);

		var bType = c.typeStack.PopLast();
		var aType = c.typeStack.PopLast();

		switch (opToken.kind)
		{
		case TokenKind.Plus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c.EmitInstruction(Instruction.AddInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c.EmitInstruction(Instruction.AddFloat).typeStack.PushBack(ValueType.Float);
			else
				c.AddSoftError(opToken.slice, "Plus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Minus:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c.EmitInstruction(Instruction.SubtractInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c.EmitInstruction(Instruction.SubtractFloat).typeStack.PushBack(ValueType.Float);
			else
				c.AddSoftError(opToken.slice, "Minus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Asterisk:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c.EmitInstruction(Instruction.MultiplyInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c.EmitInstruction(Instruction.MultiplyFloat).typeStack.PushBack(ValueType.Float);
			else
				c.AddSoftError(opToken.slice, "Multiply operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Slash:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c.EmitInstruction(Instruction.DivideInt).typeStack.PushBack(ValueType.Int);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c.EmitInstruction(Instruction.DivideFloat).typeStack.PushBack(ValueType.Float);
			else
				c.AddSoftError(opToken.slice, "Divide operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.EqualEqual:
			if (aType != bType)
			{
				c.AddSoftError(opToken.slice, "Equal operator can only be applied to same type values");
				c.typeStack.PushBack(ValueType.Bool);
				break;
			}

			switch (aType)
			{
			case ValueType.Bool:
				c.EmitInstruction(Instruction.EqualBool);
				break;
			case ValueType.Int:
				c.EmitInstruction(Instruction.EqualInt);
				break;
			case ValueType.Float:
				c.EmitInstruction(Instruction.EqualFloat);
				break;
			case ValueType.String:
				c.EmitInstruction(Instruction.EqualString);
				break;
			default:
				c.AddSoftError(opToken.slice, "Equal operator can only be applied to bools, ints and floats");
				break;
			}
			c.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.BangEqual:
			if (aType != bType)
			{
				c.AddSoftError(opToken.slice, "NotEqual operator can only be applied to same type values");
				c.typeStack.PushBack(ValueType.Bool);
				break;
			}

			switch (aType)
			{
			case ValueType.Bool:
				c.EmitInstruction(Instruction.EqualBool);
				break;
			case ValueType.Int:
				c.EmitInstruction(Instruction.EqualInt);
				break;
			case ValueType.Float:
				c.EmitInstruction(Instruction.EqualFloat);
				break;
			case ValueType.String:
				c.EmitInstruction(Instruction.EqualString);
				break;
			default:
				c.AddSoftError(opToken.slice, "NotEqual operator can only be applied to bools, ints and floats");
				break;
			}
			c.EmitInstruction(Instruction.Not);
			c.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.Greater:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c.EmitInstruction(Instruction.GreaterInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c.EmitInstruction(Instruction.GreaterFloat);
			else
				c.AddSoftError(opToken.slice, "Greater operator can only be applied to ints or floats");
			c.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.GreaterEqual:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c
					.EmitInstruction(Instruction.LessInt)
					.EmitInstruction(Instruction.Not);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c
					.EmitInstruction(Instruction.LessFloat)
					.EmitInstruction(Instruction.Not);
			else
				c.AddSoftError(opToken.slice, "GreaterOrEqual operator can only be applied to ints or floats");
			c.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.Less:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c.EmitInstruction(Instruction.LessInt);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c.EmitInstruction(Instruction.LessFloat);
			else
				c.AddSoftError(opToken.slice, "Less operator can only be applied to ints or floats");
			c.typeStack.PushBack(ValueType.Bool);
			break;
		case TokenKind.LessEqual:
			if (aType == ValueType.Int && bType == ValueType.Int)
				c
					.EmitInstruction(Instruction.GreaterInt)
					.EmitInstruction(Instruction.Not);
			else if (aType == ValueType.Float && bType == ValueType.Float)
				c
					.EmitInstruction(Instruction.GreaterFloat)
					.EmitInstruction(Instruction.Not);
			else
				c.AddSoftError(opToken.slice, "LessOrEqual operator can only be applied to ints or floats");
			c.typeStack.PushBack(ValueType.Bool);
			break;
		default:
			return;
		}
	}
}