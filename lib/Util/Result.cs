using System.Runtime.InteropServices;

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

[StructLayout(LayoutKind.Explicit)]
public readonly struct Result<T, E>
{
	[FieldOffset(0)]
	public readonly bool isOk;
	[FieldOffset(4)]
	public readonly T ok;
	[FieldOffset(4)]
	public readonly E error;

	public Result(bool isOk, T ok, E error)
	{
		this.isOk = isOk;
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