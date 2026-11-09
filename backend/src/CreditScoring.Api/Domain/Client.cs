namespace CreditScoring.Api.Domain;

public sealed record ClientContact(string? Address, string? Phone, string? Email, string? Employer)
{
    public static readonly ClientContact None = new(null, null, null, null);
}

public class Client
{
    public const string AnonymisedName = "Anonymised";

    private Client() { }

    public Guid Id { get; private set; }
    public string ClientNumber { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public DateOnly? DateOfBirth { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Employer { get; private set; }
    public Guid BranchId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? AnonymisedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    public static Client Create(
        string fullName,
        DateOnly? dateOfBirth,
        Guid branchId,
        Actor createdBy,
        DateTimeOffset now,
        ClientContact? contact = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("A client belongs to a branch.", nameof(branchId));

        var client = new Client
        {
            Id = Guid.CreateVersion7(now),
            BranchId = branchId,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = createdBy.UserId,
        };
        client.SetDetails(fullName, dateOfBirth, contact ?? ClientContact.None, now);
        return client;
    }

    public ChangeResult Edit(string fullName, DateOnly? dateOfBirth, ClientContact contact, DateTimeOffset now)
    {
        if (AnonymisedAt is not null)
            return Anonymised();
        SetDetails(fullName, dateOfBirth, contact, now);
        return new Changed();
    }

    public ChangeResult Deactivate()
    {
        IsActive = false;
        return new Changed();
    }

    public ChangeResult Reactivate()
    {
        if (AnonymisedAt is not null)
            return Anonymised();
        IsActive = true;
        return new Changed();
    }

    public ChangeResult Anonymise(DateTimeOffset now)
    {
        if (AnonymisedAt is not null)
            return Anonymised();

        FullName = AnonymisedName;
        DateOfBirth = null;
        Address = Phone = Email = Employer = null;
        IsActive = false;
        AnonymisedAt = now;
        return new Changed();
    }

    private void SetDetails(string fullName, DateOnly? dateOfBirth, ClientContact contact, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        if (dateOfBirth > DateOnly.FromDateTime(now.UtcDateTime))
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), dateOfBirth, "date of birth is in the future");

        FullName = fullName.Trim();
        DateOfBirth = dateOfBirth;
        Address = Text.TrimToNull(contact.Address);
        Phone = Text.TrimToNull(contact.Phone);
        Email = Text.TrimToNull(contact.Email);
        Employer = Text.TrimToNull(contact.Employer);
    }

    private static Refused Anonymised() => new(null, "client_anonymised", "This client has been anonymised.");
}

public static class ClientAnonymisation
{
    public static ChangeResult Apply(Client client, IReadOnlyCollection<CreditApplication> applications, DateTimeOffset now)
    {
        if (applications.Any(a => a.ClientId != client.Id))
            throw new ArgumentException("Only the client's own applications can be anonymised with it.", nameof(applications));

        var result = client.Anonymise(now);
        if (result is not Changed)
            return result;
        foreach (var application in applications)
            application.RemoveApplicantName();
        return result;
    }
}
