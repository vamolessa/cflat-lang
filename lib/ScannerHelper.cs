public static class ScannerHelper
{
	public static bool StartsWith(string str, int index, string match)
	{
		var count = str.Length;
		if (str.Length - index < match.Length)
			return false;

		for (var i = 0; i < match.Length; i++)
		{
			if (str[index + i] != match[i])
				return false;
		}

		return true;
	}
}