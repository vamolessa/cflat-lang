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
		void Convert(Compiler c, string s, Token t)
		{
			if (t.kind == (int)TokenKind.Nil)
				c.EmitInstruction(Instruction.LoadNil);
			else if (t.kind == (int)TokenKind.True)
				c.EmitInstruction(Instruction.LoadTrue);
			else
				c.EmitInstruction(Instruction.LoadFalse);
		}

		compiler.Convert(Convert);
	}

	public static void Number(Compiler compiler)
	{
		void Convert(Compiler c, string s, Token t)
		{
			if (t.kind == (int)TokenKind.IntegerNumber)
				c.EmitLoadConstant(new Value(CompilerHelper.ToInteger(s, t)));
			else
				c.EmitLoadConstant(new Value(CompilerHelper.ToFloat(s, t)));
		}

		compiler.Convert(Convert);
	}

	public static void Unary(Compiler compiler)
	{
		var opKind = (TokenKind)compiler.previousToken.kind;

		compiler.ParseWithPrecedence((int)Precedence.Unary);

		switch (opKind)
		{
		case TokenKind.Minus:
			compiler.EmitInstruction(Instruction.Negate);
			break;
		case TokenKind.Bang:
			compiler.EmitInstruction(Instruction.Not);
			break;
		default:
			break;
		}
	}

	public static void Binary(Compiler compiler)
	{
		var opKind = compiler.previousToken.kind;

		var opPrecedence = compiler.GetTokenPrecedence(opKind);
		compiler.ParseWithPrecedence(opPrecedence + 1);

		switch ((TokenKind)opKind)
		{
		case TokenKind.Plus:
			compiler.EmitInstruction(Instruction.Add);
			break;
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