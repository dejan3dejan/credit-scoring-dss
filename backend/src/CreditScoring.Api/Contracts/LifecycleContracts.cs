using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record UserRef(Guid Id, string DisplayName)
{
    public static UserRef? From(Guid? id, User? user) => id switch
    {
        null => null,
        { } known when user?.Id == known => new UserRef(user.Id, user.DisplayName),
        _ => throw new InvalidOperationException("the assigned user must be loaded to build a response"),
    };
}

public sealed record WithdrawalResponse(DateTimeOffset At, string By, string Reason);

public sealed record SignOffResponse(
    Guid Id,
    FinalDecision Outcome,
    string? Reason,
    SignOffReason Why,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    SignOffStatus Status,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? Note)
{
    public static SignOffResponse From(SignOffRequest request) => new(
        request.Id,
        request.Outcome,
        request.Reason,
        request.Why,
        request.RequestedBy,
        request.RequestedAt,
        request.Status,
        request.ReviewedBy,
        request.ReviewedAt,
        request.Note);
}

public sealed record NoteResponse(Guid Id, string Text, string Author, DateTimeOffset CreatedAt)
{
    public static NoteResponse From(ApplicationNote note) => new(note.Id, note.Text, note.Author, note.CreatedAt);
}

public sealed record SignOffReviewRequest
{
    [Required, Display(Name = "Approve")]
    public bool? Approve { get; init; }

    [StringLength(BusinessLimits.DecisionReasonMaxLength), Display(Name = "Note")]
    public string? Note { get; init; }
}

public sealed record AssignRequest
{
    [Required, Display(Name = "Officer")]
    public Guid? UserId { get; init; }
}

public sealed record WithdrawRequest
{
    [Required, StringLength(BusinessLimits.DecisionReasonMaxLength), Display(Name = "Reason")]
    public string? Reason { get; init; }
}

public sealed record NoteRequest
{
    [Required, StringLength(ApplicationNote.TextMaxLength), Display(Name = "Note")]
    public string? Text { get; init; }
}
