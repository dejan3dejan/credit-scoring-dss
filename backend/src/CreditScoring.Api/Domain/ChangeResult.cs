namespace CreditScoring.Api.Domain;

public abstract record ChangeResult;

public sealed record Changed : ChangeResult;

public sealed record Refused(string? Field, string Code, string Message) : ChangeResult;
