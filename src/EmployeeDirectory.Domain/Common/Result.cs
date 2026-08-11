using System.Diagnostics.CodeAnalysis;

namespace EmployeeDirectory.Domain.Common;

/// <summary>
/// 성공/실패를 반환값으로 표현하는 Result 타입.
/// </summary>
/// <remarks>
/// 예외를 제어 흐름으로 쓰지 않는 이유:
/// (1) 한 번의 업로드에서 <b>여러 건</b>의 검증 실패를 모아서 돌려줘야 한다 (예외는 첫 실패에서 멈춘다),
/// (2) 실패가 시그니처에 드러나 호출자가 처리를 강제받는다,
/// (3) 정상 흐름에서 스택 언와인딩 비용이 없다.
/// </remarks>
public class Result
{
    private static readonly IReadOnlyList<Error> NoErrors = Array.Empty<Error>();

    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        if (isSuccess && errors.Count > 0)
        {
            throw new InvalidOperationException("성공 결과는 오류를 가질 수 없습니다.");
        }

        if (!isSuccess && errors.Count == 0)
        {
            throw new InvalidOperationException("실패 결과는 최소 한 개의 오류를 가져야 합니다.");
        }

        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<Error> Errors { get; }

    /// <summary>첫 번째 오류. 실패 결과에서만 유효하다.</summary>
    public Error FirstError => Errors.Count > 0
        ? Errors[0]
        : throw new InvalidOperationException("성공 결과에는 오류가 없습니다.");

    public static Result Success() => new(true, NoErrors);

    public static Result Failure(params Error[] errors) => new(false, errors);

    public static Result Failure(IReadOnlyList<Error> errors) => new(false, errors);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, NoErrors);

    public static Result<TValue> Failure<TValue>(params Error[] errors) => new(default, false, errors);

    public static Result<TValue> Failure<TValue>(IReadOnlyList<Error> errors) => new(default, false, errors);
}

/// <inheritdoc cref="Result"/>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, IReadOnlyList<Error> errors)
        : base(isSuccess, errors)
        => _value = value;

    /// <summary>성공 결과의 값. 실패 결과에서 접근하면 예외가 발생한다.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("실패한 Result 의 값에 접근할 수 없습니다.");

    public bool TryGetValue([NotNullWhen(true)] out TValue? value)
    {
        value = _value;
        return IsSuccess;
    }

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
