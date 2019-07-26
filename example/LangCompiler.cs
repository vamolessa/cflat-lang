using System.Collections.Generic;

public sealed class LangCompiler
{

	public Result<ByteCodeChunk, List<CompileError>> Compile(string source, ITokenizer tokenizer)
	{
		var compiler = new Compiler();

		tokenizer.Begin(LangScanners.scanners, source);
		compiler.Begin(tokenizer, LangParseRules.rules);

		compiler.Next();
		Expression(compiler);
		compiler.Consume(Token.EndKind, "Expected end of expression.");

		// end compiler
		compiler.EmitInstruction(Instruction.Return);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.GetByteCodeChunk());
	}

	public static void Expression(Compiler compiler)
	{
		compiler.ParseWithPrecedence((int)Precedence.Assignment);
	}

	public static void Grouping(Compiler compiler)
	{
		Expression(compiler);
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after expression");
	}

	public static void Literal(Compiler compiler)
	{
		switch ((TokenKind)compiler.previousToken.kind)
		{
		case TokenKind.Nil:
			compiler.EmitInstruction(Instruction.LoadNil);
			compiler.PushType((int)Value.Type.Nil);
			break;
		case TokenKind.True:
			compiler.EmitInstruction(Instruction.LoadTrue);
			compiler.PushType((int)Value.Type.Bool);
			break;
		case TokenKind.False:
			compiler.EmitInstruction(Instruction.LoadFalse);
			compiler.PushType((int)Value.Type.Bool);
			break;
		case TokenKind.IntegerNumber:
			compiler.EmitLoadConstant(new Value(CompilerHelper.ParseInt(compiler)));
			compiler.PushType((int)Value.Type.Int);
			break;
		case TokenKind.RealNumber:
			compiler.EmitLoadConstant(new Value(CompilerHelper.ParseFloat(compiler)));
			compiler.PushType((int)Value.Type.Float);
			break;
		case TokenKind.String:
			compiler.EmitLoadStringLiteral(CompilerHelper.ParseString(compiler));
			compiler.PushType((int)Value.Type.Object);
			break;
		default:
			compiler.AddHardError(
				compiler.previousToken.index,
				string.Format("Invalid token kind {0}", compiler.previousToken.kind)
			);
			break;
		}
	}

	public static void Unary(Compiler compiler)
	{
		var opKind = compiler.previousToken.kind;
		var opToken = compiler.previousToken;

		compiler.ParseWithPrecedence((int)Precedence.Unary);

		switch ((TokenKind)opKind)
		{
		case TokenKind.Minus:
			{
				compiler.EmitInstruction(Instruction.Negate);
				var type = (Value.Type)compiler.PopType();
				if (type != Value.Type.Int && type != Value.Type.Float)
					compiler.AddSoftError(opToken.index, "Unary minus operator can only be applied to numbers");
				compiler.PushType((int)Value.Type.Int);
				break;
			}
		case TokenKind.Bang:
			{
				compiler.EmitInstruction(Instruction.Not);
				var type = (Value.Type)compiler.PopType();
				if (type != Value.Type.Bool)
					compiler.AddSoftError(opToken.index, "Not operator can only be applied to booleans");
				compiler.PushType((int)Value.Type.Bool);
			}
			break;
		default:
			break;
		}
	}

	public static void Binary(Compiler compiler)
	{
		var opKind = compiler.previousToken.kind;
		var opToken = compiler.previousToken;

		var opPrecedence = compiler.GetTokenPrecedence(opKind);
		compiler.ParseWithPrecedence(opPrecedence + 1);

		switch ((TokenKind)opKind)
		{
		case TokenKind.Plus:
			{
				compiler.EmitInstruction(Instruction.Add);
				var aType = (Value.Type)compiler.PopType();
				var bType = (Value.Type)compiler.PopType();
				if (aType != Value.Type.Int || bType != Value.Type.Int)
					compiler.AddSoftError(opToken.index, "Plus operator can only be applied to numbers");
				compiler.PushType((int)Value.Type.Int);
				break;
			}
		case TokenKind.Minus:
			compiler.EmitInstruction(Instruction.Subtract);
			break;
		case TokenKind.Asterisk:
			compiler.EmitInstruction(Instruction.Multiply);
			break;
		case TokenKind.Slash:
			compiler.EmitInstruction(Instruction.Divide);
			break;
		case TokenKind.EqualEqual:
			compiler.EmitInstruction(Instruction.Equal);
			break;
		case TokenKind.BangEqual:
			compiler.EmitInstruction(Instruction.Equal);
			compiler.EmitInstruction(Instruction.Not);
			break;
		case TokenKind.Greater:
			compiler.EmitInstruction(Instruction.Greater);
			break;
		case TokenKind.GreaterEqual:
			compiler.EmitInstruction(Instruction.Less);
			compiler.EmitInstruction(Instruction.Not);
			break;
		case TokenKind.Less:
			compiler.EmitInstruction(Instruction.Less);
			break;
		case TokenKind.LessEqual:
			compiler.EmitInstruction(Instruction.Greater);
			compiler.EmitInstruction(Instruction.Not);
			break;
		default:
			return;
		}
	}
}