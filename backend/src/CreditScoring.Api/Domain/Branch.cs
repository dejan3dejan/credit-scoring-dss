namespace CreditScoring.Api.Domain;

public class Branch
{
    public const int CodeMaxLength = 16;
    public const int NameMaxLength = 100;

    private Branch() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? City { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static Branch Create(string code, string name, string? city, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var branch = new Branch
        {
            Id = Guid.CreateVersion7(now),
            Code = NormalizeCode(code),
            IsActive = true,
            CreatedAt = now,
        };
        branch.Edit(name, city);
        return branch;
    }

    public void Edit(string name, string? city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        City = Text.TrimToNull(city);
    }

    public ChangeResult Deactivate(bool hasActiveUsers)
    {
        if (hasActiveUsers)
            return new Refused(null, "branch_has_active_users", "Move or deactivate the branch's active users first.");
        IsActive = false;
        return new Changed();
    }

    public void Reactivate() => IsActive = true;
}
