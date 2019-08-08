using System.Text;

public static class Repl
{
	public static void Start()
	{
		var sb = new StringBuilder();

		while (true)
		{
			sb.Clear();
			var lastLineEmpty = false;

			while (true)
			{
				var line = System.Console.ReadLine();
				if (line == null)
					return;

				var lineEmpty = string.IsNullOrEmpty(line);
				if (lineEmpty && lastLineEmpty)
					break;
				lastLineEmpty = lineEmpty;
				sb.Append(line);
			}

			var source = sb.ToString();
			if (source.Length == 0)
				break;

			Interpreter.RunSource(source);
			if (System.Console.Read() < 0)
				break;

			System.Console.Write("\n\n----\n\n");
		}
	}
}