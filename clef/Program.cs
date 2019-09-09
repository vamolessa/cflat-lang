using System.IO;

/*
sysexits:
https://www.freebsd.org/cgi/man.cgi?query=sysexits
*/
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
			Interpreter.RunSource(source, true);
			return;
		}

		ConsoleHelper.Write("Usage: clef [script]");
		System.Environment.Exit(64);
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
			System.Environment.Exit(74);
			return null;
		}
	}
}
