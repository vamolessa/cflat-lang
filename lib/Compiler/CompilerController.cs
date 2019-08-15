using System.Collections.Generic;
using System.Text;

public sealed class CompilerController
{
	public enum StatementKind
	{
		Other,
		Expression,
		Return,
	}

	public Compiler compiler = new Compiler();
	public readonly ParseRules parseRules = new ParseRules();

	public List<CompileError> Compile(string source, ByteCodeChunk chunk)
	{
		compiler.Reset(source, chunk);

		compiler.parser.Next();
		while (!compiler.parser.Match(TokenKind.End))
			Declaration();

		compiler.EmitInstruction(Instruction.Halt);

		return compiler.errors;
	}

	public List<CompileError> CompileExpression(string source, ByteCodeChunk chunk)
	{
		compiler.Reset(source, chunk);

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
		compiler.chunk.functions.PushBack(new Function(string.Empty, 0, 0));

		compiler.EmitInstruction(Instruction.Halt);

		return compiler.errors;
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
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
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

	public static void FunctionExpression(CompilerController self, Precedence precedence)
	{
		var functionJump = self.compiler.BeginEmitForwardJump(Instruction.JumpForward);
		self.ConsumeFunction(new Slice());
		self.compiler.EndEmitForwardJump(functionJump);

		var functionIndex = self.compiler.chunk.functions.count - 1;
		var function = self.compiler.chunk.functions.buffer[functionIndex];

		self.compiler.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Function, function.typeIndex));
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
			} while (compiler.parser.Match(TokenKind.Comma));
		}
		compiler.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

		if (compiler.parser.Match(TokenKind.Colon))
			builder.returnType = compiler.ParseType("Expected function return type", 0);

		compiler.EndFunctionDeclaration(builder, slice);
		compiler.functionReturnTypeStack.PushBack(builder.returnType);

		compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before function body");

		if (builder.returnType.kind == TypeKind.Unit)
		{
			BlockStatement();
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Block(this, Precedence.None);
			var type = compiler.typeStack.PopLast();
			if (!type.IsEqualTo(builder.returnType))
				compiler.AddSoftError(compiler.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", builder.returnType.ToString(compiler.chunk), type.ToString(compiler.chunk));
		}

		compiler.EmitInstruction(Instruction.Return);
		compiler.EmitByte((byte)builder.returnType.GetSize(compiler.chunk));

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

	public static void StructExpression(CompilerController self, Precedence precedence)
	{
		var slice = self.compiler.parser.previousToken.slice;

		var source = self.compiler.parser.tokenizer.source;
		var builder = self.compiler.BeginStructDeclaration();
		var fieldStartIndex = self.compiler.chunk.structTypeFields.count;

		self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before struct initializer");
		while (
			!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			self.compiler.parser.Consume(TokenKind.Identifier, "Expected field name");
			var fieldSlice = self.compiler.parser.previousToken.slice;
			var fieldName = CompilerHelper.GetSlice(self.compiler, fieldSlice);
			self.compiler.parser.Consume(TokenKind.Equal, "Expected '=' after field name");

			Expression(self);
			var expressionType = self.compiler.typeStack.PopLast();

			builder.WithField(fieldName, expressionType);
		}
		self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct initializer");

		var structIndex = builder.BuildAnonymous();
		var type = new ValueType(TypeKind.Struct, structIndex);
		self.compiler.typeStack.PushBack(type);

		if (self.compiler.chunk.structTypes.count >= ushort.MaxValue)
		{
			self.compiler.chunk.structTypes.count -= builder.fieldCount;
			self.compiler.AddSoftError(slice, "Too many struct declarations");
		}

		var structSize = self.compiler.chunk.structTypes.buffer[structIndex].size;
		if (structSize >= byte.MaxValue)
		{
			self.compiler.AddSoftError(
				slice,
				"Struct size is too big. Max is {0}. Got {1}",
				byte.MaxValue,
				structSize
			);
		}
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

		compiler.EmitPop(type.GetSize(compiler.chunk));

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
			Statement(out var _, out var _);
		}

		compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");
		compiler.EndScope(scope, 0);
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

		if (!compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
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
		if (!itVar.type.IsKind(TypeKind.Int))
			compiler.AddSoftError(itVar.slice, "Expected variable of type int in for loop");

		compiler.parser.Consume(TokenKind.Comma, "Expected comma after begin expression");
		Expression(this);
		var toVarIndex = compiler.DeclareLocalVariable(compiler.parser.previousToken.slice, false);
		compiler.localVariables.buffer[toVarIndex].isUsed = true;
		if (!compiler.localVariables.buffer[toVarIndex].type.IsKind(TypeKind.Int))
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

		compiler.EndScope(scope, 0);
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
		var returnType = new ValueType(TypeKind.Unit);

		if (expectedType.IsKind(TypeKind.Unit))
		{
			compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			Expression(this);
			if (compiler.typeStack.count > 0)
				returnType = compiler.typeStack.PopLast();
		}

		compiler.EmitInstruction(Instruction.Return);
		compiler.EmitByte((byte)expectedType.GetSize(compiler.chunk));

		if (!returnType.IsEqualTo(expectedType))
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", expectedType.ToString(compiler.chunk), returnType.ToString(compiler.chunk));

		return returnType;
	}

	private void PrintStatement()
	{
		Expression(this);
		compiler.EmitInstruction(Instruction.Print);

		var type = compiler.typeStack.buffer[compiler.typeStack.count - 1];
		type.Write(out var b0, out var b1, out var b2, out var b3);
		compiler.EmitByte(b0);
		compiler.EmitByte(b1);
		compiler.EmitByte(b2);
		compiler.EmitByte(b3);

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
		var lastStatementType = new ValueType(TypeKind.Unit);
		var statementKind = StatementKind.Other;

		while (
			!self.compiler.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.compiler.parser.Check(TokenKind.End)
		)
		{
			self.Statement(out lastStatementType, out statementKind);
		}

		self.compiler.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

		var sizeLeftOnStack = 0;
		if (statementKind == StatementKind.Expression)
		{
			sizeLeftOnStack = lastStatementType.GetSize(self.compiler.chunk);
			self.compiler.chunk.bytes.count -= sizeLeftOnStack > 1 ? 2 : 1;
		}

		self.compiler.EndScope(scope, sizeLeftOnStack);

		if (statementKind == StatementKind.Other)
		{
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			self.compiler.EmitInstruction(Instruction.LoadUnit);
		}
		else
		{
			self.compiler.typeStack.PushBack(lastStatementType);
		}
	}

	public static void If(CompilerController self, Precedence precedence)
	{
		Expression(self);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression as if condition");

		self.compiler.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

		var elseJump = self.compiler.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
		Block(self, precedence);
		var thenType = self.compiler.typeStack.PopLast();
		var hasElse = self.compiler.parser.Match(TokenKind.Else);

		if (!hasElse && !thenType.IsKind(TypeKind.Unit))
		{
			var size = thenType.GetSize(self.compiler.chunk);
			self.compiler.EmitPop(size);
			self.compiler.EmitInstruction(Instruction.LoadUnit);
			thenType = new ValueType(TypeKind.Unit);
		}

		var thenJump = self.compiler.BeginEmitForwardJump(Instruction.JumpForward);
		self.compiler.EndEmitForwardJump(elseJump);

		if (hasElse)
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
			if (!elseType.IsEqualTo(thenType))
				self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "If expression must produce values of the same type on both branches. Found types: {0} and {1}", thenType, elseType);
		}
		else
		{
			self.compiler.EmitInstruction(Instruction.LoadUnit);
		}

		self.compiler.EndEmitForwardJump(thenJump);
		self.compiler.typeStack.PushBack(thenType);
	}

	public static void And(CompilerController self, Precedence precedence)
	{
		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression before and");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
		self.compiler.EmitInstruction(Instruction.Pop);
		ParseWithPrecedence(self, Precedence.And);
		self.compiler.EndEmitForwardJump(jump);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression after and");

		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
	}

	public static void Or(CompilerController self, Precedence precedence)
	{
		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression before or");

		var jump = self.compiler.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
		self.compiler.EmitInstruction(Instruction.Pop);
		ParseWithPrecedence(self, Precedence.Or);
		self.compiler.EndEmitForwardJump(jump);

		if (!self.compiler.typeStack.PopLast().IsKind(TypeKind.Bool))
			self.compiler.AddSoftError(self.compiler.parser.previousToken.slice, "Expected bool expression after or");

		self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
	}

	public static void Literal(CompilerController self, Precedence precedence)
	{
		switch (self.compiler.parser.previousToken.kind)
		{
		case TokenKind.True:
			self.compiler.EmitInstruction(Instruction.LoadTrue);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.False:
			self.compiler.EmitInstruction(Instruction.LoadFalse);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.IntLiteral:
			self.compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetInt(self.compiler)),
				TypeKind.Int
			);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));
			break;
		case TokenKind.FloatLiteral:
			self.compiler.EmitLoadLiteral(
				new ValueData(CompilerHelper.GetFloat(self.compiler)),
				TypeKind.Float
			);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Float));
			break;
		case TokenKind.StringLiteral:
			self.compiler.EmitLoadStringLiteral(CompilerHelper.GetString(self.compiler));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.String));
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

	private struct Storage
	{
		public bool isValid;
		public int variableIndex;
		public ValueType type;
		public int stackIndex;
	}

	public static void Identifier(CompilerController self, Precedence precedence)
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
				} while (self.compiler.parser.Match(TokenKind.Dot));
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
		}
		else if (self.compiler.ResolveToFunctionIndex(slice, out var functionIndex))
		{
			self.compiler.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
			var function = self.compiler.chunk.functions.buffer[functionIndex];
			var type = new ValueType(TypeKind.Function, function.typeIndex);
			self.compiler.typeStack.PushBack(type);
		}
		else if (self.compiler.ResolveToNativeFunctionIndex(slice, out var nativeFunctionIndex))
		{
			self.compiler.EmitLoadFunction(Instruction.LoadNativeFunction, nativeFunctionIndex);
			var function = self.compiler.chunk.nativeFunctions.buffer[nativeFunctionIndex];
			var type = new ValueType(TypeKind.NativeFunction, function.typeIndex);
			self.compiler.typeStack.PushBack(type);
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

			var type = new ValueType(TypeKind.Struct, structIndex);
			self.compiler.typeStack.PushBack(type);
		}
		else
		{
			self.compiler.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let'");
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

	public static void Dot(CompilerController self, Precedence precedence)
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
		} while (self.compiler.parser.Match(TokenKind.Dot));

		var fieldSize = type.GetSize(self.compiler.chunk);
		var sizeAboveField = structSize - offset - fieldSize;

		self.compiler.EmitPop(sizeAboveField);

		if (offset > 0)
		{
			self.compiler.EmitInstruction(Instruction.Move);
			self.compiler.EmitByte((byte)offset);
			self.compiler.EmitByte((byte)fieldSize);
		}

		self.compiler.typeStack.PushBack(type);
		return;
	}

	public static void Call(CompilerController self, Precedence precedence)
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
			} while (self.compiler.parser.Match(TokenKind.Comma));
		}

		self.compiler.parser.Consume(TokenKind.CloseParenthesis, "Expect ')' after function argument list");

		if (isFunction && argIndex != functionType.parameters.length)
			self.compiler.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", functionType.parameters.length, argIndex);

		if (type.kind == TypeKind.Function)
			self.compiler.EmitInstruction(Instruction.Call);
		else if (type.kind == TypeKind.NativeFunction)
			self.compiler.EmitInstruction(Instruction.CallNative);

		self.compiler.EmitByte((byte)(isFunction ? functionType.parametersSize : 0));
		self.compiler.typeStack.PushBack(
			isFunction ? functionType.returnType : new ValueType(TypeKind.Unit)
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
			switch (type.kind)
			{
			case TypeKind.Int:
				self.compiler.EmitInstruction(Instruction.NegateInt);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));
				break;
			case TypeKind.Float:
				self.compiler.EmitInstruction(Instruction.NegateFloat);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Float));
				break;
			default:
				self.compiler.AddSoftError(opToken.slice, "Unary minus operator can only be applied to ints or floats");
				self.compiler.typeStack.PushBack(type);
				break;
			}
			break;
		case TokenKind.Not:
			switch (type.kind)
			{
			case TypeKind.Bool:
				self.compiler.EmitInstruction(Instruction.Not);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
				break;
			case TypeKind.Int:
			case TypeKind.Float:
			case TypeKind.String:
				self.compiler.EmitInstruction(Instruction.Pop);
				self.compiler.EmitInstruction(Instruction.LoadFalse);
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
				break;
			default:
				self.compiler.AddSoftError(opToken.slice, "Not operator can only be applied to bools, ints, floats or strings");
				self.compiler.typeStack.PushBack(new ValueType(TypeKind.Bool));
				break;
			}
			break;
		case TokenKind.Int:
			if (type.IsKind(TypeKind.Float))
				self.compiler.EmitInstruction(Instruction.FloatToInt);
			else
				self.compiler.AddSoftError(opToken.slice, "Can only convert floats to int. Got {0}", type.ToString(self.compiler.chunk));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Int));
			break;
		case TokenKind.Float:
			if (type.IsKind(TypeKind.Int))
				self.compiler.EmitInstruction(Instruction.IntToFloat);
			else
				self.compiler.AddSoftError(opToken.slice, "Can only convert ints to float. Got {0}", type.ToString(self.compiler.chunk));
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Float));
			break;
		default:
			self.compiler.AddHardError(
					opToken.slice,
					string.Format("Expected unary operator. Got {0}", opToken.kind)
				);
			self.compiler.typeStack.PushBack(new ValueType(TypeKind.Unit));
			break;
		}
	}

	public static void Binary(CompilerController self, Precedence precedence)
	{
		var c = self.compiler;
		var opToken = c.parser.previousToken;
		var hasNotAfterIs =
			opToken.kind == TokenKind.Is &&
			c.parser.Match(TokenKind.Not);

		var opPrecedence = self.parseRules.GetPrecedence(opToken.kind);
		ParseWithPrecedence(self, opPrecedence + 1);

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
				c.AddSoftError(opToken.slice, "Plus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Minus:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.SubtractInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.SubtractFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(opToken.slice, "Minus operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Asterisk:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.MultiplyInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.MultiplyFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(opToken.slice, "Multiply operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Slash:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.DivideInt).typeStack.PushBack(new ValueType(TypeKind.Int));
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.DivideFloat).typeStack.PushBack(new ValueType(TypeKind.Float));
			else
				c.AddSoftError(opToken.slice, "Divide operator can only be applied to ints or floats").typeStack.PushBack(aType);
			break;
		case TokenKind.Is:
			{
				var opName = hasNotAfterIs ? "IsNot" : "Is";

				if (!aType.IsEqualTo(bType))
				{
					c.AddSoftError(opToken.slice, "{0} operator can only be applied to same type values", opName);
					c.typeStack.PushBack(new ValueType(TypeKind.Bool));
					break;
				}
				if (!aType.IsSimple() || !bType.IsSimple())
				{
					c.AddSoftError(opToken.slice, "{0} operator can only be applied to bools, ints and floats", opName);
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
					c.AddSoftError(opToken.slice, "{0} operator can only be applied to bools, ints and floats", opName);
					break;
				}

				if (hasNotAfterIs)
					c.EmitInstruction(Instruction.Not);
				c.typeStack.PushBack(new ValueType(TypeKind.Bool));
				break;
			}
		case TokenKind.Greater:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.GreaterInt);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.GreaterFloat);
			else
				c.AddSoftError(opToken.slice, "Greater operator can only be applied to ints or floats");
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.GreaterEqual:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c
					.EmitInstruction(Instruction.LessInt)
					.EmitInstruction(Instruction.Not);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c
					.EmitInstruction(Instruction.LessFloat)
					.EmitInstruction(Instruction.Not);
			else
				c.AddSoftError(opToken.slice, "GreaterOrEqual operator can only be applied to ints or floats");
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.Less:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c.EmitInstruction(Instruction.LessInt);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c.EmitInstruction(Instruction.LessFloat);
			else
				c.AddSoftError(opToken.slice, "Less operator can only be applied to ints or floats");
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));
			break;
		case TokenKind.LessEqual:
			if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				c
					.EmitInstruction(Instruction.GreaterInt)
					.EmitInstruction(Instruction.Not);
			else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				c
					.EmitInstruction(Instruction.GreaterFloat)
					.EmitInstruction(Instruction.Not);
			else
				c.AddSoftError(opToken.slice, "LessOrEqual operator can only be applied to ints or floats");
			c.typeStack.PushBack(new ValueType(TypeKind.Bool));
			break;
		default:
			return;
		}
	}
}