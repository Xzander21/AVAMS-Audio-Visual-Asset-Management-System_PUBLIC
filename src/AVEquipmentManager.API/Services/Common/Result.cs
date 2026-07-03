namespace AVEquipmentManager.API.Services.Common;

/// <summary>
/// Non-throwing result wrapper used by the transaction-proof lifecycle
/// services. Lets controllers branch on Success without try/catch.
///
/// Exceptions are reserved for infrastructure failures inside the service
/// (DB outage, concurrency conflict) — the service catches those, rolls
/// back the transaction, and returns Result.Fail with a clean message.
/// </summary>
public readonly record struct Result<T>(bool Success, T? Value, string? Error)
{
    public static Result<T> Ok(T value)        => new(true,  value,   null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
