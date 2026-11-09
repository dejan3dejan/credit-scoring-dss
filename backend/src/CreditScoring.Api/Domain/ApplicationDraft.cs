namespace CreditScoring.Api.Domain;

public enum DraftStatus
{
    Open = 1,
    Submitted = 2,
    Discarded = 3,
}

public class ApplicationDraft
{
    private ApplicationDraft() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Client? Client { get; private set; }
    public Guid BranchId { get; private set; }
    public string Answers { get; private set; } = "{}";
    public DraftStatus Status { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public string CreatedBy { get; private set; } = "";
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ApplicationDraft Start(Client client, string answers, Actor by, DateTimeOffset now)
    {
        var draft = new ApplicationDraft
        {
            Id = Guid.CreateVersion7(now),
            Status = DraftStatus.Open,
            CreatedBy = by.Name,
            CreatedByUserId = by.UserId,
            CreatedAt = now,
        };
        draft.SetAnswers(client, answers, now);
        return draft;
    }

    public ChangeResult Replace(Client client, string answers, DateTimeOffset now)
    {
        if (Status != DraftStatus.Open)
            return Closed();
        SetAnswers(client, answers, now);
        return new Changed();
    }

    public ChangeResult Discard(DateTimeOffset now)
    {
        if (Status != DraftStatus.Open)
            return Closed();
        Status = DraftStatus.Discarded;
        UpdatedAt = now;
        return new Changed();
    }

    public ChangeResult MarkSubmitted(Guid applicationId, DateTimeOffset now)
    {
        if (Status != DraftStatus.Open)
            return Closed();
        Status = DraftStatus.Submitted;
        ApplicationId = applicationId;
        UpdatedAt = now;
        return new Changed();
    }

    private void SetAnswers(Client client, string answers, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(answers);
        ClientId = client.Id;
        Client = client;
        BranchId = client.BranchId;
        Answers = answers;
        UpdatedAt = now;
    }

    private static Refused Closed() => new(null, "draft_closed", "This draft was already submitted or discarded.");
}
