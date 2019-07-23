public static class InterpreterHelper
{
	public static bool ToBool(object value)
	{
		return !(value is null || value is false);
	}

	public static Option<int> ToInt(object value)
	{
		if (value is int integer)
			return Option.Some(integer);
		return Option.None;
	}
}