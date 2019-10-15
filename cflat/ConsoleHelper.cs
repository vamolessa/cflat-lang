using Console = System.Console;

public static class ConsoleHelper
{
	public static void LineBreak()
	{
		Console.WriteLine();
	}

	public static void Write(string text)
	{
		Console.Write(text);
	}

	public static void Warning(string text)
	{
		var color = Console.ForegroundColor;
		Console.ForegroundColor = System.ConsoleColor.Yellow;
		Console.Write(text);
		Console.ForegroundColor = color;
	}

	public static void Error(string text)
	{
		var color = Console.ForegroundColor;
		Console.ForegroundColor = System.ConsoleColor.Red;
		Console.Write(text);
		Console.ForegroundColor = color;
	}
}