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
		public int variableIndex;
		public ValueType type;
		public byte offset;
	}

	private struct IndexStorage
	{
		public byte elementSize;
		public byte offset;
		public ValueType type;
	}

	private struct VariableDeclarationInfo
	{
		public Slice slice;
		public bool isMutable;
	}

	public readonly Compiler compiler = new Compiler();
	public readonly ParseRules parseRules = new ParseRules();

	public Buffer<CompileError> Compile(ByteCodeChunk chunk, Mode mode, string source)
	{
		compiler.Reset(chunk, mode, source);

		compiler.parser.Next();
		while (!compiler.parser.Match(TokenKind.End))
			Declaration();

		compiler.EmitInstruction(Instruction.Halt);

		for (var i = 0; i < compiler.chunk.functions.count; i++)
		{
			var function = compiler.chunk.functions.buffer[i];
			if (function.codeIndex < 0)
			{
				var slice = new Slice(-function.codeIndex, 1);
				var functionType = new ValueType(TypeKind.Function, function.typeIndex);
				compiler.AddSoftError(slice, "Pending definition for function prototype '{0}' {1}", function.name, functionType.ToString(compiler.chunk));
			}
		}

		return compiler.errors;
	}

	public Buffer<CompileError> CompileExpression(ByteCodeChunk chunk, Mode mode, string source)
	{
		compiler.Reset(chunk, mode, source);

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
		compiler.EmitByte(topType.GetSize(compiler.chunk));

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
		prefixRule(self, slice);

		while (
			parser.currentToken.kind != TokenKind.End &&
			precedence <= self.parseRules.GetPrecedence(parser.currentToken.kind)
		)
		{
			parser.Next();
			var infixRule = self.parseRules.GetInfixRule(parser.previousToken.kind);
			infixRule(self, slice);
			slice = Slice.FromTo(slice, parser.previousToken.slice);
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

	public static void FunctionExpression(CompilerController self, Slice previousSlice)
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

		var requireBody = slice.length == 0;
		var source = compiler.parser.tokenizer.source;
		var builder = compiler.BeginFunctionDeclaration();
		var paramStartIndex = compiler.localVariables.count;

		compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name");
		if (!compiler.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				var isMutable = compiler.parser.Match(TokenKind.Mut);
				compiler.parser.Consume(TokenKind.Identifier, "Expected parameter name");
				var paramSlice = compiler.parser.previousToken.slice;
				compiler.parser.Consume(TokenKind.Colon, "Expected ':' after parameter name");
				var paramType = compiler.ParseType("Expected parameter type");
				var paramAndTypeSlice = Slice.FromTo(paramSlice, compiler.parser.previousToken.slice);

				if (builder.parameterCount >= MaxParamCount)
				{
					compiler.AddSoftError(paramAndTypeSlice, "Function can not have more than {0} parameters", MaxParamCount);
					continue;
				}

				var hasDuplicatedParameter = false;
				for (var i = 0; i < builder.parameterCount; i++)
				{
					var otherSlice = compiler.localVariables.buffer[paramStartIndex + i].slice;
					if (CompilerHelper.AreEqual(source, paramSlice, otherSlice))
					{
						hasDuplicatedParameter = true;
						break;
					}
				}

				if (hasDuplicatedParameter)
				{
					compiler.AddSoftError(paramAndTypeSlice, "Function already has a parameter named '{0}'", CompilerHelper.GetSlice(compiler, paramSlice));
					continue;
				}

				var paramFlags = VariableFlags.Used;
				if (isMutable)
					paramFlags |= VariableFlags.Mutable;
				compiler.AddLocalVariable(paramSlice, paramType, paramFlags);
				builder.WithParam(paramType);
			} while (compiler.parser.Match(TokenKind.Comma) || compiler.parser.Match(TokenKind.End));
		}
		compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (compiler.parser.Match(TokenKind.Colon))
			builder.returnType = compiler.ParseType("Expected function return type");

		if (requireBody)
		{
			compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before function body");
		}
		else if (!compiler.parser.Match(TokenKind.OpenCurlyBrackets))
		{
			compiler.localVariables.count -= builder.parameterCount;
			compiler.EndFunctionDeclaration(builder, slice, false);
			return;
		}

		compiler.functionReturnTypeStack.PushBack(builder.returnType);
		var functionIndex = compiler.EndFunctionDeclaration(builder, slice, true);

		{
			var functionTypeIndex = compiler.chunk.functions.buffer[functionIndex].typeIndex;
			var functionType = compiler.chunk.functionTypes.buffer[functionTypeIndex];
			compiler.DebugEmitPushFrame();
			compiler.DebugEmitPushType(new ValueType(TypeKind.Function, functionTypeIndex));
			for (var i = 0; i < functionType.parameters.length; i++)
			{
				var paramType = compiler.chunk.functionParamTypes.buffer[functionType.parameters.index + i];
				compiler.DebugEmitPushType(paramType);
			}
		}

		if (builder.returnType.IsKind(TypeKind.Unit))
		{
			BlockStatement();
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(this);
			var type = compiler.typeStack.PopLast();
			if (!builder.returnType.Accepts(type))
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
			var fieldType = compiler.ParseType("Expected field type");
			var fieldAndTypeSlice = Slice.FromTo(fieldSlice, compiler.parser.previousToken.slice);

			if (fieldType.IsReference)
				compiler.AddSoftError(fieldAndTypeSlice, "Struct can not contain reference fields");

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
				compiler.AddSoftError(fieldAndTypeSlice, "Struct already has a field named '{0}'", fieldName);
				continue;
			}

			builder.WithField(fieldName, fieldType);
		}
		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct fields");

		compiler.EndStructDeclaration(builder, slice);
	}

	public static void FinishTupleExpression(CompilerController self, Slice slice, ValueType firstElementType)
	{
		var builder = self.compiler.chunk.BeginTupleType();

		if (firstElementType.IsReference)
			self.compiler.AddSoftError(slice, "Can not create tuple containing references");
		builder.WithElement(firstElementType);

		while (
			!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			self.compiler.parser.Consume(TokenKind.Comma, "Expected ',' after element value expression");
			var expressionSlice = Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();
			if (expressionType.IsReference)
				self.compiler.AddSoftError(expressionSlice, "Can not create tuple containing references");
			builder.WithElement(expressionType);
		}
		self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after tuple expression");

		slice = Slice.FromTo(slice, self.compiler.parser.previousToken.slice);

		var result = builder.Build(out var typeIndex);
		var type = self.compiler.CheckTupleBuild(result, slice) ?
			new ValueType(TypeKind.Tuple, typeIndex) :
			new ValueType(TypeKind.Unit);

		self.compiler.typeStack.PushBack(type);

		{
			self.compiler.DebugEmitPopType((byte)builder.elementCount);
			self.compiler.DebugEmitPushType(type);
		}
	}

	public static void ArrayExpression(CompilerController self, Slice previousSlice)
	{
		var defaultValueSlice = Expression(self);
		var defaultValueType = self.compiler.typeStack.PopLast();
		if (defaultValueType.IsArray)
			self.compiler.AddSoftError(defaultValueSlice, "Can not create array of arrays");
		if (defaultValueType.IsReference)
			self.compiler.AddSoftError(defaultValueSlice, "Can not create array of references");

		self.compiler.parser.Consume(TokenKind.Colon, "Expected ':' after array element default value");

		var lengthSlice = Expression(self);
		var lengthType = self.compiler.typeStack.PopLast();
		if (!lengthType.IsKind(TypeKind.Int))
			self.compiler.AddSoftError(lengthSlice, "Expected int expression for array length. Got {0}", lengthType.ToString(self.compiler.chunk));

		self.compiler.parser.Consume(TokenKind.CloseSquareBrackets, "Expected ']' after array expression");

		var arrayType = defaultValueType.ToArrayType();
		self.compiler.typeStack.PushBack(arrayType);

		self.compiler.DebugEmitPopType(2);

		self.compiler.EmitInstruction(Instruction.CreateArray);
		self.compiler.EmitByte(defaultValueType.GetSize(self.compiler.chunk));

		self.compiler.DebugEmitPushType(arrayType);
	}

	public static void LengthExpression(CompilerController self, Slice previousSlice)
	{
		var expressionSlice = Expression(self);
		var expressionType = self.compiler.typeStack.PopLast();
		self.compiler.DebugEmitPopType(0);

		if (!expressionType.IsArray)
			self.compiler.AddSoftError(expressionSlice, "Expected array type. Got {0}", expressionType.ToString(self.compiler.chunk));

		self.compiler.EmitInstruction(Instruction.LoadArrayLength);

		self.compiler.DebugEmitPushType(new ValueType(TypeKind.Int));
		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));
	}

	public void Statement(out ValueType type, out StatementKind kind)
	{
		type = new ValueType(TypeKind.Unit);
		kind = StatementKind.Other;

		if (compiler.parser.Match(TokenKind.OpenCurlyBrackets))
			BlockStatement();
		else if (compiler.parser.Match(TokenKind.Let))
			VariableDeclaration();
		else if (compiler.parser.Match(TokenKind.Set))
			SetStatement();
		else if (compiler.parser.Match(TokenKind.While))
			WhileStatement();
		else if (compiler.parser.Match(TokenKind.Repeat))
			RepeatStatement();
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

	private void VariableDeclaration()
	{
		if (compiler.parser.Match(TokenKind.OpenCurlyBrackets))
			MultipleVariableDeclaration();
		else
			SingleVariableDeclaration();
	}

	private int SingleVariableDeclaration()
	{
		var isMutable = compiler.parser.Match(TokenKind.Mut);
		compiler.parser.Consume(TokenKind.Identifier, "Expected variable name");
		var slice = compiler.parser.previousToken.slice;

		compiler.parser.Consume(TokenKind.Equal, "Expected assignment");
		Expression(this);

		return compiler.DeclareLocalVariable(slice, isMutable);
	}

	private void MultipleVariableDeclaration()
	{
		var declarations = new Buffer<VariableDeclarationInfo>(8);

		while (
			!compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!compiler.parser.Check(TokenKind.End)
		)
		{
			var isMutable = compiler.parser.Match(TokenKind.Mut);
			compiler.parser.Consume(TokenKind.Identifier, "Expected variable name");
			declarations.PushBackUnchecked(new VariableDeclarationInfo
			{
				slice = compiler.parser.previousToken.slice,
				isMutable = isMutable
			});

			if (!compiler.parser.Check(TokenKind.CloseCurlyBrackets))
				compiler.parser.Consume(TokenKind.Comma, "Expected ',' after variable name");
		}
		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after variable names");
		compiler.parser.Consume(TokenKind.Equal, "Expected assignment");
		var expressionSlice = Expression(this);

		var expressionType = compiler.typeStack.PopLast();
		if (expressionType.kind != TypeKind.Tuple || expressionType.flags != TypeFlags.None)
		{
			compiler.AddSoftError(expressionSlice, "Expression must be a tuple");
			return;
		}

		var tupleElements = compiler.chunk.tupleTypes.buffer[expressionType.index].elements;
		if (tupleElements.length != declarations.count)
		{
			compiler.AddSoftError(
				expressionSlice,
				"Tuple element count must be equal to variable declaration count. Expected {0}. Got {1}",
				declarations.count,
				tupleElements.length
			);
			return;
		}

		for (var i = 0; i < declarations.count; i++)
		{
			var declaration = declarations.buffer[i];
			var elementType = compiler.chunk.tupleElementTypes.buffer[tupleElements.index + i];
			compiler.AddLocalVariable(declaration.slice, elementType, declaration.isMutable ? VariableFlags.Mutable : VariableFlags.None);
		}
	}

	public void SetStatement()
	{
		compiler.parser.Consume(TokenKind.Identifier, "Expected identifier");
		var slice = compiler.parser.previousToken.slice;

		if (compiler.ResolveToLocalVariableIndex(slice, out var variableIndex))
		{
			var localVar = compiler.localVariables.buffer[variableIndex];
			var storage = new Storage
			{
				variableIndex = variableIndex,
				type = localVar.type,
			};

			GetStorage(this, ref slice, ref storage);
			if (compiler.parser.Match(TokenKind.OpenSquareBrackets))
			{
				Access(this, slice, ref storage);
				SetArrayElement(slice, storage.type, localVar.IsMutable);
			}
			else if (compiler.parser.Match(TokenKind.OpenParenthesis))
			{
				Access(this, slice, ref storage);
				SetFunctionReturn(slice, storage.type);
			}
			else
			{
				compiler.parser.Consume(TokenKind.Equal, "Expected '=' before expression");
				Assign(this, slice, ref storage);
			}
		}
		else if (compiler.ResolveToFunctionIndex(slice, out var functionIndex))
		{
			compiler.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
			var function = compiler.chunk.functions.buffer[functionIndex];
			var type = new ValueType(TypeKind.Function, function.typeIndex);

			compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name on set statement");
			SetFunctionReturn(slice, type);
		}
		else if (compiler.ResolveToNativeFunctionIndex(slice, out var nativeFunctionIndex))
		{
			compiler.EmitLoadFunction(Instruction.LoadNativeFunction, nativeFunctionIndex);
			var function = compiler.chunk.nativeFunctions.buffer[nativeFunctionIndex];
			var type = new ValueType(TypeKind.NativeFunction, function.typeIndex);

			compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name on set statement");
			SetFunctionReturn(slice, type);
		}
		else
		{
			compiler.AddHardError(slice, "Could not find variable or function named '{0}'", CompilerHelper.GetSlice(compiler, slice));
			return;
		}
	}

	private void SetArrayElement(Slice slice, ValueType arrayType, bool isMutable)
	{
		GetIndexStorage(this, arrayType, ref slice, out var storage);

		if (compiler.parser.Match(TokenKind.OpenSquareBrackets))
		{
			IndexAccess(this, ref storage);
			SetArrayElement(slice, storage.type, isMutable);
		}
		if (compiler.parser.Match(TokenKind.OpenParenthesis))
		{
			IndexAccess(this, ref storage);
			SetFunctionReturn(slice, storage.type);
		}
		else
		{
			compiler.parser.Consume(TokenKind.Equal, "Expected '=' before expression");
			if (!isMutable)
				compiler.AddSoftError(slice, "Can not write to immutable variable. Try adding 'mut' after 'let' at its declaration");

			IndexAssign(this, ref storage);
		}
	}

	private void SetFunctionReturn(Slice slice, ValueType functionType)
	{
		compiler.typeStack.PushBack(functionType);
		compiler.DebugEmitPushType(functionType);

		Call(this, slice);
		slice = Slice.FromTo(slice, compiler.parser.previousToken.slice);

		if (compiler.parser.Match(TokenKind.Dot))
		{
			Dot(this, slice);
			slice = Slice.FromTo(slice, compiler.parser.previousToken.slice);
		}

		var type = compiler.typeStack.PopLast();

		if (compiler.parser.Match(TokenKind.OpenSquareBrackets))
			SetArrayElement(slice, type, true);
		else if (compiler.parser.Match(TokenKind.OpenParenthesis))
			SetFunctionReturn(slice, type);
		else
			compiler.AddHardError(slice, "Can not write to temporary value. Try assigning it to a variable first");
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

	public void RepeatStatement()
	{
		var labelSlice = new Slice();
		if (compiler.parser.Match(TokenKind.Colon))
		{
			compiler.parser.Consume(TokenKind.Identifier, "Expected loop label name");
			labelSlice = compiler.parser.previousToken.slice;
		}

		var scope = compiler.BeginScope();

		compiler.EmitLoadLiteral(new ValueData(0), TypeKind.Int);
		compiler.DebugEmitPushType(new ValueType(TypeKind.Int));

		var itVarIndex = compiler.AddLocalVariable(new Slice(), new ValueType(TypeKind.Int), VariableFlags.Used | VariableFlags.Iteration);
		var itVar = compiler.localVariables.buffer[itVarIndex];

		var expressionSlice = Expression(this);
		if (!compiler.typeStack.PopLast().IsKind(TypeKind.Int))
			compiler.AddSoftError(expressionSlice, "Expected expression of type int as repeat count");
		var countVarIndex = compiler.AddLocalVariable(new Slice(), new ValueType(TypeKind.Int), VariableFlags.Used);

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after repeat statement");

		var loopJump = compiler.BeginEmitBackwardJump();
		compiler.EmitInstruction(Instruction.RepeatLoopCheck);
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

		if (!expectedType.Accepts(returnType))
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
		compiler.DebugEmitPopType(1);

		compiler.EmitInstruction(Instruction.Print);
		compiler.EmitType(type);
	}

	public static Slice Expression(CompilerController self)
	{
		return ParseWithPrecedence(self, Precedence.Assignment);
	}

	public static void Grouping(CompilerController self, Slice previousSlice)
	{
		Expression(self);
		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void BlockOrTupleExpression(CompilerController self, Slice previousSlice)
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
			self.compiler.parser.Check(TokenKind.Set) ||
			self.compiler.parser.Check(TokenKind.While) ||
			self.compiler.parser.Check(TokenKind.Repeat) ||
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
			var expressionSlice = Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();
			if (self.compiler.parser.Check(TokenKind.Comma))
			{
				self.compiler.scopeDepth -= 1;
				FinishTupleExpression(self, expressionSlice, expressionType);
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

	public static void If(CompilerController self, Slice previousSlice)
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
				If(self, self.compiler.parser.previousToken.slice);
			}
			else
			{
				self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after else");
				Block(self);
			}

			var elseType = self.compiler.typeStack.PopLast();
			if (!thenType.Accepts(elseType))
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

	public static void And(CompilerController self, Slice previousSlice)
	{
		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(previousSlice, "Expected bool expression before '&&'");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		self.compiler.EmitInstruction(Instruction.Pop);
		self.compiler.DebugEmitPopType(1);
		var rightSlice = ParseWithPrecedence(self, Precedence.And);
		self.compiler.EndEmitForwardJump(jump);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(rightSlice, "Expected bool expression after '&&'");

		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
	}

	public static void Or(CompilerController self, Slice previousSlice)
	{
		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(previousSlice, "Expected bool expression before '||'");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		self.compiler.EmitInstruction(Instruction.Pop);
		self.compiler.DebugEmitPopType(1);
		var rightSlice = ParseWithPrecedence(self, Precedence.Or);
		self.compiler.EndEmitForwardJump(jump);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(rightSlice, "Expected bool expression after '||'");

		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
	}

	public static void Literal(CompilerController self, Slice previousSlice)
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

	private static void GetStorage(CompilerController self, ref Slice slice, ref Storage storage)
	{
		storage.type = storage.type.ToReferredType();

		var hasError = false;
		while (self.compiler.parser.Match(TokenKind.Dot) || self.compiler.parser.Match(TokenKind.End))
		{
			if (hasError)
				continue;

			hasError = !FieldAccess(
				self,
				ref slice,
				ref storage.type,
				ref storage.offset
			);
		}
	}

	public static void Identifier(CompilerController self, Slice previousSlice)
	{
		var slice = self.compiler.parser.previousToken.slice;
		var storage = new Storage { variableIndex = -1 };

		if (self.compiler.ResolveToLocalVariableIndex(slice, out var variableIndex))
		{
			var localVar = self.compiler.localVariables.buffer[variableIndex];
			storage.variableIndex = variableIndex;
			storage.type = localVar.type;

			GetStorage(self, ref slice, ref storage);
		}

		Access(self, slice, ref storage);
	}

	private static void Assign(CompilerController self, Slice slice, ref Storage storage)
	{
		if (storage.variableIndex >= 0)
		{
			ref var localVar = ref self.compiler.localVariables.buffer[storage.variableIndex];
			var storageIsMutable = localVar.IsMutable || localVar.type.IsMutable;
			if (!storageIsMutable)
				self.compiler.AddSoftError(slice, "Can not write to immutable variable");

			var expressionSlice = Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();
			if (!storage.type.Accepts(expressionType))
			{
				self.compiler.AddSoftError(
					expressionSlice,
					"Wrong type for assignment. Expected {0}. Got {1}",
					storage.type.ToString(self.compiler.chunk),
					expressionType.ToString(self.compiler.chunk)
				);
			}

			self.compiler.DebugEmitPopType(1);

			if (localVar.type.IsReference)
			{
				if (localVar.type.IsMutable)
					localVar.flags |= VariableFlags.Used;

				self.compiler.EmitInstruction(Instruction.SetReference);
				self.compiler.EmitByte(localVar.stackIndex);
				self.compiler.EmitByte(storage.offset);
				self.compiler.EmitByte(storage.type.GetSize(self.compiler.chunk));
			}
			else
			{
				self.compiler.EmitSetLocal(localVar.stackIndex + storage.offset, storage.type);
			}
		}
		else
		{
			Expression(self);
			self.compiler.AddSoftError(slice, "Can not write to undeclared variable. Try declaring it with 'let mut'");
		}
	}

	private static void Access(CompilerController self, Slice slice, ref Storage storage)
	{
		if (storage.variableIndex >= 0)
		{
			ref var localVar = ref self.compiler.localVariables.buffer[storage.variableIndex];
			localVar.flags |= VariableFlags.Used;

			if (localVar.type.IsReference)
			{
				self.compiler.EmitInstruction(Instruction.LoadReference);
				self.compiler.EmitByte(localVar.stackIndex);
				self.compiler.EmitByte(storage.offset);
				self.compiler.EmitByte(storage.type.GetSize(self.compiler.chunk));

				self.compiler.typeStack.PushBack(storage.type);
				self.compiler.DebugEmitPushType(storage.type);
			}
			else
			{
				self.compiler.EmitLoadLocal(localVar.stackIndex + storage.offset, storage.type);
				self.compiler.typeStack.PushBack(storage.type);
				self.compiler.DebugEmitPushType(storage.type);
			}
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
				if (!field.type.Accepts(expressionType))
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
			self.compiler.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let'");
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
		}
	}

	public static bool FieldAccess(CompilerController self, ref Slice slice, ref ValueType type, ref byte stackOffset)
	{
		if (!self.compiler.chunk.GetStructType(type, out var structType))
		{
			type = new ValueType(TypeKind.Unit);
			self.compiler.AddSoftError(slice, "Accessed value must be a struct");
			return false;
		}

		var structTypeIndex = type.index;

		self.compiler.parser.Consume(TokenKind.Identifier, "Expected field name");
		slice = self.compiler.parser.previousToken.slice;

		byte offset = 0;
		var source = self.compiler.parser.tokenizer.source;

		for (var i = 0; i < structType.fields.length; i++)
		{
			var fieldIndex = structType.fields.index + i;
			var field = self.compiler.chunk.structTypeFields.buffer[fieldIndex];
			if (CompilerHelper.AreEqual(source, slice, field.name))
			{
				type = field.type;
				stackOffset += offset;
				return true;
			}

			offset += field.type.GetSize(self.compiler.chunk);
		}

		var sb = new StringBuilder();
		self.compiler.chunk.FormatStructType(structTypeIndex, sb);
		self.compiler.AddSoftError(slice, "Could not find such field for struct of type {0}", sb);
		type = new ValueType(TypeKind.Unit);
		return false;
	}

	public static void Dot(CompilerController self, Slice previousSlice)
	{
		var storage = new Storage { type = self.compiler.typeStack.PopLast() };

		var slice = previousSlice;
		var structSize = storage.type.GetSize(self.compiler.chunk);

		if (FieldAccess(self, ref slice, ref storage.type, ref storage.offset))
			GetStorage(self, ref slice, ref storage);

		var fieldSize = storage.type.GetSize(self.compiler.chunk);
		var sizeAboveField = structSize - storage.offset - fieldSize;

		self.compiler.EmitPop(sizeAboveField);
		self.compiler.DebugEmitPopType(1);

		if (storage.offset > 0)
		{
			self.compiler.EmitInstruction(Instruction.Move);
			self.compiler.EmitByte(storage.offset);
			self.compiler.EmitByte(fieldSize);
		}

		self.compiler.typeStack.PushBack(storage.type);
		self.compiler.DebugEmitPushType(storage.type);
		return;
	}

	private static void GetIndexStorage(CompilerController self, ValueType arrayType, ref Slice slice, out IndexStorage storage)
	{
		if (!arrayType.IsArray)
			self.compiler.AddSoftError(slice, "Can only index array types. Got {0}", arrayType.ToString(self.compiler.chunk));

		var indexExpressionSlice = Expression(self);
		var indexExpressionType = self.compiler.typeStack.PopLast();
		if (!indexExpressionType.IsKind(TypeKind.Int))
			self.compiler.AddSoftError(indexExpressionSlice, "Expected int expression for array index. Got {0}", indexExpressionType.ToString(self.compiler.chunk));

		self.compiler.parser.Consume(TokenKind.CloseSquareBrackets, "Expected ']' after array indexing");

		byte offset = 0;
		storage = new IndexStorage();
		storage.type = arrayType.ToArrayElementType();
		storage.elementSize = storage.type.GetSize(self.compiler.chunk);

		var hasError = false;
		while (self.compiler.parser.Match(TokenKind.Dot) || self.compiler.parser.Match(TokenKind.End))
		{
			if (hasError)
				continue;
			hasError = !FieldAccess(
				self,
				ref slice,
				ref storage.type,
				ref offset
			);
		}

		storage.offset = (byte)offset;
	}

	public static void Index(CompilerController self, Slice previousSlice)
	{
		var arrayType = self.compiler.typeStack.PopLast();
		var slice = previousSlice;

		GetIndexStorage(self, arrayType, ref slice, out var storage);
		IndexAccess(self, ref storage);
	}

	private static void IndexAccess(CompilerController self, ref IndexStorage storage)
	{
		self.compiler.DebugEmitPopType(2);

		self.compiler.EmitInstruction(Instruction.LoadArrayElement);
		self.compiler.EmitByte(storage.elementSize);
		self.compiler.EmitByte(storage.type.GetSize(self.compiler.chunk));
		self.compiler.EmitByte(storage.offset);

		self.compiler.typeStack.PushBack(storage.type);
		self.compiler.DebugEmitPushType(storage.type);
	}

	private static void IndexAssign(CompilerController self, ref IndexStorage storage)
	{
		var expressionSlice = Expression(self);
		var expressionType = self.compiler.typeStack.PopLast();
		if (!storage.type.Accepts(expressionType))
		{
			self.compiler.AddSoftError(
				expressionSlice,
				"Wrong type for assignment. Expected {0}. Got {1}",
				storage.type.ToString(self.compiler.chunk),
				expressionType.ToString(self.compiler.chunk)
			);
		}

		self.compiler.DebugEmitPopType(3);

		self.compiler.EmitInstruction(Instruction.SetArrayElement);
		self.compiler.EmitByte(storage.elementSize);
		self.compiler.EmitByte(storage.type.GetSize(self.compiler.chunk));
		self.compiler.EmitByte(storage.offset);
	}

	public static void Call(CompilerController self, Slice previousSlice)
	{
		var slice = previousSlice;

		var type = self.compiler.typeStack.PopLast();
		var isFunction = self.compiler.chunk.GetFunctionType(type, out var functionType);
		if (!isFunction)
			self.compiler.AddSoftError(slice, "Callee must be a function");

		var argIndex = 0;
		if (!self.compiler.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				var argSlice = Expression(self);
				var argType = self.compiler.typeStack.PopLast();

				if (
					isFunction &&
					argIndex < functionType.parameters.length
				)
				{
					var paramType = self.compiler.chunk.functionParamTypes.buffer[functionType.parameters.index + argIndex];

					if (!paramType.Accepts(argType))
					{
						self.compiler.AddSoftError(
							argSlice,
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

	public static void Reference(CompilerController self, Slice previousSlice)
	{
		var isMutable = self.compiler.parser.Match(TokenKind.Mut);

		self.compiler.parser.Consume(TokenKind.Identifier, "Expected identifier");
		var slice = self.compiler.parser.previousToken.slice;

		if (self.compiler.ResolveToLocalVariableIndex(slice, out var variableIndex))
		{
			var localVar = self.compiler.localVariables.buffer[variableIndex];
			var storage = new Storage
			{
				variableIndex = variableIndex,
				type = localVar.type
			};

			GetStorage(self, ref slice, ref storage);
			// if (self.compiler.parser.Match(TokenKind.OpenSquareBrackets))
			// {
			// 	Access(self, slice, ref storage);
			// 	SetArrayElement(slice, storage.type, localVar.IsMutable);
			// }
			// else if (self.compiler.parser.Match(TokenKind.OpenParenthesis))
			// {
			// 	Access(self, slice, ref storage);
			// 	SetFunctionReturn(slice, storage.type);
			// }
			// else
			{
				var storageIsMutable = localVar.IsMutable || localVar.type.IsMutable;
				if (isMutable && !storageIsMutable)
				{
					self.compiler.AddSoftError(slice, "Can not create a mutable reference to an immutable variable");
					return;
				}

				self.compiler.localVariables.buffer[storage.variableIndex].flags |= VariableFlags.Used;

				if (localVar.type.IsReference)
				{
					self.compiler.EmitLoadLocal(localVar.stackIndex, localVar.type);
					if (storage.offset > 0)
					{
						self.compiler.EmitLoadLiteral(new ValueData(storage.offset), TypeKind.Int);
						self.compiler.EmitInstruction(Instruction.AddInt);
					}
				}
				else
				{
					self.compiler.EmitInstruction(Instruction.CreateStackReference);
					self.compiler.EmitByte((byte)storage.offset);
				}

				var referenceType = storage.type.ToReferenceType(isMutable);
				self.compiler.DebugEmitPushType(referenceType);
				self.compiler.typeStack.PushBack(referenceType);
			}
		}
		// else if (self.compiler.ResolveToFunctionIndex(slice, out var functionIndex))
		// {
		// 	self.compiler.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
		// 	var function = self.compiler.chunk.functions.buffer[functionIndex];
		// 	var type = new ValueType(TypeKind.Function, function.typeIndex);

		// 	self.compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name on set statement");
		// 	SetFunctionReturn(slice, type);
		// }
		// else if (self.compiler.ResolveToNativeFunctionIndex(slice, out var nativeFunctionIndex))
		// {
		// 	self.compiler.EmitLoadFunction(Instruction.LoadNativeFunction, nativeFunctionIndex);
		// 	var function = self.compiler.chunk.nativeFunctions.buffer[nativeFunctionIndex];
		// 	var type = new ValueType(TypeKind.NativeFunction, function.typeIndex);

		// 	self.compiler.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name on set statement");
		// 	SetFunctionReturn(slice, type);
		// }
		else
		{
			self.compiler.AddHardError(slice, "Could not find variable or function named '{0}'", CompilerHelper.GetSlice(self.compiler, slice));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			return;
		}
	}

	public static void Unary(CompilerController self, Slice previousSlice)
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
				self.compiler.AddSoftError(slice, "Unary '-' operator can only be applied to ints or floats. Got type {0}", type.ToString(self.compiler.chunk));
				self.compiler.typeStack.PushBack(type);
			}
			break;
		case TokenKind.Bang:
			if (type.IsKind(TypeKind.Bool))
			{
				self.compiler.EmitInstruction(Instruction.Not);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			}
			else
			{
				self.compiler.AddSoftError(slice, "'!' operator can only be applied to bools. Got type {0}", type.ToString(self.compiler.chunk));
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

	public static void Binary(CompilerController self, Slice previousSlice)
	{
		var c = self.compiler;
		var opToken = c.parser.previousToken;

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
		case TokenKind.EqualEqual:
		case TokenKind.BangEqual:
			{
				var operatorName = opToken.kind == TokenKind.EqualEqual ? "==" : "!=";

				if (!aType.IsEqualTo(bType))
				{
					c.AddSoftError(slice, "'{0}' operator can only be applied to same type values. Got types {1} and {2}", operatorName, aType.ToString(self.compiler.chunk), bType.ToString(self.compiler.chunk));
					c.typeStack.PushBack(new ValueType(TypeKind.Bool));
					break;
				}
				if (!aType.IsSimple)
				{
					c.AddSoftError(slice, "'{0}' operator can not be applied to unit, tuples or structs types. Got type {1}", operatorName, aType.ToString(self.compiler.chunk));
					break;
				}

				switch (aType.kind)
				{
				case TypeKind.Bool:
					c.EmitInstruction(Instruction.EqualBool);
					break;
				case TypeKind.Int:
				case TypeKind.Function:
				case TypeKind.NativeFunction:
				case TypeKind.NativeClass:
					c.EmitInstruction(Instruction.EqualInt);
					break;
				case TypeKind.Float:
					c.EmitInstruction(Instruction.EqualFloat);
					break;
				case TypeKind.String:
					c.EmitInstruction(Instruction.EqualString);
					break;
				default:
					c.AddSoftError(slice, "'{0}' operator can not be applied to unit, tuples or structs types. Got type {1}", operatorName, aType.ToString(self.compiler.chunk));
					break;
				}

				if (opToken.kind == TokenKind.BangEqual)
					self.compiler.EmitInstruction(Instruction.Not);

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
}