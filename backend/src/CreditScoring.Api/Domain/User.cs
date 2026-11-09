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
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User() { }

    public Guid Id { get; private set; }
    public string UserName { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public UserRole Role { get; private set; }
    public string PasswordHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public string PreferredLanguage { get; private set; } = Languages.English;
    public int FailedSignInCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastSignedInAt { get; private set; }

    public static string NormalizeUserName(string userName) => userName.Trim().ToLowerInvariant();

    public static User Create(string userName, string displayName, UserRole role, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), role, "unknown role");

        return new User
        {
            Id = Guid.CreateVersion7(createdAt),
            UserName = NormalizeUserName(userName),
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

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil > now;

    public void RecordFailedSignIn(DateTimeOffset now)
    {
        if (IsLockedOut(now))
            throw new InvalidOperationException("A locked account must be refused before its password is checked.");

        FailedSignInCount++;
        if (FailedSignInCount < MaxFailedSignIns)
            return;

        LockedUntil = now + LockoutDuration;
        FailedSignInCount = 0;
    }

    public void RecordSignIn(DateTimeOffset now)
    {
        if (IsLockedOut(now))
            throw new InvalidOperationException("A locked account must be refused before its password is checked.");

        FailedSignInCount = 0;
        LockedUntil = null;
        LastSignedInAt = now;
    }

    public Actor AsActor() => new(Id, DisplayName);
}
