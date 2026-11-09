namespace CreditScoring.Api.Domain;

public class ApplicationNote
{
    public const int TextMaxLength = 2000;

    private ApplicationNote() { }

    public Guid Id { get; private set; }
    public Guid CreditApplicationId { get; private set; }
    public string Text { get; private set; } = "";
    public string Author { get; private set; } = "";
    public Guid? AuthorUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ApplicationNote Write(Guid applicationId, string text, Actor author, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new ApplicationNote
        {
            Id = Guid.CreateVersion7(now),
            CreditApplicationId = applicationId,
            Text = text.Trim(),
            Author = author.Name,
            AuthorUserId = author.UserId,
            CreatedAt = now,
        };
    }
}
