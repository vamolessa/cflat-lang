using System.IO;

public static class Program
{
	public static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Repl.Start();
			return;
		}

		if (args.Length == 1)
		{
			var source = ReadFile(args[0]);
			Interpreter.RunSource(source);
			return;
		}

		System.Console.Error.WriteLine("Invalid input");
		return;
	}

	public static string ReadFile(string filename)
	{
		try
		{
			return File.ReadAllText(filename);
		}
		catch (FileNotFoundException e)
		{
			System.Console.Error.WriteLine(e.Message);
			System.Environment.Exit(-1);
			return null;
		}
	}
}
