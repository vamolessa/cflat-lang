using System.Collections.Specialized;
using System.IO;

/*
sysexits:
https://www.freebsd.org/cgi/man.cgi?query=sysexits
*/
public static class Program
{
	public sealed class Test : cflat.debug.IRequestHandler
	{
		public void OnRequest(string uriLocalPath, string body, cflat.debug.JsonWriter writer)
		{
			using (var o = writer.Object)
			{
				o.String("path", uriLocalPath);
				o.String("body", body);

				using (var sub = o.Object("sub"))
				{
					sub.Boolean("bool", true);
					sub.Number("number", 3.5f);
					using (var a = sub.Array("some array"))
					{
						a.String("this");
						a.String("is");
						a.String("another");
						a.String("array:");
						using (var oa = a.Array)
						{
							oa.String("something");
							oa.Boolean(false);
						}
					}
				}
			}
		}
	}

	public static void Main(string[] args)
	{
		ConsoleHelper.Write("BEGIN!\n");
		var server = new cflat.debug.Server(4747, new Test());
		server.Start();
		System.Console.ReadKey();
		server.Stop();
		return;

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
