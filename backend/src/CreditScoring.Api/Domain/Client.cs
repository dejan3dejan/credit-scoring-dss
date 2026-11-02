namespace CreditScoring.Api.Domain;

public class Client
{
    private Client() { }

    public Guid Id { get; private set; }
    public string ClientNumber { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public DateOnly? DateOfBirth { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    public static Client Create(string fullName, DateOnly? dateOfBirth, Actor createdBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        if (dateOfBirth > DateOnly.FromDateTime(now.UtcDateTime))
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), dateOfBirth, "date of birth is in the future");

        return new Client
        {
            Id = Guid.CreateVersion7(now),
            FullName = fullName.Trim(),
            DateOfBirth = dateOfBirth,
            CreatedAt = now,
            CreatedByUserId = createdBy.UserId,
        };
    }
}
