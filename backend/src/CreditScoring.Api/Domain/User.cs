namespace CreditScoring.Api.Domain;

public enum UserRole
{
    Officer = 1,
    Manager = 2,
    Risk = 3,
}

public sealed record Actor(Guid UserId, string Name);

public class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string UserName { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public UserRole Role { get; private set; }
    public string PasswordHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(string userName, string displayName, UserRole role, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), role, "unknown role");

        return new User
        {
            Id = Guid.CreateVersion7(createdAt),
            UserName = userName.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Role = role,
            CreatedAt = createdAt,
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public Actor AsActor() => new(Id, DisplayName);
}
