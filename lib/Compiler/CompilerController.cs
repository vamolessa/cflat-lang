using System.Text;

public sealed class CompilerController
{
	public enum StatementKind
	{
		Other,
		Expression,
		Return,
	}

	private struct Storage
	{
		public bool isValid;
		public int variableIndex;
		public ValueType type;
		public int stackIndex;
	}

	public readonly Compiler compiler = new Compiler();
	public readonly ParseRules parseRules = new ParseRules();
	public Buffer<System.Reflection.Assembly> searchingAssemblies = new Buffer<System.Reflection.Assembly>();

	public Buffer<CompileError> Compile(string source, ByteCodeChunk chunk, Mode mode)
	{
		compiler.Reset(source, chunk, mode);

		compiler.parser.Next();
		while (!compiler.parser.Match(TokenKind.End))
			Declaration();

		compiler.EmitInstruction(Instruction.Halt);
		return compiler.errors;
	}

	public Buffer<CompileError> CompileExpression(string source, ByteCodeChunk chunk, Mode mode)
	{
		compiler.Reset(source, chunk, mode);

		{
			compiler.DebugEmitPushFrame();
			compiler.DebugEmitPushType(new ValueType(TypeKind.Function, compiler.chunk.functionTypes.count));
		}

		compiler.parser.Next();
		Expression(this);

		var topType = compiler.typeStack.count > 0 ?
			compiler.typeStack.PopLast() :
			new ValueType(TypeKind.Unit);

		compiler.chunk.functionTypes.PushBack(new FunctionType(
			new Slice(),
			topType,
			0
		));
		var functionTypeIndex = (ushort)(compiler.chunk.functionTypes.count - 1);
		compiler.chunk.functions.PushBack(new Function(string.Empty, 0, functionTypeIndex));

		{
			compiler.DebugEmitPopFrame();
			compiler.DebugEmitPushType(topType);
		}

		compiler.EmitInstruction(Instruction.Return);
		compiler.EmitByte(topType.GetSize(chunk));

		compiler.EmitInstruction(Instruction.Halt);
		return compiler.errors;
	}

	public static Slice ParseWithPrecedence(CompilerController self, Precedence precedence)
	{
		var parser = self.compiler.parser;
		parser.Next();
		var slice = parser.previousToken.slice;
		if (parser.previousToken.kind == TokenKind.End)
			return slice;

		var prefixRule = self.parseRules.GetPrefixRule(parser.previousToken.kind);
		if (prefixRule == null)
		{
			self.compiler.AddHardError(parser.previousToken.slice, "Expected expression");
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			return slice;
		}
		prefixRule(self, precedence, slice);

		while (
			parser.currentToken.kind != TokenKind.End &&
			precedence <= self.parseRules.GetPrecedence(parser.currentToken.kind)
		)
		{
			parser.Next();
			var infixRule = self.parseRules.GetInfixRule(parser.previousToken.kind);
			infixRule(self, precedence, slice);
			slice = Slice.FromTo(slice, parser.previousToken.slice);
		}

		var canAssign = precedence <= Precedence.Assignment;
		if (canAssign && self.compiler.parser.Match(TokenKind.Equal))
		{
			self.compiler.AddHardError(slice, "Invalid assignment target");
			Expression(self);
		}

		return Slice.FromTo(slice, parser.previousToken.slice);
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
				compiler.typeStack.count = 0;
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
			compiler.AddHardError(compiler.parser.currentToken.slice, "Expected function or struct declaration");
		Syncronize();
	}

	public void FunctionDeclaration()
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected function name");
		ConsumeFunction(compiler.parser.previousToken.slice);
	}

	public static void FunctionExpression(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var functionJump = self.compiler.BeginEmitForwardJump(Instruction.JumpForward);
		self.ConsumeFunction(new Slice());
		self.compiler.EndEmitForwardJump(functionJump);

		var functionIndex = self.compiler.chunk.functions.count - 1;
		var function = self.compiler.chunk.functions.buffer[functionIndex];

		self.compiler.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
		var type = new ValueType(TypeKind.Function, function.typeIndex);
		self.compiler.typeStack.PushBack(type);
		self.compiler.DebugEmitPushType(type);
	}

	private void ConsumeFunction(Slice slice)
	{
		const int MaxParamCount = 8;

		var source = compiler.parser.tokenizer.source;
		var builder = compiler.BeginFunctionDeclaration();
		var paramStartIndex = compiler.localVariables.count;

		compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name");
		if (!compiler.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				compiler.parser.Consume(TokenKind.Identifier, "Expected parameter name");
				var paramSlice = compiler.parser.previousToken.slice;
				compiler.parser.Consume(TokenKind.Colon, "Expected ':' after parameter name");
				var paramType = compiler.ParseType("Expected parameter type", 0);

				if (builder.parameterCount >= MaxParamCount)
				{
					compiler.AddSoftError(paramSlice, "Function can not have more than {0} parameters", MaxParamCount);
					continue;
				}

				var hasDuplicate = false;
				for (var i = 0; i < builder.parameterCount; i++)
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
					compiler.AddSoftError(paramSlice, "Function already has a parameter named '{0}'", CompilerHelper.GetSlice(compiler, paramSlice));
					continue;
				}

				compiler.AddLocalVariable(paramSlice, paramType, false, true);
				builder.WithParam(paramType);
			} while (compiler.parser.Match(TokenKind.Comma) || compiler.parser.Match(TokenKind.End));
		}
		compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (compiler.parser.Match(TokenKind.Colon))
			builder.returnType = compiler.ParseType("Expected function return type", 0);

		compiler.EndFunctionDeclaration(builder, slice);
		compiler.functionReturnTypeStack.PushBack(builder.returnType);

		{
			var functionTypeIndex = compiler.chunk.functions.buffer[compiler.chunk.functions.count - 1].typeIndex;
			var functionType = compiler.chunk.functionTypes.buffer[functionTypeIndex];
			compiler.DebugEmitPushFrame();
			compiler.DebugEmitPushType(new ValueType(TypeKind.Function, functionTypeIndex));
			for (var i = 0; i < functionType.parameters.length; i++)
			{
				var paramType = compiler.chunk.functionParamTypes.buffer[functionType.parameters.index + i];
				compiler.DebugEmitPushType(paramType);
			}
		}

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before function body");

		if (builder.returnType.kind == TypeKind.Unit)
		{
			BlockStatement();
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(this);
			var type = compiler.typeStack.PopLast();
			if (!type.IsEqualTo(builder.returnType))
				compiler.AddSoftError(compiler.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", builder.returnType.ToString(compiler.chunk), type.ToString(compiler.chunk));
		}

		{
			compiler.DebugEmitPopFrame();
			compiler.DebugEmitPushType(builder.returnType);
		}

		compiler.EmitInstruction(Instruction.Return);
		compiler.EmitByte(builder.returnType.GetSize(compiler.chunk));

		compiler.functionReturnTypeStack.PopLast();
		compiler.localVariables.count -= builder.parameterCount;
	}

	public void StructDeclaration()
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected struct name");
		var slice = compiler.parser.previousToken.slice;

		var source = compiler.parser.tokenizer.source;
		var builder = compiler.BeginStructDeclaration();
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
			var fieldType = compiler.ParseType("Expected field type", 0);
			if (!compiler.parser.Check(TokenKind.CloseCurlyBrackets))
				compiler.parser.Consume(TokenKind.Comma, "Expected ',' after field type");

			var hasDuplicate = false;
			for (var i = 0; i < builder.fieldCount; i++)
			{
				var otherName = compiler.chunk.structTypeFields.buffer[fieldStartIndex + i].name;
				if (CompilerHelper.AreEqual(source, fieldSlice, otherName))
				{
					hasDuplicate = true;
					break;
				}
			}

			var fieldName = CompilerHelper.GetSlice(compiler, fieldSlice);
			if (hasDuplicate)
			{
				compiler.AddSoftError(fieldSlice, "Struct already has a field named '{0}'", fieldName);
				continue;
			}

			builder.WithField(fieldName, fieldType);
		}
		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct fields");

		compiler.EndStructDeclaration(builder, slice);
	}

	public static void FinishTupleExpression(CompilerController self, ValueType firstElementType)
	{
		var slice = self.compiler.parser.previousToken.slice;

		var builder = self.compiler.chunk.BeginTupleType();
		builder.WithElement(firstElementType);

		while (
			!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			self.compiler.parser.Consume(TokenKind.Comma, "Expected ',' after element value expression");
			Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();
			builder.WithElement(expressionType);
		}
		self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after tuple expression");

		slice = Slice.FromTo(slice, self.compiler.parser.previousToken.slice);

		var result = builder.Build(out var typeIndex);
		var type = self.compiler.CheckTupleBuild(result, slice) ?
			new ValueType(TypeKind.Tuple, typeIndex) :
			new ValueType(TypeKind.Unit);

		self.compiler.typeStack.PushBack(type);

		self.compiler.DebugEmitPopType((byte)builder.elementCount);
		self.compiler.DebugEmitPushType(type);
	}

	public void Statement(out ValueType type, out StatementKind kind)
	{
		type = new ValueType(TypeKind.Unit);
		kind = StatementKind.Other;

		if (compiler.parser.Match(TokenKind.OpenCurlyBrackets))
			BlockStatement();
		else if (compiler.parser.Match(TokenKind.Let))
			VariableDeclaration(false);
		else if (compiler.parser.Match(TokenKind.Mut))
			VariableDeclaration(true);
		else if (compiler.parser.Match(TokenKind.While))
			WhileStatement();
		else if (compiler.parser.Match(TokenKind.For))
			ForStatement();
		else if (compiler.parser.Match(TokenKind.Break))
			BreakStatement();
		else if (compiler.parser.Match(TokenKind.Return))
			(type, kind) = (ReturnStatement(), StatementKind.Return);
		else if (compiler.parser.Match(TokenKind.Print))
			PrintStatement();
		else
			(type, kind) = (ExpressionStatement(), StatementKind.Expression);
	}

	public ValueType ExpressionStatement()
	{
		Expression(this);
		var type = compiler.typeStack.count > 0 ?
			compiler.typeStack.PopLast() :
			new ValueType(TypeKind.Unit);

		if (!compiler.parser.Check(TokenKind.CloseCurlyBrackets))
		{
			compiler.EmitPop(type.GetSize(compiler.chunk));
			compiler.DebugEmitPopType(1);
		}

		return type;
	}

	public void BlockStatement()
	{
		var scope = compiler.BeginScope();
		ValueType lastStatementType = new ValueType(TypeKind.Unit);
		StatementKind lastStatementKind = StatementKind.Other;
		while (
			!compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!compiler.parser.Check(TokenKind.End)
		)
		{
			Statement(out lastStatementType, out lastStatementKind);
		}

		if (lastStatementKind == StatementKind.Expression)
		{
			compiler.EmitPop(lastStatementType.GetSize(compiler.chunk));
			compiler.DebugEmitPopType(1);
		}

		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");
		compiler.EndScope(scope, 0);
	}

	private void VariableDeclaration(bool mutable)
	{
		if (compiler.parser.Match(TokenKind.OpenCurlyBrackets))
			MultipleVariableDeclaration(mutable);
		else
			SingleVariableDeclaration(mutable);
	}

	private int SingleVariableDeclaration(bool mutable)
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected variable name");
		var slice = compiler.parser.previousToken.slice;

		compiler.parser.Consume(TokenKind.Equal, "Expected assignment");
		Expression(this);

		return compiler.DeclareLocalVariable(slice, mutable);
	}

	private void MultipleVariableDeclaration(bool mutable)
	{
		var slices = new Buffer<Slice>(8);

		while (
			!compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!compiler.parser.Check(TokenKind.End)
		)
		{
			compiler.parser.Consume(TokenKind.Identifier, "Expected variable name");
			slices.PushBackUnchecked(compiler.parser.previousToken.slice);

			if (!compiler.parser.Check(TokenKind.CloseCurlyBrackets))
				compiler.parser.Consume(TokenKind.Comma, "Expected ',' after variable name");
		}
		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after variable names");
		compiler.parser.Consume(TokenKind.Equal, "Expected assignment");
		var expressionSlice = Expression(this);

		var expressionType = compiler.typeStack.PopLast();
		if (expressionType.kind != TypeKind.Tuple)
		{
			compiler.AddSoftError(expressionSlice, "Expression must be a tuple");
			return;
		}

		var tupleElements = compiler.chunk.tupleTypes.buffer[expressionType.index].elements;
		if (tupleElements.length != slices.count)
		{
			compiler.AddSoftError(
				expressionSlice,
				"Tuple element count must be equal to variable declaration count. Expected {0}. Got {1}",
				slices.count,
				tupleElements.length
			);
			return;
		}

		for (var i = 0; i < slices.count; i++)
		{
			var slice = slices.buffer[i];
			var elementType = compiler.chunk.tupleElementTypes.buffer[tupleElements.index + i];
			compiler.AddLocalVariable(slice, elementType, mutable, false);
		}
	}

	public void WhileStatement()
	{
		var labelSlice = new Slice();
		if (compiler.parser.Match(TokenKind.Colon))
		{
			compiler.parser.Consume(TokenKind.Identifier, "Expected loop label name");
			labelSlice = compiler.parser.previousToken.slice;
		}

		var loopJump = compiler.BeginEmitBackwardJump();
		var expressionSlice = Expression(this);

		if (!compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			compiler.AddSoftError(expressionSlice, "Expected bool expression as while condition");

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		compiler.DebugEmitPopType(1);
		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		compiler.BeginLoop(labelSlice);
		BlockStatement();

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		compiler.EndLoop();
	}

	public void ForStatement()
	{
		var labelSlice = new Slice();
		if (compiler.parser.Match(TokenKind.Colon))
		{
			compiler.parser.Consume(TokenKind.Identifier, "Expected loop label name");
			labelSlice = compiler.parser.previousToken.slice;
		}

		var scope = compiler.BeginScope();
		var itVarIndex = SingleVariableDeclaration(true);
		compiler.localVariables.buffer[itVarIndex].isUsed = true;
		var itVar = compiler.localVariables.buffer[itVarIndex];
		if (!itVar.type.IsKind(TypeKind.Int))
			compiler.AddSoftError(itVar.slice, "Expected variable of type int in for loop");

		compiler.parser.Consume(TokenKind.Comma, "Expected comma after begin expression");
		var expressionSlice = Expression(this);
		var toVarIndex = compiler.DeclareLocalVariable(compiler.parser.previousToken.slice, false);
		compiler.localVariables.buffer[toVarIndex].isUsed = true;
		if (!compiler.localVariables.buffer[toVarIndex].type.IsKind(TypeKind.Int))
			compiler.AddSoftError(expressionSlice, "Expected expression of type int");

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

		var loopJump = compiler.BeginEmitBackwardJump();
		compiler.EmitInstruction(Instruction.ForLoopCheck);
		compiler.EmitByte((byte)itVar.stackIndex);

		compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
		compiler.DebugEmitPopType(1);
		var breakJump = compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		compiler.BeginLoop(labelSlice);
		BlockStatement();

		compiler.EmitInstruction(Instruction.IncrementLocalInt);
		compiler.EmitByte((byte)itVar.stackIndex);

		compiler.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
		compiler.EndEmitForwardJump(breakJump);
		compiler.EndLoop();

		compiler.EndScope(scope, 0);
	}

	private void BreakStatement()
	{
		var slice = compiler.parser.previousToken.slice;
		var breakJump = compiler.BeginEmitForwardJump(Instruction.JumpForward);

		var nestingIndex = -1;

		if (compiler.loopNesting.count == 0)
			compiler.AddSoftError(slice, "Not inside a loop");
		else
			nestingIndex = compiler.loopNesting.count - 1;

		if (compiler.parser.Match(TokenKind.Colon))
		{
			compiler.parser.Consume(TokenKind.Identifier, "Expected loop label name");
			var labelSlice = compiler.parser.previousToken.slice;
			slice = Slice.FromTo(slice, labelSlice);

			var source = compiler.parser.tokenizer.source;
			for (var i = 0; i < compiler.loopNesting.count; i++)
			{
				var loopLabelSlice = compiler.loopNesting.buffer[i];
				if (CompilerHelper.AreEqual(source, labelSlice, loopLabelSlice))
				{
					nestingIndex = i;
					break;
				}
			}

			if (nestingIndex < 0)
				compiler.AddSoftError(labelSlice, "Could not find an enclosing loop with label '{0}'", CompilerHelper.GetSlice(compiler, labelSlice));
		}

		if (nestingIndex > byte.MaxValue)
		{
			compiler.AddHardError(slice, "Break is nested too deeply. Max loop nesting level is {0}", byte.MaxValue);
			nestingIndex = -1;
		}

		if (nestingIndex >= 0)
			compiler.loopBreaks.PushBack(new LoopBreak(breakJump, (byte)nestingIndex));
	}

	private ValueType ReturnStatement()
	{
		var expectedType = compiler.functionReturnTypeStack.buffer[compiler.functionReturnTypeStack.count - 1];
		var returnType = new ValueType(TypeKind.Unit);

		Slice slice;

		if (expectedType.IsKind(TypeKind.Unit))
		{
			slice = compiler.parser.previousToken.slice;
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			slice = Expression(this);
			if (compiler.typeStack.count > 0)
				returnType = compiler.typeStack.PopLast();
		}

		{
			compiler.DebugEmitPopFrame();
			compiler.DebugEmitPushType(expectedType);
		}

		compiler.EmitInstruction(Instruction.Return);
		compiler.EmitByte(expectedType.GetSize(compiler.chunk));

		if (!returnType.IsEqualTo(expectedType))
			compiler.AddSoftError(
				slice,
				"Wrong return type. Expected {0}. Got {1}",
				expectedType.ToString(compiler.chunk),
				returnType.ToString(compiler.chunk)
			);

		return returnType;
	}

	private void PrintStatement()
	{
		Expression(this);

		var type = compiler.typeStack.PopLast();

		compiler.EmitInstruction(Instruction.Print);
		compiler.EmitType(type);
	}

	public static Slice Expression(CompilerController self)
	{
		return ParseWithPrecedence(self, Precedence.Assignment);
	}

	public static void Grouping(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		Expression(self);
		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void BlockOrTupleExpression(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		if (self.compiler.parser.Match(TokenKind.CloseCurlyBrackets))
		{
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Unit));
			return;
		}
		else if (
			self.compiler.parser.Check(TokenKind.Let) ||
			self.compiler.parser.Check(TokenKind.Mut) ||
			self.compiler.parser.Check(TokenKind.While) ||
			self.compiler.parser.Check(TokenKind.For) ||
			self.compiler.parser.Check(TokenKind.Break) ||
			self.compiler.parser.Check(TokenKind.Return) ||
			self.compiler.parser.Check(TokenKind.Print)
		)
		{
			var scope = self.compiler.BeginScope();
			self.Statement(out var firstStatementType, out var firstStatementKind);
			FinishBlock(self, scope, firstStatementType, firstStatementKind);
			return;
		}
		else
		{
			var scope = self.compiler.BeginScope();
			Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();
			if (self.compiler.parser.Check(TokenKind.Comma))
			{
				self.compiler.scopeDepth -= 1;
				FinishTupleExpression(self, expressionType);
			}
			else
			{
				FinishBlock(self, scope, expressionType, StatementKind.Expression);
			}
		}
	}

	public static void Block(CompilerController self)
	{
		if (self.compiler.parser.Match(TokenKind.CloseCurlyBrackets))
		{
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Unit));
			return;
		}

		var scope = self.compiler.BeginScope();
		self.Statement(out var firstStatementType, out var firstStatementKind);
		FinishBlock(self, scope, firstStatementType, firstStatementKind);
	}

	public static void FinishBlock(CompilerController self, Scope scope, ValueType lastStatementType, StatementKind lastStatementKind)
	{
		while (
			!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			self.Statement(out lastStatementType, out lastStatementKind);
		}

		self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		var sizeLeftOnStack = lastStatementKind == StatementKind.Expression ?
			lastStatementType.GetSize(self.compiler.chunk) :
			0;

		self.compiler.EndScope(scope, sizeLeftOnStack);

		if (lastStatementKind == StatementKind.Other)
		{
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Unit));
		}
		else
		{
			self.compiler.typeStack.PushBack(lastStatementType);
			self.compiler.DebugEmitPushType(lastStatementType);
		}
	}

	public static void If(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var expressionSlice = Expression(self);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(expressionSlice, "Expected bool expression as if condition");

		self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

		self.compiler.DebugEmitPopType(1);
		var elseJump = self.compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		Block(self);
		var thenType = self.compiler.typeStack.PopLast();
		var hasElse = self.compiler.parser.Match(TokenKind.Else);

		if (!hasElse && !thenType.IsKind(TypeKind.Unit))
		{
			var size = thenType.GetSize(self.compiler.chunk);
			self.compiler.EmitPop(size);
			self.compiler.DebugEmitPopType(1);
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Unit));
			thenType = new ValueType(TypeKind.Unit);
		}

		var thenJump = self.compiler.BeginEmitForwardJump(Instruction.JumpForward);
		self.compiler.EndEmitForwardJump(elseJump);

		if (hasElse)
		{
			if (self.compiler.parser.Match(TokenKind.If))
			{
				If(self, precedence, self.compiler.parser.previousToken.slice);
			}
			else
			{
				self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after else");
				Block(self);
			}

			var elseType = self.compiler.typeStack.PopLast();
			if (!elseType.IsEqualTo(thenType))
				self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "If expression must produce values of the same type on both branches. Found types: {0} and {1}", thenType, elseType);
		}
		else
		{
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Unit));
		}

		self.compiler.EndEmitForwardJump(thenJump);
		self.compiler.typeStack.PushBack(thenType);
	}

	public static void And(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(previousSlice, "Expected bool expression before and");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		self.compiler.EmitInstruction(Instruction.Pop);
		self.compiler.DebugEmitPopType(1);
		var rightSlice = ParseWithPrecedence(self, Precedence.And);
		self.compiler.EndEmitForwardJump(jump);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(rightSlice, "Expected bool expression after and");

		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
	}

	public static void Or(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(previousSlice, "Expected bool expression before or");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		self.compiler.EmitInstruction(Instruction.Pop);
		self.compiler.DebugEmitPopType(1);
		var rightSlice = ParseWithPrecedence(self, Precedence.Or);
		self.compiler.EndEmitForwardJump(jump);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(rightSlice, "Expected bool expression after or");

		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
	}

	public static void Literal(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		switch (self.compiler.parser.previousToken.kind)
		{
		case TokenKind.True:
			self.compiler.EmitInstruction(Instruction.LoadTrue);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.False:
			self.compiler.EmitInstruction(Instruction.LoadFalse);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.IntLiteral:
			self.compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(self.compiler)),
				TypeKind.Int
			);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Int));
			break;
		case TokenKind.FloatLiteral:
			self.compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(self.compiler)),
				TypeKind.Float
			);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Float));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Float));
			break;
		case TokenKind.StringLiteral:
			self.compiler.EmitLoadStringLiteral(CompilerHelper.GetString(self.compiler));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.String));
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.String));
			break;
		default:
			self.compiler.AddHardError(
				self.compiler.parser.previousToken.slice,
				string.Format("Expected literal. Got {0}", self.compiler.parser.previousToken.kind)
			);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			break;
		}
	}

	public static void Identifier(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var storage = new Storage();

		var slice = self.compiler.parser.previousToken.slice;
		if (self.compiler.ResolveToLocalVariableIndex(slice, out storage.variableIndex))
		{
			ref var localVar = ref self.compiler.localVariables.buffer[storage.variableIndex];
			storage.isValid = true;
			storage.type = localVar.type;
			storage.stackIndex = localVar.stackIndex;

			if (self.compiler.parser.Match(TokenKind.Dot))
			{
				do
				{
					if (!FieldAccess(
						self,
						ref slice,
						ref storage.type,
						ref storage.stackIndex
					))
					{
						self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
						break;
					}
				} while (self.compiler.parser.Match(TokenKind.Dot) || self.compiler.parser.Match(TokenKind.End));
			}
		}

		var canAssign = precedence <= Precedence.Assignment;
		if (canAssign && self.compiler.parser.Match(TokenKind.Equal))
			Assign(self, slice, ref storage);
		else
			Access(self, slice, ref storage);
	}

	private static void Assign(CompilerController self, Slice slice, ref Storage storage)
	{
		if (storage.isValid)
		{
			if (!self.compiler.localVariables.buffer[storage.variableIndex].isMutable)
				self.compiler.AddSoftError(slice, "Can not write to immutable variable. Try using 'mut' instead of 'let'");

			Expression(self);

			var expressionType = self.compiler.typeStack.PopLast();
			if (!expressionType.IsEqualTo(storage.type))
			{
				self.compiler.AddSoftError(
					self.compiler.parser.previousToken.slice,
					"Wrong type for assignment. Expected {0}. Got {1}",
					storage.type.ToString(self.compiler.chunk),
					expressionType.ToString(self.compiler.chunk)
				);
			}
			self.compiler.typeStack.PushBack(expressionType);

			var varTypeSize = storage.type.GetSize(self.compiler.chunk);
			if (varTypeSize > 1)
			{
				self.compiler.EmitInstruction(Instruction.AssignLocalMultiple);
				self.compiler.EmitByte((byte)storage.stackIndex);
				self.compiler.EmitByte((byte)varTypeSize);
			}
			else
			{
				self.compiler.EmitInstruction(Instruction.AssignLocal);
				self.compiler.EmitByte((byte)storage.stackIndex);
			}
		}
		else
		{
			Expression(self);
			self.compiler.AddSoftError(slice, "Can not write to undeclared variable. Try declaring it with 'let' or 'mut'");
		}
	}

	private static void Access(CompilerController self, Slice slice, ref Storage storage)
	{
		if (storage.isValid)
		{
			self.compiler.localVariables.buffer[storage.variableIndex].isUsed = true;
			self.compiler.EmitLoadLocal(storage.stackIndex, storage.type);
			self.compiler.typeStack.PushBack(storage.type);
			self.compiler.DebugEmitPushType(storage.type);
		}
		else if (self.compiler.ResolveToFunctionIndex(slice, out var functionIndex))
		{
			self.compiler.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
			var function = self.compiler.chunk.functions.buffer[functionIndex];
			var type = new ValueType(TypeKind.Function, function.typeIndex);
			self.compiler.typeStack.PushBack(type);
			self.compiler.DebugEmitPushType(type);
		}
		else if (self.compiler.ResolveToNativeFunctionIndex(slice, out var nativeFunctionIndex))
		{
			self.compiler.EmitLoadFunction(Instruction.LoadNativeFunction, nativeFunctionIndex);
			var function = self.compiler.chunk.nativeFunctions.buffer[nativeFunctionIndex];
			var type = new ValueType(TypeKind.NativeFunction, function.typeIndex);
			self.compiler.typeStack.PushBack(type);
			self.compiler.DebugEmitPushType(type);
		}
		else if (self.compiler.ResolveToStructTypeIndex(slice, out var structIndex))
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
				if (!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets))
					self.compiler.parser.Consume(TokenKind.Comma, "Expected ',' after field value expression");

				var expressionType = self.compiler.typeStack.PopLast();
				if (!expressionType.IsEqualTo(field.type))
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
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Struct, structIndex));
			self.compiler.DebugEmitPopType((byte)structType.fields.length);
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Struct, structIndex));
		}
		else
		{
			self.compiler.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let' or 'mut'");
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
		}
	}

	public static bool FieldAccess(CompilerController self, ref Slice slice, ref ValueType type, ref int stackIndex)
	{
		if (!self.compiler.chunk.GetStructType(type, out var structType))
		{
			self.compiler.AddSoftError(slice, "Accessed value must be a struct");
			return false;
		}

		var structTypeIndex = type.index;

		self.compiler.parser.Consume(TokenKind.Identifier, "Expected field name");
		slice = self.compiler.parser.previousToken.slice;

		var offset = 0;
		var source = self.compiler.parser.tokenizer.source;

		for (var i = 0; i < structType.fields.length; i++)
		{
			var fieldIndex = structType.fields.index + i;
			var field = self.compiler.chunk.structTypeFields.buffer[fieldIndex];
			if (CompilerHelper.AreEqual(source, slice, field.name))
			{
				type = field.type;
				stackIndex += offset;
				return true;
			}

			offset += field.type.GetSize(self.compiler.chunk);
		}

		var sb = new StringBuilder();
		self.compiler.chunk.FormatStructType(structTypeIndex, sb);
		self.compiler.AddSoftError(slice, "Could not find such field for struct of type {0}", sb);
		return false;
	}

	public static void Dot(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var slice = self.compiler.parser.previousToken.slice;
		var type = self.compiler.typeStack.PopLast();
		var offset = 0;

		var structSize = type.GetSize(self.compiler.chunk);

		do
		{
			if (!FieldAccess(
				self,
				ref slice,
				ref type,
				ref offset
			))
			{
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
				return;
			}
		} while (self.compiler.parser.Match(TokenKind.Dot) || self.compiler.parser.Match(TokenKind.End));

		var fieldSize = type.GetSize(self.compiler.chunk);
		var sizeAboveField = structSize - offset - fieldSize;

		self.compiler.EmitPop(sizeAboveField);
		self.compiler.DebugEmitPopType(1);

		if (offset > 0)
		{
			self.compiler.EmitInstruction(Instruction.Move);
			self.compiler.EmitByte((byte)offset);
			self.compiler.EmitByte((byte)fieldSize);
		}

		self.compiler.typeStack.PushBack(type);
		self.compiler.DebugEmitPushType(type);
		return;
	}

	public static void Call(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var slice = self.compiler.parser.previousToken.slice;

		var type = self.compiler.typeStack.PopLast();
		var isFunction = self.compiler.chunk.GetFunctionType(type, out var functionType);
		if (!isFunction)
			self.compiler.AddSoftError(slice, "Callee must be a function");

		var argIndex = 0;
		if (!self.compiler.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				Expression(self);
				var argType = self.compiler.typeStack.PopLast();
				if (
					isFunction &&
					argIndex < functionType.parameters.length
				)
				{
					var paramType = self.compiler.chunk.functionParamTypes.buffer[functionType.parameters.index + argIndex];
					if (!argType.IsEqualTo(paramType))
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
			} while (self.compiler.parser.Match(TokenKind.Comma) || self.compiler.parser.Match(TokenKind.End));
		}

		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expect ')' after function argument list");

		slice = Slice.FromTo(slice, self.compiler.parser.previousToken.slice);

		if (isFunction && argIndex != functionType.parameters.length)
			self.compiler.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", functionType.parameters.length, argIndex);

		var popCount = isFunction ? functionType.parameters.length + 1 : 1;
		self.compiler.DebugEmitPopType((byte)popCount);

		if (type.kind == TypeKind.Function)
			self.compiler.EmitInstruction(Instruction.Call);
		else if (type.kind == TypeKind.NativeFunction)
			self.compiler.EmitInstruction(Instruction.CallNative);

		self.compiler.EmitByte((byte)(isFunction ? functionType.parametersSize : 0));
		var returnType = isFunction ? functionType.returnType : new ValueType(TypeKind.Unit);
		self.compiler.typeStack.PushBack(returnType);

		if (type.kind == TypeKind.NativeFunction)
			self.compiler.DebugEmitPushType(returnType);
	}

	public static void Unary(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var opToken = self.compiler.parser.previousToken;

		var slice = ParseWithPrecedence(self, Precedence.Unary);
		slice = Slice.FromTo(previousSlice, slice);

		var type = self.compiler.typeStack.PopLast();

		switch (opToken.kind)
		{
		case TokenKind.Minus:
			if (type.IsKind(TypeKind.Int))
			{
				self.compiler.EmitInstruction(Instruction.NegateInt);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));
			}
			else if (type.IsKind(TypeKind.Float))
			{
				self.compiler.EmitInstruction(Instruction.NegateFloat);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Float));
			}
			else
			{
				self.compiler.AddSoftError(slice, "Unary minus operator can only be applied to ints or floats. Got type {0}", type.ToString(self.compiler.chunk));
				self.compiler.typeStack.PushBack(type);
			}
			break;
		case TokenKind.Not:
			if (type.IsKind(TypeKind.Bool))
			{
				self.compiler.EmitInstruction(Instruction.Not);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			}
			else
			{
				self.compiler.AddSoftError(slice, "Not operator can only be applied to bools. Got type {0}", type.ToString(self.compiler.chunk));
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			}
			break;
		case TokenKind.Int:
			if (type.IsKind(TypeKind.Float))
				self.compiler.EmitInstruction(Instruction.FloatToInt);
			else
				self.compiler.AddSoftError(slice, "Can only convert floats to int. Got type {0}", type.ToString(self.compiler.chunk));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));

			self.compiler.DebugEmitPopType(1);
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Int));
			break;
		case TokenKind.Float:
			if (type.IsKind(TypeKind.Int))
				self.compiler.EmitInstruction(Instruction.IntToFloat);
			else
				self.compiler.AddSoftError(slice, "Can only convert ints to float. Got {0}", type.ToString(self.compiler.chunk));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Float));

			self.compiler.DebugEmitPopType(1);
			self.compiler.DebugEmitPushType(new ValueType(TypeKind.Float));
			break;
		default:
			self.compiler.AddHardError(
					slice,
					string.Format("Expected unary operator. Got token {0}", opToken.kind)
				);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			break;
		}
	}

	public static void Binary(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var c = self.compiler;
		var opToken = c.parser.previousToken;
		var hasNotAfterIs = opToken.kind == TokenKind.Is && c.parser.Match(TokenKind.Not);

		var opPrecedence = self.parseRules.GetPrecedence(opToken.kind);
		var slice = ParseWithPrecedence(self, opPrecedence + 1);
		slice = Slice.FromTo(previousSlice, slice);

		var bType = c.typeStack.PopLast();
		var aType = c.typeStack.PopLast();

		switch (opToken.kind)
		{
		case TokenKind.Plus:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.AddInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.AddFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(slice, "Plus operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk)).typeStack.PushBack(aType);

			self.compiler.DebugEmitPopType(1);
			break;
		case TokenKind.Minus:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.SubtractInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.SubtractFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(slice, "Minus operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk)).typeStack.PushBack(aType);

			self.compiler.DebugEmitPopType(1);
			break;
		case TokenKind.Asterisk:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.MultiplyInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.MultiplyFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(slice, "Multiply operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk)).typeStack.PushBack(aType);

			self.compiler.DebugEmitPopType(1);
			break;
		case TokenKind.Slash:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.DivideInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.DivideFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(slice, "Divide operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk)).typeStack.PushBack(aType);

			self.compiler.DebugEmitPopType(1);
			break;
		case TokenKind.Is:
			{
				var opName = hasNotAfterIs ? "IsNot" : "Is";
				if (!aType.IsEqualTo(bType))
				{
					c.AddSoftError(slice, "{0} operator can only be applied to same type values. Got types {0} and {1}", aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk), opName);
					c.typeStack.PushBack(new ValueType(TypeKind.Bool));
					break;
				}
				if (!aType.IsSimple() || !bType.IsSimple())
				{
					c.AddSoftError(slice, "{0} operator can only be applied to bools, ints, floats and strings. Got types {0} and {1}", aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk), opName);
					break;
				}

				switch (aType.kind)
				{
				case TypeKind.Bool:
					c.EmitInstruction(Instruction.EqualBool);
					break;
				case TypeKind.Int:
					c.EmitInstruction(Instruction.EqualInt);
					break;
				case TypeKind.Float:
					c.EmitInstruction(Instruction.EqualFloat);
					break;
				case TypeKind.String:
					c.EmitInstruction(Instruction.EqualString);
					break;
				default:
					c.AddSoftError(slice, "{0} operator can only be applied to bools, ints, floats and string. Got types {0} and {1}", aType.ToString(self.compiler.chunk), opName);
					break;
				}

				if (hasNotAfterIs)
					c.EmitInstruction(Instruction.Not);
				c.typeStack.PushBack(new ValueType(TypeKind.Bool));

				{
					self.compiler.DebugEmitPopType(2);
					self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
				}
				break;
			}
		case TokenKind.Greater:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.GreaterInt);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.GreaterFloat);
			else
				c.AddSoftError(slice, "Greater operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk));
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));

			{
				self.compiler.DebugEmitPopType(2);
				self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
			}
			break;
		case TokenKind.GreaterEqual:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.LessInt).EmitInstruction(Instruction.Not);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.LessFloat).EmitInstruction(Instruction.Not);
			else
				c.AddSoftError(slice, "GreaterOrEqual operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk));
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));

			{
				self.compiler.DebugEmitPopType(2);
				self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
			}
			break;
		case TokenKind.Less:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.LessInt);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.LessFloat);
			else
				c.AddSoftError(slice, "Less operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk));
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));

			{
				self.compiler.DebugEmitPopType(2);
				self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
			}
			break;
		case TokenKind.LessEqual:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.GreaterInt).EmitInstruction(Instruction.Not);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.GreaterFloat).EmitInstruction(Instruction.Not);
			else
				c.AddSoftError(slice, "LessOrEqual operator can only be applied to ints or floats. Got types {0} and {1}", aType.ToString(self.compiler.chunk));
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));

			{
				self.compiler.DebugEmitPopType(2);
				self.compiler.DebugEmitPushType(new ValueType(TypeKind.Bool));
			}
			break;
		default:
			return;
		}
	}

	public static void NativeCall(CompilerController self, Precedence precedence, Slice previousSlice)
	{
		var identifiers = new Buffer<string>(8);
		var slice = self.compiler.parser.previousToken.slice;
		do
		{
			self.compiler.parser.Consume(TokenKind.Identifier, "Expected native identifier");
			identifiers.PushBackUnchecked(CompilerHelper.GetPreviousSlice(self.compiler));
		} while (self.compiler.parser.Match(TokenKind.Dot) || self.compiler.parser.Match(TokenKind.End));
		slice = Slice.FromTo(slice, self.compiler.parser.previousToken.slice);

		var methods = new System.Reflection.MethodInfo[0];
		string methodName = null;

		if (identifiers.count < 2)
		{
			self.compiler.AddSoftError(slice, "Expected at least 2 identifiers separated by '.'");
		}
		else
		{
			var typeName = string.Join(".", identifiers.buffer, 0, identifiers.count - 1);

			System.Type type = null;
			for (var i = 0; i < self.searchingAssemblies.count; i++)
			{
				var assembly = self.searchingAssemblies.buffer[i];
				type = assembly.GetType(typeName);
				if (type != null)
					break;
			}

			if (type != null)
			{
				methods = type.GetMethods();
				methodName = identifiers.buffer[identifiers.count - 1];
			}
			else
			{
				self.compiler.AddSoftError(slice, "Could not find type '{0}'", typeName);
			}
		}

		var expressionTypes = new Buffer<ValueType>(8);
		var argumentsSize = 0;

		self.compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' before native function call argument");
		while (
			!self.compiler.parser.Check(TokenKind.CloseParenthesis) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();
			expressionTypes.PushBack(expressionType);
			argumentsSize = expressionType.GetSize(self.compiler.chunk);

			if (!self.compiler.parser.Check(TokenKind.CloseParenthesis))
				self.compiler.parser.Consume(TokenKind.Comma, "Expected ',' after native function call argument");
		}
		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after native function call argument");

		if (argumentsSize > byte.MaxValue)
		{
			self.compiler.AddSoftError(
				slice,
				"Native function call arguments size is too big. Max is {0}", byte.MaxValue
			);
			argumentsSize = byte.MaxValue;
		}

		System.Reflection.MethodInfo method = null;
		foreach (var m in methods)
		{
			if (m.Name != methodName)
				goto NoMatch;

			var parameters = m.GetParameters();
			if (parameters.Length != expressionTypes.count)
				goto NoMatch;

			for (var i = 0; i < parameters.Length; i++)
			{
				if (!expressionTypes.buffer[i].IsCompatibleWithNativeType(parameters[i].ParameterType))
					goto NoMatch;
			}

			method = m;
			break;
		NoMatch:;
		}

		var returnType = self.compiler.parser.Match(TokenKind.Colon) ?
			self.compiler.ParseType("Expected native function call return type", 0) :
			new ValueType(TypeKind.Unit);

		if (!string.IsNullOrEmpty(methodName))
		{
			if (method == null)
			{
				self.compiler.AddSoftError(slice, "Could not find native method '{0}'", methodName);
			}
			else if (!returnType.IsCompatibleWithNativeType(method.ReturnType))
			{
				self.compiler.AddSoftError(
					slice,
					"Inconpatible return type of native method '{0}'. Expected '{1}'. Got '{2}'",
					methodName,
					returnType.ToString(self.compiler.chunk),
					method.ReturnType.Name
				);
				method = null;
			}
		}

		self.compiler.DebugEmitPopType((byte)expressionTypes.count);

		if (self.compiler.chunk.nativeCalls.count > byte.MaxValue)
		{
			self.compiler.AddSoftError(
				slice,
				"Too many native function calls. Max is {0}", byte.MaxValue
			);
			return;
		}

		self.compiler.EmitInstruction(Instruction.CallNativeAuto);
		self.compiler.EmitByte((byte)self.compiler.chunk.nativeCalls.count);
		self.compiler.chunk.nativeCalls.PushBack(new NativeCall(
			method,
			returnType,
			expressionTypes.ToArray(),
			(byte)argumentsSize
		));

		self.compiler.typeStack.PushBack(returnType);
		self.compiler.DebugEmitPushType(returnType);
	}
}