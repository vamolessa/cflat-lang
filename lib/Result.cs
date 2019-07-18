public static class Result
{
	public static OkResult<T> Ok<T>(T ok)
	{
		return new OkResult<T>(ok);
	}

	public static ErrorResult<E> Error<E>(E error)
	{
		return new ErrorResult<E>(error);
	}
}

public readonly struct OkResult<T>
{
	public readonly T ok;

	public OkResult(T ok)
	{
		this.ok = ok;
	}
}

public readonly struct ErrorResult<E>
{
	public readonly E error;

	public ErrorResult(E error)
	{
		this.error = error;
	}
}

public readonly struct Result<T, E>
{
	public readonly bool IsOk;
	public readonly T ok;
	public readonly E error;

	public Result(bool isOk, T ok, E error)
	{
		this.IsOk = isOk;
		this.ok = ok;
		this.error = error;
	}

	public static implicit operator Result<T, E>(OkResult<T> result)
	{
		return new Result<T, E>(true, result.ok, default(E));
	}

	public static implicit operator Result<T, E>(ErrorResult<E> result)
	{
		return new Result<T, E>(false, default(T), result.error);
	}
}