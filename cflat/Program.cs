using System.IO;

/*
sysexits:
https://www.freebsd.org/cgi/man.cgi?query=sysexits
*/
public static class Program
{
	public static void Main(string[] args)
	{
		var server = new cflat.debug.DebugServer(cflat.debug.DebugServer.DefaultPort);
		server.StartPaused();
		ConsoleHelper.Write("SERVER STARTED\n");
		while (true)
			System.Threading.Thread.Sleep(1000);

		if (args.Length == 0)
		{
			Repl.Start();
			return;
		}

		if (args.Length == 1)
		{
			var path = args[0];
			var source = ReadFile(path);
			Interpreter.RunSource(path, source, true);
			return;
		}

		ConsoleHelper.Write("Usage: cflat [script]");
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
