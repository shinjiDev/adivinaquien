namespace AdivinaQue.Engine;

public readonly struct Result
{
    private Result(bool isSuccess, ErrorCode? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public ErrorCode? Error { get; }

    public static Result Ok() => new(true, null);

    public static Result Fail(ErrorCode error) => new(false, error);
}
