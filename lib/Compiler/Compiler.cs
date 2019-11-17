using System.Text;

namespace cflat
{
	internal sealed class Compiler
	{
		public readonly struct ExpressionResult
		{
			public readonly Slice slice;
			public readonly ValueType type;

			public ExpressionResult(Slice slice, ValueType type)
			{
				this.slice = slice;
				this.type = type;
			}
		}

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
			public ValueType type;
			public byte offset;
			public byte elementSize;
		}

		private struct VariableDeclarationInfo
		{
			public Slice slice;
			public bool isMutable;
		}

		public readonly CompilerIO io = new CompilerIO();
		public Buffer<Source> compiledSources = new Buffer<Source>(1);
		private readonly ParseRules parseRules = new ParseRules();
		private Option<IModuleResolver> moduleResolver = Option.None;

		public Buffer<CompileError> CompileSource(ByteCodeChunk chunk, Option<IModuleResolver> moduleResolver, Mode mode, Source source)
		{
			this.moduleResolver = moduleResolver;
			compiledSources.count = 0;

			io.Reset(mode, chunk);
			Compile(source);
			return io.errors;
		}

		private void Compile(Source source)
		{
			io.BeginSource(source.content, compiledSources.count);
			compiledSources.PushBack(source);

			var finishedModuleImports = false;
			io.parser.Next();
			while (!io.parser.Match(TokenKind.End))
				Declaration(ref finishedModuleImports);

			io.EmitInstruction(Instruction.Halt);

			for (var i = 0; i < io.chunk.functions.count; i++)
			{
				var function = io.chunk.functions.buffer[i];
				if (function.codeIndex < 0)
				{
					var slice = new Slice(-function.codeIndex, 1);
					var functionType = new ValueType(TypeKind.Function, function.typeIndex);
					io.AddSoftError(slice, "Pending definition for function prototype '{0}' {1}", function.name, functionType.ToString(io.chunk));
				}
			}

			io.EndSource();
		}

		public Buffer<CompileError> CompileExpression(ByteCodeChunk chunk, Mode mode, Source source)
		{
			moduleResolver = null;
			io.Reset(mode, chunk);
			io.BeginSource(source.content, compiledSources.count);
			compiledSources.PushBack(source);

			{
				io.DebugEmitPushFrame();
				io.DebugEmitPushType(new ValueType(TypeKind.Function, io.chunk.functionTypes.count));
			}

			io.parser.Next();
			var expression = Expression(this);

			io.chunk.functionTypes.PushBack(new FunctionType(new Slice(), expression.type, 0));
			var functionTypeIndex = (ushort)(io.chunk.functionTypes.count - 1);
			io.chunk.functions.PushBack(new Function(string.Empty, true, 0, functionTypeIndex));

			{
				io.DebugEmitPopFrame();
				io.DebugEmitPushType(expression.type);
			}

			io.EmitInstruction(Instruction.Return);
			io.EmitByte(expression.type.GetSize(io.chunk));

			io.EmitInstruction(Instruction.Halt);

			io.EndSource();
			return io.errors;
		}

		private static ExpressionResult ParseWithPrecedence(Compiler self, Precedence precedence)
		{
			var parser = self.io.parser;
			parser.Next();
			var slice = parser.previousToken.slice;
			if (parser.previousToken.kind == TokenKind.End)
				return new ExpressionResult(slice, new ValueType(TypeKind.Unit));

			var prefixRule = self.parseRules.GetPrefixRule(parser.previousToken.kind);
			if (prefixRule == null)
			{
				self.io.AddHardError(parser.previousToken.slice, "Expected expression");
				return new ExpressionResult(slice, new ValueType(TypeKind.Unit));
			}
			var type = prefixRule(self);

			while (
				parser.currentToken.kind != TokenKind.End &&
				precedence <= self.parseRules.GetPrecedence(parser.currentToken.kind)
			)
			{
				parser.Next();
				var infixRule = self.parseRules.GetInfixRule(parser.previousToken.kind);
				type = infixRule(self, new ExpressionResult(slice, type));
				slice = Slice.FromTo(slice, parser.previousToken.slice);
			}

			slice = Slice.FromTo(slice, parser.previousToken.slice);
			return new ExpressionResult(slice, type);
		}

		private void Syncronize()
		{
			if (!io.isInPanicMode)
				return;

			while (io.parser.currentToken.kind != TokenKind.End)
			{
				switch (io.parser.currentToken.kind)
				{
				case TokenKind.Mod:
				case TokenKind.Function:
				case TokenKind.Struct:
					io.isInPanicMode = false;
					return;
				default:
					break;
				}

				io.parser.Next();
			}
		}

		private void Declaration(ref bool finishedModuleImports)
		{
			var isPublic = io.parser.Match(TokenKind.Pub);

			if (!isPublic && io.parser.Match(TokenKind.Mod))
			{
				ModuleImport(finishedModuleImports);
			}
			else if (io.parser.Match(TokenKind.Function))
			{
				finishedModuleImports = true;
				FunctionDeclaration(isPublic);
			}
			else if (io.parser.Match(TokenKind.Struct))
			{
				finishedModuleImports = true;
				StructDeclaration(isPublic);
			}
			else
			{
				finishedModuleImports = true;
				io.AddHardError(io.parser.currentToken.slice, "Expected module import or function/struct declaration");
			}

			Syncronize();
		}

		private void ModuleImport(bool finishedModuleImports)
		{
			var slice = io.parser.previousToken.slice;
			io.parser.Consume(TokenKind.StringLiteral, "Expected module path string");
			var modulePath = CompilerHelper.GetParsedString(io);
			slice = Slice.FromTo(slice, io.parser.previousToken.slice);

			if (finishedModuleImports)
			{
				io.AddSoftError(slice, "Module imports must appear at the top");
				return;
			}

			if (!moduleResolver.isSome)
			{
				io.AddSoftError(slice, "No module resolver provided. Can't import module '{0}'", modulePath);
				return;
			}

			var currentSource = compiledSources.buffer[compiledSources.count - 1];
			var maybeModuleUri = moduleResolver.value.ResolveModuleUri(currentSource.uri, modulePath);
			if (!maybeModuleUri.isSome)
			{
				io.AddSoftError(slice, "Could not resolve module uri '{0}' from '{1}'", modulePath, currentSource.uri);
				return;
			}
			var moduleUri = maybeModuleUri.value;

			for (var i = 0; i < compiledSources.count; i++)
			{
				if (compiledSources.buffer[i].uri == moduleUri)
					return;
			}

			var maybeModuleSource = moduleResolver.value.ResolveModuleSource(currentSource.uri, moduleUri);
			if (!maybeModuleSource.isSome)
			{
				io.AddSoftError(slice, "Could not resolve module source '{0}' from '{1}'", moduleUri, currentSource.uri);
				return;
			}

			Compile(new Source(moduleUri, maybeModuleSource.value));
		}

		private void FunctionDeclaration(bool isPublic)
		{
			io.parser.Consume(TokenKind.Identifier, "Expected function name");
			ConsumeFunction(io.parser.previousToken.slice, isPublic);
		}

		internal static ValueType FunctionExpression(Compiler self)
		{
			var functionJump = self.io.BeginEmitForwardJump(Instruction.JumpForward);
			self.ConsumeFunction(new Slice(), false);
			self.io.EndEmitForwardJump(functionJump);

			var functionIndex = self.io.chunk.functions.count - 1;
			var function = self.io.chunk.functions.buffer[functionIndex];

			self.io.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
			var type = new ValueType(TypeKind.Function, function.typeIndex);
			self.io.DebugEmitPushType(type);
			return type;
		}

		private void ConsumeFunction(Slice slice, bool isPublic)
		{
			const int MaxParamCount = 8;

			var requireBody = slice.length == 0;
			var source = io.parser.tokenizer.source;
			var builder = io.BeginFunctionDeclaration();
			var paramStartIndex = io.localVariables.count;

			io.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name");
			if (!io.parser.Check(TokenKind.CloseParenthesis))
			{
				do
				{
					var isMutable = io.parser.Match(TokenKind.Mut);
					io.parser.Consume(TokenKind.Identifier, "Expected parameter name");
					var paramSlice = io.parser.previousToken.slice;
					io.parser.Consume(TokenKind.Colon, "Expected ':' after parameter name");
					var paramType = io.ParseType("Expected parameter type");
					var paramAndTypeSlice = Slice.FromTo(paramSlice, io.parser.previousToken.slice);

					if (builder.parameterCount >= MaxParamCount)
					{
						io.AddSoftError(paramAndTypeSlice, "Function can not have more than {0} parameters", MaxParamCount);
						continue;
					}

					var hasDuplicatedParameter = false;
					for (var i = 0; i < builder.parameterCount; i++)
					{
						var otherSlice = io.localVariables.buffer[paramStartIndex + i].slice;
						if (CompilerHelper.AreEqual(source, paramSlice, otherSlice))
						{
							hasDuplicatedParameter = true;
							break;
						}
					}

					if (hasDuplicatedParameter)
					{
						io.AddSoftError(paramAndTypeSlice, "Function already has a parameter named '{0}'", CompilerHelper.GetSlice(io, paramSlice));
						continue;
					}

					var paramFlags = VariableFlags.Used;
					if (isMutable)
						paramFlags |= VariableFlags.Mutable;
					io.AddLocalVariable(paramSlice, paramType, paramFlags);
					builder.WithParam(paramType);
				} while (io.parser.Match(TokenKind.Comma) || io.parser.Match(TokenKind.End));
			}
			io.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function parameter list");

			if (io.parser.Match(TokenKind.Colon))
			{
				var returnSlice = io.parser.currentToken.slice;
				builder.returnType = io.ParseType("Expected function return type");
				returnSlice = Slice.FromTo(returnSlice, io.parser.previousToken.slice);
				if (builder.returnType.IsReference)
					io.AddSoftError(returnSlice, "Function can not return a reference");
			}

			if (requireBody)
			{
				io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before function body");
			}
			else if (!io.parser.Match(TokenKind.OpenCurlyBrackets))
			{
				io.localVariables.count -= builder.parameterCount;
				io.EndFunctionDeclaration(builder, slice, isPublic, false);
				return;
			}

			io.functionReturnTypeStack.PushBack(builder.returnType);
			var functionIndex = io.EndFunctionDeclaration(builder, slice, isPublic, true);

			{
				var functionTypeIndex = io.chunk.functions.buffer[functionIndex].typeIndex;
				var functionType = io.chunk.functionTypes.buffer[functionTypeIndex];
				io.DebugEmitPushFrame();
				io.DebugEmitPushType(new ValueType(TypeKind.Function, functionTypeIndex));
				for (var i = 0; i < functionType.parameters.length; i++)
				{
					var paramType = io.chunk.functionParamTypes.buffer[functionType.parameters.index + i];
					io.DebugEmitPushType(paramType);
				}
			}

			if (builder.returnType.IsKind(TypeKind.Unit))
			{
				BlockStatement();
				io.EmitInstruction(Instruction.LoadUnit);
			}
			else
			{
				var type = Block(this);
				if (!builder.returnType.Accepts(type))
					io.AddSoftError(io.parser.previousToken.slice, "Wrong return type. Expected {0}. Got {1}", builder.returnType.ToString(io.chunk), type.ToString(io.chunk));
			}

			{
				io.DebugEmitPopFrame();
				io.DebugEmitPushType(builder.returnType);
			}

			io.EmitInstruction(Instruction.Return);
			io.EmitByte(builder.returnType.GetSize(io.chunk));

			io.functionReturnTypeStack.PopLast();
			io.localVariables.count -= builder.parameterCount;
		}

		private void StructDeclaration(bool isPublic)
		{
			io.parser.Consume(TokenKind.Identifier, "Expected struct name");
			var slice = io.parser.previousToken.slice;

			var source = io.parser.tokenizer.source;
			var builder = io.BeginStructDeclaration();
			var fieldStartIndex = io.chunk.structTypeFields.count;

			io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before struct fields");
			while (
				!io.parser.Check(TokenKind.CloseCurlyBrackets) &&
				!io.parser.Check(TokenKind.End)
			)
			{
				io.parser.Consume(TokenKind.Identifier, "Expected field name");
				var fieldSlice = io.parser.previousToken.slice;
				io.parser.Consume(TokenKind.Colon, "Expected ':' after field name");
				var fieldType = io.ParseType("Expected field type");
				var fieldAndTypeSlice = Slice.FromTo(fieldSlice, io.parser.previousToken.slice);

				if (fieldType.IsReference)
					io.AddSoftError(fieldAndTypeSlice, "Struct can not contain reference fields");

				if (!io.parser.Check(TokenKind.CloseCurlyBrackets))
					io.parser.Consume(TokenKind.Comma, "Expected ',' after field type");

				var hasDuplicate = false;
				for (var i = 0; i < builder.fieldCount; i++)
				{
					var otherName = io.chunk.structTypeFields.buffer[fieldStartIndex + i].name;
					if (CompilerHelper.AreEqual(source, fieldSlice, otherName))
					{
						hasDuplicate = true;
						break;
					}
				}

				var fieldName = CompilerHelper.GetSlice(io, fieldSlice);
				if (hasDuplicate)
				{
					io.AddSoftError(fieldAndTypeSlice, "Struct already has a field named '{0}'", fieldName);
					continue;
				}

				builder.WithField(fieldName, fieldType);
			}
			io.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct fields");

			io.EndStructDeclaration(builder, slice, isPublic);
		}

		internal static ValueType FinishTupleExpression(Compiler self, ExpressionResult firstElement)
		{
			var builder = self.io.chunk.BeginTupleType();

			if (firstElement.type.IsReference)
				self.io.AddSoftError(firstElement.slice, "Can not create tuple containing references");
			builder.WithElement(firstElement.type);

			while (
				!self.io.parser.Check(TokenKind.CloseCurlyBrackets) &&
				!self.io.parser.Check(TokenKind.End)
			)
			{
				self.io.parser.Consume(TokenKind.Comma, "Expected ',' after element value expression");
				var expression = Expression(self);
				if (expression.type.IsReference)
					self.io.AddSoftError(expression.slice, "Can not create tuple containing references");
				builder.WithElement(expression.type);
			}
			self.io.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after tuple expression");

			var slice = Slice.FromTo(firstElement.slice, self.io.parser.previousToken.slice);

			var result = builder.Build(out var typeIndex);
			var type = self.io.CheckTupleBuild(result, slice) ?
				new ValueType(TypeKind.Tuple, typeIndex) :
				new ValueType(TypeKind.Unit);

			{
				self.io.DebugEmitPopTypes((byte)builder.elementCount);
				self.io.DebugEmitPushType(type);
			}

			return type;
		}

		internal static ValueType ArrayExpression(Compiler self)
		{
			var element = Expression(self);
			if (element.type.IsArray)
				self.io.AddSoftError(element.slice, "Can not create array of arrays");
			if (element.type.IsReference)
				self.io.AddSoftError(element.slice, "Can not create array of references");

			var arrayType = element.type.ToArrayType();

			if (self.io.parser.Match(TokenKind.CloseSquareBrackets))
			{
				self.io.DebugEmitPopTypes(1);
				self.io.EmitInstruction(Instruction.CreateArrayFromStack);
				self.io.EmitByte(element.type.GetSize(self.io.chunk));
				self.io.EmitByte(1);
			}
			else if (self.io.parser.Check(TokenKind.Comma))
			{
				var arrayLength = 1;

				while (
					!self.io.parser.Check(TokenKind.CloseSquareBrackets) &&
					!self.io.parser.Check(TokenKind.End)
				)
				{
					self.io.parser.Consume(TokenKind.Comma, "Expected ',' after array element expression");
					var otherElement = Expression(self);
					if (!otherElement.type.IsEqualTo(element.type))
						self.io.AddSoftError(
							element.slice,
							"Array must have all elements of the same type. Expected {0}. Got {1}",
							element.type.ToString(self.io.chunk),
							otherElement.type.ToString(self.io.chunk)
						);
					arrayLength += 1;
				}

				self.io.parser.Consume(TokenKind.CloseSquareBrackets, "Expected ']' after array expression");

				if (arrayLength <= byte.MaxValue)
				{
					self.io.DebugEmitPopTypes((byte)arrayLength);
					self.io.EmitInstruction(Instruction.CreateArrayFromStack);
					self.io.EmitByte(element.type.GetSize(self.io.chunk));
					self.io.EmitByte((byte)arrayLength);
				}
				else
				{
					var slice = Slice.FromTo(element.slice, self.io.parser.previousToken.slice);
					self.io.AddSoftError(slice, "Array length is too big. Max is {0}", byte.MaxValue);
				}
			}
			else if (self.io.parser.Match(TokenKind.Colon))
			{
				var length = Expression(self);
				if (!length.type.IsKind(TypeKind.Int))
					self.io.AddSoftError(length.slice, "Expected int expression for array length. Got {0}", length.type.ToString(self.io.chunk));

				self.io.parser.Consume(TokenKind.CloseSquareBrackets, "Expected ']' after array expression");

				self.io.DebugEmitPopTypes(2);
				self.io.EmitInstruction(Instruction.CreateArrayFromDefault);
				self.io.EmitByte(element.type.GetSize(self.io.chunk));
			}
			else
			{
				self.io.AddHardError(
					self.io.parser.currentToken.slice,
					"Expected array expression"
				);
			}

			self.io.DebugEmitPushType(arrayType);
			return arrayType;
		}

		internal static ValueType LengthExpression(Compiler self)
		{
			var expression = Expression(self);
			self.io.DebugEmitPopTypes(0);

			if (!expression.type.IsArray)
				self.io.AddSoftError(expression.slice, "Expected array type. Got {0}", expression.type.ToString(self.io.chunk));

			self.io.EmitInstruction(Instruction.LoadArrayLength);

			self.io.DebugEmitPushType(new ValueType(TypeKind.Int));
			return new ValueType(TypeKind.Int);
		}

		private void Statement(out ValueType type, out StatementKind kind)
		{
			type = new ValueType(TypeKind.Unit);
			kind = StatementKind.Other;

			if (io.parser.Match(TokenKind.OpenCurlyBrackets))
				BlockStatement();
			else if (io.parser.Match(TokenKind.Let))
				VariableDeclaration();
			else if (io.parser.Match(TokenKind.Set))
				SetStatement();
			else if (io.parser.Match(TokenKind.While))
				WhileStatement();
			else if (io.parser.Match(TokenKind.Repeat))
				RepeatStatement();
			else if (io.parser.Match(TokenKind.Break))
				BreakStatement();
			else if (io.parser.Match(TokenKind.Return))
				(type, kind) = (ReturnStatement(), StatementKind.Return);
			else if (io.parser.Match(TokenKind.Print))
				PrintStatement();
			else
				(type, kind) = (ExpressionStatement(), StatementKind.Expression);
		}

		private ValueType ExpressionStatement()
		{
			var expression = Expression(this);

			if (!io.parser.Check(TokenKind.CloseCurlyBrackets))
			{
				io.EmitPop(expression.type.GetSize(io.chunk));
				io.DebugEmitPopTypes(1);
			}

			return expression.type;
		}

		private void BlockStatement()
		{
			var scope = io.BeginScope();
			ValueType lastStatementType = new ValueType(TypeKind.Unit);
			StatementKind lastStatementKind = StatementKind.Other;
			while (
				!io.parser.Check(TokenKind.CloseCurlyBrackets) &&
				!io.parser.Check(TokenKind.End)
			)
			{
				Statement(out lastStatementType, out lastStatementKind);
			}

			if (lastStatementKind == StatementKind.Expression)
			{
				io.EmitPop(lastStatementType.GetSize(io.chunk));
				io.DebugEmitPopTypes(1);
			}

			io.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");
			io.EndScope(scope, 0);
		}

		private void VariableDeclaration()
		{
			if (io.parser.Match(TokenKind.OpenCurlyBrackets))
				MultipleVariableDeclaration();
			else
				SingleVariableDeclaration();
		}

		private int SingleVariableDeclaration()
		{
			var isMutable = io.parser.Match(TokenKind.Mut);
			io.parser.Consume(TokenKind.Identifier, "Expected variable name");
			var slice = io.parser.previousToken.slice;

			io.parser.Consume(TokenKind.Equal, "Expected assignment");
			var expression = Expression(this);

			return io.AddLocalVariable(slice, expression.type, isMutable ? VariableFlags.Mutable : VariableFlags.None);
		}

		private void MultipleVariableDeclaration()
		{
			var declarations = new Buffer<VariableDeclarationInfo>(8);

			while (
				!io.parser.Check(TokenKind.CloseCurlyBrackets) &&
				!io.parser.Check(TokenKind.End)
			)
			{
				var isMutable = io.parser.Match(TokenKind.Mut);
				io.parser.Consume(TokenKind.Identifier, "Expected variable name");
				declarations.PushBackUnchecked(new VariableDeclarationInfo
				{
					slice = io.parser.previousToken.slice,
					isMutable = isMutable
				});

				if (!io.parser.Check(TokenKind.CloseCurlyBrackets))
					io.parser.Consume(TokenKind.Comma, "Expected ',' after variable name");
			}
			io.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after variable names");
			io.parser.Consume(TokenKind.Equal, "Expected assignment");
			var expression = Expression(this);

			if (expression.type.kind != TypeKind.Tuple || expression.type.flags != TypeFlags.None)
			{
				io.AddSoftError(expression.slice, "Expression must be a tuple");
				return;
			}

			var tupleElements = io.chunk.tupleTypes.buffer[expression.type.index].elements;
			if (tupleElements.length != declarations.count)
			{
				io.AddSoftError(
					expression.slice,
					"Tuple element count must be equal to variable declaration count. Expected {0}. Got {1}",
					declarations.count,
					tupleElements.length
				);
				return;
			}

			for (var i = 0; i < declarations.count; i++)
			{
				var declaration = declarations.buffer[i];
				var elementType = io.chunk.tupleElementTypes.buffer[tupleElements.index + i];
				io.AddLocalVariable(declaration.slice, elementType, declaration.isMutable ? VariableFlags.Mutable : VariableFlags.None);
			}
		}

		private void SetStatement()
		{
			io.parser.Consume(TokenKind.Identifier, "Expected identifier");
			var slice = io.parser.previousToken.slice;

			if (io.ResolveToLocalVariableIndex(slice, out var variableIndex))
			{
				ref var localVar = ref io.localVariables.buffer[variableIndex];
				localVar.flags |= VariableFlags.Changed;

				var storage = GetStorage(this, variableIndex, localVar.type, ref slice);
				if (io.parser.Match(TokenKind.OpenSquareBrackets))
				{
					Access(this, slice, storage);
					SetArrayElement(slice, storage.type, localVar.IsMutable);
				}
				else if (io.parser.Match(TokenKind.OpenParenthesis))
				{
					Access(this, slice, storage);
					SetFunctionReturn(slice, storage.type);
				}
				else
				{
					io.parser.Consume(TokenKind.Equal, "Expected '=' before expression");
					Assign(this, slice, storage);
				}
			}
			else if (io.ResolveToFunctionIndex(slice, out var functionIndex))
			{
				io.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
				var function = io.chunk.functions.buffer[functionIndex];
				var type = new ValueType(TypeKind.Function, function.typeIndex);

				io.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name on set statement");
				SetFunctionReturn(slice, type);
			}
			else if (io.ResolveToNativeFunctionIndex(slice, out var nativeFunctionIndex))
			{
				io.EmitLoadFunction(Instruction.LoadNativeFunction, nativeFunctionIndex);
				var function = io.chunk.nativeFunctions.buffer[nativeFunctionIndex];
				var type = new ValueType(TypeKind.NativeFunction, function.typeIndex);

				io.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function name on set statement");
				SetFunctionReturn(slice, type);
			}
			else
			{
				io.AddHardError(slice, "Could not find variable or function named '{0}'", CompilerHelper.GetSlice(io, slice));
				return;
			}
		}

		private void SetArrayElement(Slice slice, ValueType arrayType, bool isMutable)
		{
			var storage = GetIndexStorage(this, arrayType, ref slice);

			if (io.parser.Match(TokenKind.OpenSquareBrackets))
			{
				IndexAccess(this, storage);
				SetArrayElement(slice, storage.type, isMutable);
			}
			else if (io.parser.Match(TokenKind.OpenParenthesis))
			{
				IndexAccess(this, storage);
				SetFunctionReturn(slice, storage.type);
			}
			else
			{
				io.parser.Consume(TokenKind.Equal, "Expected '=' before expression");
				if (!isMutable)
					io.AddSoftError(slice, "Can not write to immutable variable. Try adding 'mut' after 'let' at its declaration");

				IndexAssign(this, storage);
			}
		}

		private void SetFunctionReturn(Slice slice, ValueType functionType)
		{
			io.DebugEmitPushType(functionType);

			var type = Call(this, new ExpressionResult(slice, functionType));
			slice = Slice.FromTo(slice, io.parser.previousToken.slice);

			if (io.parser.Match(TokenKind.Dot))
			{
				type = Dot(this, new ExpressionResult(slice, type));
				slice = Slice.FromTo(slice, io.parser.previousToken.slice);
			}

			if (io.parser.Match(TokenKind.OpenSquareBrackets))
				SetArrayElement(slice, type, true);
			else if (io.parser.Match(TokenKind.OpenParenthesis))
				SetFunctionReturn(slice, type);
			else
				io.AddHardError(slice, "Can not write to temporary value. Try assigning it to a variable first");
		}

		private void WhileStatement()
		{
			var labelSlice = new Slice();
			if (io.parser.Match(TokenKind.Colon))
			{
				io.parser.Consume(TokenKind.Identifier, "Expected loop label name");
				labelSlice = io.parser.previousToken.slice;
			}

			var loopJump = io.BeginEmitBackwardJump();
			var expression = Expression(this);

			if (!expression.type.IsKind(TypeKind.Bool))
				io.AddSoftError(expression.slice, "Expected bool expression as while condition");

			io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after while statement");

			io.DebugEmitPopTypes(1);
			var breakJump = io.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
			io.BeginLoop(labelSlice);
			BlockStatement();

			io.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
			io.EndEmitForwardJump(breakJump);
			io.EndLoop();
		}

		private void RepeatStatement()
		{
			var labelSlice = new Slice();
			if (io.parser.Match(TokenKind.Colon))
			{
				io.parser.Consume(TokenKind.Identifier, "Expected loop label name");
				labelSlice = io.parser.previousToken.slice;
			}

			var scope = io.BeginScope();

			io.EmitLoadLiteral(new ValueData(0), TypeKind.Int);
			io.DebugEmitPushType(new ValueType(TypeKind.Int));

			var itVarIndex = io.AddLocalVariable(new Slice(), new ValueType(TypeKind.Int), VariableFlags.Used | VariableFlags.Iteration);
			var itVar = io.localVariables.buffer[itVarIndex];

			var expression = Expression(this);
			if (!expression.type.IsKind(TypeKind.Int))
				io.AddSoftError(expression.slice, "Expected expression of type int as repeat count");
			var countVarIndex = io.AddLocalVariable(new Slice(), new ValueType(TypeKind.Int), VariableFlags.Used);

			io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after repeat statement");

			var loopJump = io.BeginEmitBackwardJump();
			io.EmitInstruction(Instruction.RepeatLoopCheck);
			io.EmitByte((byte)itVar.stackIndex);

			io.DebugEmitPushType(new ValueType(TypeKind.Bool));
			io.DebugEmitPopTypes(1);
			var breakJump = io.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
			io.BeginLoop(labelSlice);
			BlockStatement();

			io.EmitInstruction(Instruction.IncrementLocalInt);
			io.EmitByte((byte)itVar.stackIndex);

			io.EndEmitBackwardJump(Instruction.JumpBackward, loopJump);
			io.EndEmitForwardJump(breakJump);
			io.EndLoop();

			io.EndScope(scope, 0);
		}

		private void BreakStatement()
		{
			var slice = io.parser.previousToken.slice;
			var breakJump = io.BeginEmitForwardJump(Instruction.JumpForward);

			var nestingIndex = -1;

			if (io.loopNesting.count == 0)
				io.AddSoftError(slice, "Not inside a loop");
			else
				nestingIndex = io.loopNesting.count - 1;

			if (io.parser.Match(TokenKind.Colon))
			{
				io.parser.Consume(TokenKind.Identifier, "Expected loop label name");
				var labelSlice = io.parser.previousToken.slice;
				slice = Slice.FromTo(slice, labelSlice);

				var source = io.parser.tokenizer.source;
				for (var i = 0; i < io.loopNesting.count; i++)
				{
					var loopLabelSlice = io.loopNesting.buffer[i];
					if (CompilerHelper.AreEqual(source, labelSlice, loopLabelSlice))
					{
						nestingIndex = i;
						break;
					}
				}

				if (nestingIndex < 0)
					io.AddSoftError(labelSlice, "Could not find an enclosing loop with label '{0}'", CompilerHelper.GetSlice(io, labelSlice));
			}

			if (nestingIndex > byte.MaxValue)
			{
				io.AddHardError(slice, "Break is nested too deeply. Max loop nesting level is {0}", byte.MaxValue);
				nestingIndex = -1;
			}

			if (nestingIndex >= 0)
				io.loopBreaks.PushBack(new LoopBreak(breakJump, (byte)nestingIndex));
		}

		private ValueType ReturnStatement()
		{
			var expectedType = io.functionReturnTypeStack.buffer[io.functionReturnTypeStack.count - 1];

			var expression = new ExpressionResult(io.parser.previousToken.slice, new ValueType(TypeKind.Unit));
			if (expectedType.IsKind(TypeKind.Unit))
				io.EmitInstruction(Instruction.LoadUnit);
			else
				expression = Expression(this);

			{
				io.DebugEmitPopFrame();
				io.DebugEmitPushType(expectedType);
			}

			io.EmitInstruction(Instruction.Return);
			io.EmitByte(expectedType.GetSize(io.chunk));

			if (!expectedType.Accepts(expression.type))
				io.AddSoftError(
					expression.slice,
					"Wrong return type. Expected {0}. Got {1}",
					expectedType.ToString(io.chunk),
					expression.type.ToString(io.chunk)
				);

			return expression.type;
		}

		private void PrintStatement()
		{
			var expression = Expression(this);

			io.DebugEmitPopTypes(1);

			io.EmitInstruction(Instruction.Print);
			io.EmitType(expression.type);
		}

		internal static ExpressionResult Expression(Compiler self)
		{
			return ParseWithPrecedence(self, Precedence.Assignment);
		}

		internal static ValueType Grouping(Compiler self)
		{
			var result = Expression(self);
			self.io.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after expression");
			return result.type;
		}

		internal static ValueType BlockOrTupleExpression(Compiler self)
		{
			if (self.io.parser.Match(TokenKind.CloseCurlyBrackets))
			{
				self.io.EmitInstruction(Instruction.LoadUnit);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Unit));
				return new ValueType(TypeKind.Unit);
			}
			else if (
				self.io.parser.Check(TokenKind.Let) ||
				self.io.parser.Check(TokenKind.Set) ||
				self.io.parser.Check(TokenKind.While) ||
				self.io.parser.Check(TokenKind.Repeat) ||
				self.io.parser.Check(TokenKind.Break) ||
				self.io.parser.Check(TokenKind.Return) ||
				self.io.parser.Check(TokenKind.Print)
			)
			{
				var scope = self.io.BeginScope();
				self.Statement(out var firstStatementType, out var firstStatementKind);
				return FinishBlock(self, scope, firstStatementType, firstStatementKind);
			}
			else
			{
				var scope = self.io.BeginScope();
				var expression = Expression(self);
				if (self.io.parser.Check(TokenKind.Comma))
				{
					self.io.scopeDepth -= 1;
					return FinishTupleExpression(self, expression);
				}
				else
				{
					return FinishBlock(self, scope, expression.type, StatementKind.Expression);
				}
			}
		}

		internal static ValueType Block(Compiler self)
		{
			if (self.io.parser.Match(TokenKind.CloseCurlyBrackets))
			{
				self.io.EmitInstruction(Instruction.LoadUnit);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Unit));
				return new ValueType(TypeKind.Unit);
			}

			var scope = self.io.BeginScope();
			self.Statement(out var firstStatementType, out var firstStatementKind);
			return FinishBlock(self, scope, firstStatementType, firstStatementKind);
		}

		internal static ValueType FinishBlock(Compiler self, Scope scope, ValueType lastStatementType, StatementKind lastStatementKind)
		{
			while (
				!self.io.parser.Check(TokenKind.CloseCurlyBrackets) &&
				!self.io.parser.Check(TokenKind.End)
			)
			{
				self.Statement(out lastStatementType, out lastStatementKind);
			}

			self.io.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after block.");

			var sizeLeftOnStack = lastStatementKind == StatementKind.Expression ?
				lastStatementType.GetSize(self.io.chunk) :
				0;

			self.io.EndScope(scope, sizeLeftOnStack);

			if (lastStatementKind == StatementKind.Other)
			{
				self.io.EmitInstruction(Instruction.LoadUnit);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Unit));
				return new ValueType(TypeKind.Unit);
			}
			else
			{
				self.io.DebugEmitPushType(lastStatementType);
				return lastStatementType;
			}
		}

		internal static ValueType If(Compiler self)
		{
			var expression = Expression(self);

			if (!expression.type.IsKind(TypeKind.Bool))
				self.io.AddSoftError(expression.slice, "Expected bool expression as if condition");

			self.io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after if expression");

			self.io.DebugEmitPopTypes(1);
			var elseJump = self.io.BeginEmitForwardJump(Instruction.PopAndJumpForwardIfFalse);
			var thenType = Block(self);
			var hasElse = self.io.parser.Match(TokenKind.Else);

			if (!hasElse && !thenType.IsKind(TypeKind.Unit))
			{
				var size = thenType.GetSize(self.io.chunk);
				self.io.EmitPop(size);
				self.io.DebugEmitPopTypes(1);
				self.io.EmitInstruction(Instruction.LoadUnit);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Unit));
				thenType = new ValueType(TypeKind.Unit);
			}

			var thenJump = self.io.BeginEmitForwardJump(Instruction.JumpForward);
			self.io.EndEmitForwardJump(elseJump);

			if (hasElse)
			{
				var elseType = new ValueType(TypeKind.Unit);
				if (self.io.parser.Match(TokenKind.If))
				{
					elseType = If(self);
				}
				else
				{
					self.io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after else");
					elseType = Block(self);
				}

				if (!thenType.Accepts(elseType))
					self.io.AddSoftError(self.io.parser.previousToken.slice, "If expression must produce values of the same type on both branches. Found types: {0} and {1}", thenType, elseType);
			}
			else
			{
				self.io.EmitInstruction(Instruction.LoadUnit);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Unit));
			}

			self.io.EndEmitForwardJump(thenJump);
			return thenType;
		}

		internal static ValueType And(Compiler self, ExpressionResult previous)
		{
			if (!previous.type.IsKind(TypeKind.Bool))
				self.io.AddSoftError(previous.slice, "Expected bool expression before '&&'");

			var jump = self.io.BeginEmitForwardJump(Instruction.JumpForwardIfFalse);
			self.io.EmitInstruction(Instruction.Pop);
			self.io.DebugEmitPopTypes(1);
			var expression = ParseWithPrecedence(self, Precedence.And);
			self.io.EndEmitForwardJump(jump);

			if (!expression.type.IsKind(TypeKind.Bool))
				self.io.AddSoftError(expression.slice, "Expected bool expression after '&&'");

			return new ValueType(TypeKind.Bool);
		}

		internal static ValueType Or(Compiler self, ExpressionResult previous)
		{
			if (!previous.type.IsKind(TypeKind.Bool))
				self.io.AddSoftError(previous.slice, "Expected bool expression before '||'");

			var jump = self.io.BeginEmitForwardJump(Instruction.JumpForwardIfTrue);
			self.io.EmitInstruction(Instruction.Pop);
			self.io.DebugEmitPopTypes(1);
			var expression = ParseWithPrecedence(self, Precedence.Or);
			self.io.EndEmitForwardJump(jump);

			if (!expression.type.IsKind(TypeKind.Bool))
				self.io.AddSoftError(expression.slice, "Expected bool expression after '||'");

			return new ValueType(TypeKind.Bool);
		}

		internal static ValueType Literal(Compiler self)
		{
			switch (self.io.parser.previousToken.kind)
			{
			case TokenKind.True:
				self.io.EmitInstruction(Instruction.LoadTrue);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Bool));
				return new ValueType(TypeKind.Bool);
			case TokenKind.False:
				self.io.EmitInstruction(Instruction.LoadFalse);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Bool));
				return new ValueType(TypeKind.Bool);
			case TokenKind.IntLiteral:
				self.io.EmitLoadLiteral(
					new ValueData(CompilerHelper.GetParsedInt(self.io)),
					TypeKind.Int
				);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Int));
				return new ValueType(TypeKind.Int);
			case TokenKind.FloatLiteral:
				self.io.EmitLoadLiteral(
					new ValueData(CompilerHelper.GetParsedFloat(self.io)),
					TypeKind.Float
				);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Float));
				return new ValueType(TypeKind.Float);
			case TokenKind.StringLiteral:
				self.io.EmitLoadStringLiteral(CompilerHelper.GetParsedString(self.io));
				self.io.DebugEmitPushType(new ValueType(TypeKind.String));
				return new ValueType(TypeKind.String);
			default:
				self.io.AddHardError(
					self.io.parser.previousToken.slice,
					string.Format("Expected literal. Got {0}", self.io.parser.previousToken.kind)
				);
				return new ValueType(TypeKind.Unit);
			}
		}

		private static Storage GetStorage(Compiler self, int variableIndex, ValueType type, ref Slice slice)
		{
			type = type.ToReferredType();

			var hasError = false;
			byte offset = 0;

			while (self.io.parser.Match(TokenKind.Dot) || self.io.parser.Match(TokenKind.End))
			{
				if (hasError)
					continue;

				hasError = !FieldAccess(
					self,
					ref slice,
					ref type,
					ref offset
				);
			}

			return new Storage
			{
				variableIndex = variableIndex,
				type = type,
				offset = offset,
			};
		}

		internal static ValueType Identifier(Compiler self)
		{
			var slice = self.io.parser.previousToken.slice;
			var storage = new Storage { variableIndex = -1 };

			if (self.io.ResolveToLocalVariableIndex(slice, out var variableIndex))
			{
				var localVar = self.io.localVariables.buffer[variableIndex];
				storage = GetStorage(self, variableIndex, localVar.type, ref slice);
			}

			return Access(self, slice, storage);
		}

		private static void Assign(Compiler self, Slice slice, Storage storage)
		{
			if (storage.variableIndex >= 0)
			{
				ref var localVar = ref self.io.localVariables.buffer[storage.variableIndex];

				var storageIsMutable = localVar.IsMutable || localVar.type.IsMutable;
				if (!storageIsMutable)
					self.io.AddSoftError(slice, "Can not write to immutable variable");

				var expression = Expression(self);
				if (!storage.type.Accepts(expression.type))
				{
					self.io.AddSoftError(
						expression.slice,
						"Wrong type for assignment. Expected {0}. Got {1}",
						storage.type.ToString(self.io.chunk),
						expression.type.ToString(self.io.chunk)
					);
				}

				self.io.DebugEmitPopTypes(1);

				if (localVar.type.IsReference)
				{
					if (localVar.type.IsMutable)
						localVar.flags |= VariableFlags.Used;

					self.io.EmitInstruction(Instruction.SetReference);
					self.io.EmitByte(localVar.stackIndex);
					self.io.EmitByte(storage.offset);
					self.io.EmitByte(storage.type.GetSize(self.io.chunk));
				}
				else
				{
					self.io.EmitSetLocal(localVar.stackIndex + storage.offset, storage.type);
				}
			}
			else
			{
				Expression(self);
				self.io.AddSoftError(slice, "Can not write to undeclared variable. Try declaring it with 'let mut'");
			}
		}

		private static ValueType Access(Compiler self, Slice slice, Storage storage)
		{
			if (storage.variableIndex >= 0)
			{
				ref var localVar = ref self.io.localVariables.buffer[storage.variableIndex];
				localVar.flags |= VariableFlags.Used;

				if (localVar.type.IsReference)
				{
					self.io.EmitInstruction(Instruction.LoadReference);
					self.io.EmitByte(localVar.stackIndex);
					self.io.EmitByte(storage.offset);
					self.io.EmitByte(storage.type.GetSize(self.io.chunk));

					self.io.DebugEmitPushType(storage.type);
				}
				else
				{
					self.io.EmitLoadLocal(localVar.stackIndex + storage.offset, storage.type);
					self.io.DebugEmitPushType(storage.type);
				}

				return storage.type;
			}
			else if (self.io.ResolveToFunctionIndex(slice, out var functionIndex))
			{
				self.io.EmitLoadFunction(Instruction.LoadFunction, functionIndex);
				var function = self.io.chunk.functions.buffer[functionIndex];
				var type = new ValueType(TypeKind.Function, function.typeIndex);
				self.io.DebugEmitPushType(type);
				return type;
			}
			else if (self.io.ResolveToNativeFunctionIndex(slice, out var nativeFunctionIndex))
			{
				self.io.EmitLoadFunction(Instruction.LoadNativeFunction, nativeFunctionIndex);
				var function = self.io.chunk.nativeFunctions.buffer[nativeFunctionIndex];
				var type = new ValueType(TypeKind.NativeFunction, function.typeIndex);
				self.io.DebugEmitPushType(type);
				return type;
			}
			else if (self.io.ResolveToStructTypeIndex(slice, out var structIndex))
			{
				var structType = self.io.chunk.structTypes.buffer[structIndex];
				var currentFieldIndex = 0;
				self.io.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' before struct initializer");
				while (
					!self.io.parser.Check(TokenKind.CloseCurlyBrackets) &&
					!self.io.parser.Check(TokenKind.End)
				)
				{
					Option<StructTypeField> field = Option.None;
					if (currentFieldIndex < structType.fields.length)
					{
						var fieldIndex = structType.fields.index + currentFieldIndex;
						field = self.io.chunk.structTypeFields.buffer[fieldIndex];
					}
					else if (currentFieldIndex == structType.fields.length)
					{
						self.io.AddSoftError(
							self.io.parser.currentToken.slice,
							"Too many fields in struct creation"
						);
					}

					var fieldSlice = self.io.parser.currentToken.slice;
					self.io.parser.Consume(TokenKind.Identifier, "Expected field name");
					if (field.isSome && !CompilerHelper.AreEqual(self.io.parser.tokenizer.source, fieldSlice, fieldSlice))
					{
						self.io.AddSoftError(
							fieldSlice,
							"Expected field name '{0}'. Struct fields must be created in declaration order",
							field.value.name
						);
					}

					self.io.parser.Consume(TokenKind.Equal, "Expected '=' after field name");

					var expression = Expression(self);
					fieldSlice = Slice.FromTo(fieldSlice, expression.slice);

					if (!self.io.parser.Check(TokenKind.CloseCurlyBrackets))
						self.io.parser.Consume(TokenKind.Comma, "Expected ',' after field value expression");

					if (field.isSome && !field.value.type.Accepts(expression.type))
					{
						self.io.AddSoftError(
							fieldSlice,
							"Wrong type for field '{0}' initializer. Expected {1}. Got {2}",
							field.value.name,
							field.value.type.ToString(self.io.chunk),
							expression.type.ToString(self.io.chunk)
						);
					}

					currentFieldIndex += 1;
				}
				if (currentFieldIndex < structType.fields.length)
				{
					self.io.AddSoftError(
						self.io.parser.previousToken.slice,
						"Too few fields in struct creation"
					);
				}
				self.io.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after struct initializer");
				self.io.DebugEmitPopTypes((byte)structType.fields.length);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Struct, structIndex));
				return new ValueType(TypeKind.Struct, structIndex);
			}
			else
			{
				self.io.AddSoftError(slice, "Can not read undeclared variable. Declare it with 'let'");
				return new ValueType(TypeKind.Unit);
			}
		}

		internal static bool FieldAccess(Compiler self, ref Slice slice, ref ValueType type, ref byte stackOffset)
		{
			if (!self.io.chunk.GetStructType(type, out var structType))
			{
				type = new ValueType(TypeKind.Unit);
				self.io.AddSoftError(slice, "Accessed value must be a struct");
				return false;
			}

			var structTypeIndex = type.index;

			self.io.parser.Consume(TokenKind.Identifier, "Expected field name");
			slice = self.io.parser.previousToken.slice;

			byte offset = 0;
			var source = self.io.parser.tokenizer.source;

			for (var i = 0; i < structType.fields.length; i++)
			{
				var fieldIndex = structType.fields.index + i;
				var field = self.io.chunk.structTypeFields.buffer[fieldIndex];
				if (CompilerHelper.AreEqual(source, slice, field.name))
				{
					type = field.type;
					stackOffset += offset;
					return true;
				}

				offset += field.type.GetSize(self.io.chunk);
			}

			var sb = new StringBuilder();
			self.io.chunk.FormatStructType(structTypeIndex, sb);
			self.io.AddSoftError(slice, "Could not find such field for struct of type {0}", sb);
			type = new ValueType(TypeKind.Unit);
			return false;
		}

		internal static ValueType Dot(Compiler self, ExpressionResult previous)
		{
			var storage = new Storage { type = previous.type };

			var slice = previous.slice;
			var structSize = storage.type.GetSize(self.io.chunk);

			byte offset = 0;
			if (FieldAccess(self, ref slice, ref storage.type, ref offset))
			{
				storage = GetStorage(self, -1, storage.type, ref slice);
				storage.offset += offset;
			}

			var fieldSize = storage.type.GetSize(self.io.chunk);
			var sizeAboveField = structSize - storage.offset - fieldSize;

			self.io.EmitPop(sizeAboveField);
			self.io.DebugEmitPopTypes(1);

			if (storage.offset > 0)
			{
				self.io.EmitInstruction(Instruction.Move);
				self.io.EmitByte(storage.offset);
				self.io.EmitByte(fieldSize);
			}

			self.io.DebugEmitPushType(storage.type);
			return storage.type;
		}

		private static IndexStorage GetIndexStorage(Compiler self, ValueType arrayType, ref Slice slice)
		{
			if (!arrayType.IsArray)
				self.io.AddSoftError(slice, "Can only index array types. Got {0}", arrayType.ToString(self.io.chunk));

			var indexExpression = Expression(self);
			if (!indexExpression.type.IsKind(TypeKind.Int))
				self.io.AddSoftError(indexExpression.slice, "Expected int expression for array index. Got {0}", indexExpression.type.ToString(self.io.chunk));

			self.io.parser.Consume(TokenKind.CloseSquareBrackets, "Expected ']' after array indexing");

			byte offset = 0;
			var storage = new IndexStorage();
			storage.type = arrayType.ToArrayElementType();
			storage.elementSize = storage.type.GetSize(self.io.chunk);

			var hasError = false;
			while (self.io.parser.Match(TokenKind.Dot) || self.io.parser.Match(TokenKind.End))
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

			return storage;
		}

		internal static ValueType Index(Compiler self, ExpressionResult previous)
		{
			var slice = previous.slice;
			var storage = GetIndexStorage(self, previous.type, ref slice);
			return IndexAccess(self, storage);
		}

		private static ValueType IndexAccess(Compiler self, IndexStorage storage)
		{
			self.io.DebugEmitPopTypes(2);

			self.io.EmitInstruction(Instruction.LoadArrayElement);
			self.io.EmitByte(storage.elementSize);
			self.io.EmitByte(storage.type.GetSize(self.io.chunk));
			self.io.EmitByte(storage.offset);

			self.io.DebugEmitPushType(storage.type);
			return storage.type;
		}

		private static void IndexAssign(Compiler self, IndexStorage storage)
		{
			var expression = Expression(self);
			if (!storage.type.Accepts(expression.type))
			{
				self.io.AddSoftError(
					expression.slice,
					"Wrong type for assignment. Expected {0}. Got {1}",
					storage.type.ToString(self.io.chunk),
					expression.type.ToString(self.io.chunk)
				);
			}

			self.io.DebugEmitPopTypes(3);

			self.io.EmitInstruction(Instruction.SetArrayElement);
			self.io.EmitByte(storage.elementSize);
			self.io.EmitByte(storage.type.GetSize(self.io.chunk));
			self.io.EmitByte(storage.offset);
		}

		internal static ValueType Call(Compiler self, ExpressionResult previous)
		{
			var slice = previous.slice;

			var type = previous.type;
			var isFunction = self.io.chunk.GetFunctionType(type, out var functionType);
			if (!isFunction)
				self.io.AddSoftError(slice, "Callee must be a function");

			var argIndex = 0;
			if (!self.io.parser.Check(TokenKind.CloseParenthesis))
			{
				do
				{
					var arg = Expression(self);

					if (isFunction && argIndex < functionType.parameters.length)
					{
						var paramType = self.io.chunk.functionParamTypes.buffer[functionType.parameters.index + argIndex];

						if (!paramType.Accepts(arg.type))
						{
							self.io.AddSoftError(
								arg.slice,
								"Wrong type for argument {0}. Expected {1}. Got {2}",
								argIndex + 1,
								paramType.ToString(self.io.chunk),
								arg.type.ToString(self.io.chunk)
							);
						}
					}

					argIndex += 1;
				} while (self.io.parser.Match(TokenKind.Comma) || self.io.parser.Match(TokenKind.End));
			}

			self.io.parser.Consume(TokenKind.CloseParenthesis, "Expect ')' after function argument list");

			slice = Slice.FromTo(slice, self.io.parser.previousToken.slice);

			if (isFunction && argIndex != functionType.parameters.length)
				self.io.AddSoftError(slice, "Wrong number of arguments. Expected {0}. Got {1}", functionType.parameters.length, argIndex);

			var popCount = isFunction ? functionType.parameters.length + 1 : 1;
			self.io.DebugEmitPopTypes((byte)popCount);

			if (type.kind == TypeKind.Function)
				self.io.EmitInstruction(Instruction.Call);
			else if (type.kind == TypeKind.NativeFunction)
				self.io.EmitInstruction(Instruction.CallNative);

			self.io.EmitByte((byte)(isFunction ? functionType.parametersSize : 0));
			var returnType = isFunction ? functionType.returnType : new ValueType(TypeKind.Unit);

			if (type.kind == TypeKind.NativeFunction)
				self.io.DebugEmitPushType(returnType);

			return returnType;
		}

		internal static ValueType Reference(Compiler self)
		{
			var isMutable = self.io.parser.Match(TokenKind.Mut);

			self.io.parser.Consume(TokenKind.Identifier, "Expected identifier");
			var slice = self.io.parser.previousToken.slice;

			if (self.io.ResolveToLocalVariableIndex(slice, out var variableIndex))
			{
				ref var localVar = ref self.io.localVariables.buffer[variableIndex];
				if (isMutable)
					localVar.flags |= VariableFlags.Changed;

				var storageIsMutable = localVar.IsMutable || localVar.type.IsMutable;
				if (isMutable && !storageIsMutable)
				{
					self.io.AddSoftError(slice, "Can not create a mutable reference to an immutable variable");
				}

				var storage = GetStorage(self, variableIndex, localVar.type, ref slice);
				var referredType = storage.type;
				if (self.io.parser.Match(TokenKind.OpenSquareBrackets))
				{
					Access(self, slice, storage);
					referredType = ReferenceArrayElement(self, slice, storage.type);
				}
				else
				{
					self.io.localVariables.buffer[storage.variableIndex].flags |= VariableFlags.Used;

					if (localVar.type.IsReference)
					{
						self.io.EmitLoadLocal(localVar.stackIndex, localVar.type);
						if (storage.offset > 0)
						{
							self.io.EmitLoadLiteral(new ValueData(storage.offset), TypeKind.Int);
							self.io.EmitInstruction(Instruction.AddInt);
						}
					}
					else
					{
						self.io.EmitInstruction(Instruction.CreateStackReference);
						self.io.EmitByte(storage.offset);
					}
				}

				var referenceType = referredType.ToReferenceType(isMutable);
				self.io.DebugEmitPushType(referenceType);
				return referenceType;
			}
			else
			{
				self.io.AddHardError(slice, "Could not find variable named '{0}'", CompilerHelper.GetSlice(self.io, slice));
				return new ValueType(TypeKind.Unit);
			}
		}

		private static ValueType ReferenceArrayElement(Compiler self, Slice slice, ValueType arrayType)
		{
			var storage = GetIndexStorage(self, arrayType, ref slice);

			if (self.io.parser.Match(TokenKind.OpenSquareBrackets))
			{
				IndexAccess(self, storage);
				return ReferenceArrayElement(self, slice, storage.type);
			}
			else
			{
				self.io.EmitInstruction(Instruction.CreateArrayElementReference);
				self.io.EmitByte(storage.elementSize);
				self.io.EmitByte(storage.offset);

				return storage.type;
			}
		}

		internal static ValueType Unary(Compiler self)
		{
			var opToken = self.io.parser.previousToken;
			var expression = ParseWithPrecedence(self, Precedence.Unary);

			switch (opToken.kind)
			{
			case TokenKind.Minus:
				if (expression.type.IsKind(TypeKind.Int))
				{
					self.io.EmitInstruction(Instruction.NegateInt);
					return new ValueType(TypeKind.Int);
				}
				else if (expression.type.IsKind(TypeKind.Float))
				{
					self.io.EmitInstruction(Instruction.NegateFloat);
					return new ValueType(TypeKind.Float);
				}
				else
				{
					self.io.AddSoftError(expression.slice, "Unary '-' operator can only be applied to ints or floats. Got type {0}", expression.type.ToString(self.io.chunk));
					return expression.type;
				}
			case TokenKind.Bang:
				if (expression.type.IsKind(TypeKind.Bool))
					self.io.EmitInstruction(Instruction.Not);
				else
					self.io.AddSoftError(expression.slice, "'!' operator can only be applied to bools. Got type {0}", expression.type.ToString(self.io.chunk));
				return new ValueType(TypeKind.Bool);
			case TokenKind.Int:
				if (expression.type.IsKind(TypeKind.Float))
					self.io.EmitInstruction(Instruction.FloatToInt);
				else
					self.io.AddSoftError(expression.slice, "Can only convert floats to int. Got type {0}", expression.type.ToString(self.io.chunk));

				self.io.DebugEmitPopTypes(1);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Int));
				return new ValueType(TypeKind.Int);
			case TokenKind.Float:
				if (expression.type.IsKind(TypeKind.Int))
					self.io.EmitInstruction(Instruction.IntToFloat);
				else
					self.io.AddSoftError(expression.slice, "Can only convert ints to float. Got {0}", expression.type.ToString(self.io.chunk));

				self.io.DebugEmitPopTypes(1);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Float));
				return new ValueType(TypeKind.Float);
			default:
				self.io.AddHardError(
						expression.slice,
						string.Format("Expected unary operator. Got token {0}", opToken.kind)
					);
				return new ValueType(TypeKind.Unit);
			}
		}

		internal static ValueType Binary(Compiler self, ExpressionResult previous)
		{
			var c = self.io;
			var opToken = c.parser.previousToken;

			var opPrecedence = self.parseRules.GetPrecedence(opToken.kind);
			var expression = ParseWithPrecedence(self, opPrecedence + 1);
			var slice = Slice.FromTo(previous.slice, expression.slice);

			var aType = previous.type;
			var bType = expression.type;

			ValueType MathOp(Instruction intOp, Instruction floatOp, string errorMessage)
			{
				if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
				{
					c.EmitInstruction(intOp);
					c.DebugEmitPopTypes(1);
					return new ValueType(TypeKind.Int);
				}
				else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
				{
					c.EmitInstruction(floatOp);
					c.DebugEmitPopTypes(1);
					return new ValueType(TypeKind.Float);
				}
				else
				{
					c.AddSoftError(slice, "{0}. Got types {1} and {2}", errorMessage, aType.ToString(c.chunk), bType.ToString(c.chunk));
					c.DebugEmitPopTypes(1);
					return aType;
				}
			}

			ValueType CompOp(Instruction intOp, Instruction floatOp, bool negate, string errorMessage)
			{
				if (aType.IsKind(TypeKind.Int) && bType.IsKind(TypeKind.Int))
					c.EmitInstruction(intOp);
				else if (aType.IsKind(TypeKind.Float) && bType.IsKind(TypeKind.Float))
					c.EmitInstruction(floatOp);
				else
					c.AddSoftError(slice, "{0}. Got types {1} and {2}", errorMessage, aType.ToString(self.io.chunk));

				if (negate)
					c.EmitInstruction(Instruction.Not);

				self.io.DebugEmitPopTypes(2);
				self.io.DebugEmitPushType(new ValueType(TypeKind.Bool));

				return new ValueType(TypeKind.Bool);
			}

			switch (opToken.kind)
			{
			case TokenKind.Plus:
				return MathOp(Instruction.AddInt, Instruction.AddFloat, "'+' operator can only be applied to ints or floats");
			case TokenKind.Minus:
				return MathOp(Instruction.SubtractInt, Instruction.SubtractFloat, "'-' operator can only be applied to ints or floats");
			case TokenKind.Asterisk:
				return MathOp(Instruction.MultiplyInt, Instruction.MultiplyFloat, "'*' operator can only be applied to ints or floats");
			case TokenKind.Slash:
				return MathOp(Instruction.DivideInt, Instruction.DivideFloat, "'/' operator can only be applied to ints or floats");
			case TokenKind.EqualEqual:
			case TokenKind.BangEqual:
				{
					var operatorName = opToken.kind == TokenKind.EqualEqual ? "==" : "!=";

					if (!aType.IsEqualTo(bType))
					{
						c.AddSoftError(slice, "'{0}' operator can only be applied to same type values. Got types {1} and {2}", operatorName, aType.ToString(self.io.chunk), bType.ToString(self.io.chunk));
						return new ValueType(TypeKind.Bool);
					}
					if (!aType.IsSimple)
					{
						c.AddSoftError(slice, "'{0}' operator can not be applied to unit, tuples or structs types. Got type {1}", operatorName, aType.ToString(self.io.chunk));
						return aType;
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
						c.AddSoftError(slice, "'{0}' operator can not be applied to unit, tuples or structs types. Got type {1}", operatorName, aType.ToString(self.io.chunk));
						break;
					}

					if (opToken.kind == TokenKind.BangEqual)
						self.io.EmitInstruction(Instruction.Not);

					self.io.DebugEmitPopTypes(2);
					self.io.DebugEmitPushType(new ValueType(TypeKind.Bool));
					return new ValueType(TypeKind.Bool);
				}
			case TokenKind.Greater:
				return CompOp(Instruction.GreaterInt, Instruction.GreaterFloat, false, "'>' operator can only be applied to ints or floats");
			case TokenKind.GreaterEqual:
				return CompOp(Instruction.LessInt, Instruction.LessFloat, true, "'>=' operator can only be applied to ints or floats");
			case TokenKind.Less:
				return CompOp(Instruction.LessInt, Instruction.LessFloat, false, "'<' operator can only be applied to ints or floats");
			case TokenKind.LessEqual:
				return CompOp(Instruction.GreaterInt, Instruction.GreaterFloat, true, "'<=' operator can only be applied to ints or floats");
			default:
				return new ValueType(TypeKind.Unit);
			}
		}
	}
}