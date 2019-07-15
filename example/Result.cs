public static class Result
{
	public static Result<T> Ok<T>(T ok)
	{
		return new Result<T>(ok);
	}

	public static ErrorResult Error(int errorIndex, string errorMessage)
	{
		return new ErrorResult(errorIndex, errorMessage);
	}
}

public readonly struct ErrorResult
{
	public readonly int index;
	public readonly string message;

	public ErrorResult(int errorIndex, string errorMessage)
	{
		this.index = errorIndex;
		this.message = errorMessage;
	}
}

public readonly struct Result<T>
{
	public readonly T ok;
	public readonly int errorIndex;
	public readonly string errorMessage;

	public bool IsOk
	{
		get { return string.IsNullOrEmpty(errorMessage) && errorIndex < 0; }
	}

	public Result(T ok)
	{
		this.ok = ok;
		this.errorIndex = -1;
		this.errorMessage = null;
	}

	public Result(int errorIndex, string errorMessage)
	{
		this.ok = default(T);
		this.errorIndex = errorIndex;
		this.errorMessage = errorMessage;
	}

	public static implicit operator Result<T>(ErrorResult error)
	{
		return new Result<T>(error.index, error.message);
	}
}