namespace CreditScoring.Api.Domain;

public enum ApplicationStage
{
    Submitted = 1,
    AwaitingSignOff = 2,
    Decided = 3,
    Withdrawn = 4,
}

public class CreditApplication
{
    public Guid Id { get; private set; }
    public string Reference { get; private set; } = "";
    public ApplicationStage Stage { get; private set; } = ApplicationStage.Submitted;
    public Guid? AssignedToUserId { get; private set; }
    public User? AssignedTo { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public string? WithdrawnBy { get; private set; }
    public Guid? WithdrawnByUserId { get; private set; }
    public string? WithdrawalReason { get; private set; }
    public Guid ClientId { get; init; }
    public Client? Client { get; set; }
    private string applicantName = "";

    public required string ApplicantName { get => applicantName; init => applicantName = value; }
    public Guid BranchId { get; init; }
    public required string SubmittedBy { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }

    public required int LoanAmount { get; init; }
    public required LoanPurpose Purpose { get; init; }
    public required EmploymentLength EmploymentLength { get; init; }
    public required HomeOwnership HomeOwnership { get; init; }
    public required decimal AnnualIncome { get; init; }
    public required IncomeVerification IncomeVerification { get; init; }
    public required decimal DebtToIncomePercent { get; init; }
    public required int CreditScore { get; init; }
    public required DateOnly EarliestCreditLine { get; init; }
    public required int InquiriesLast6Months { get; init; }
    public required int DelinquenciesLast2Years { get; init; }
    public required int PublicRecords { get; init; }
    public required int Bankruptcies { get; init; }
    public required decimal RevolvingBalance { get; init; }
    public required decimal RevolvingUtilizationPercent { get; init; }
    public required int OpenAccounts { get; init; }
    public required int TotalAccounts { get; init; }
    public required int MortgageAccounts { get; init; }

    public CreditDecision? Decision { get; set; }
    public OfficerDecision? OfficerDecision { get; private set; }
    public List<SignOffRequest> SignOffs { get; init; } = [];
    public List<ShadowScore> ShadowScores { get; init; } = [];
    public List<ScoringFailure> ScoringFailures { get; init; } = [];

    public SignOffRequest? PendingSignOff => SignOffs.SingleOrDefault(s => s.Status == SignOffStatus.Pending);

    public bool IsClosed => Stage is ApplicationStage.Decided or ApplicationStage.Withdrawn;

    public void RemoveApplicantName() => applicantName = Client.AnonymisedName;

    public bool MayBeHandledBy(Guid userId, bool actsForBranch) => actsForBranch || AssignedToUserId == userId;

    public ChangeResult AssignTo(User assignee)
    {
        if (IsClosed)
            return new Refused(null, "not_assignable", "A decided or withdrawn application cannot be reassigned.");
        AssignedToUserId = assignee.Id;
        AssignedTo = assignee;
        return new Changed();
    }

    public void RecordDecision(OfficerDecision decision)
    {
        if (IsClosed || OfficerDecision is not null)
            throw new InvalidOperationException("a decided or withdrawn application cannot be decided again");
        OfficerDecision = decision;
        Stage = ApplicationStage.Decided;
    }

    public SignOffRequest RequestSignOff(FinalDecision outcome, string? reason, SignOffReason why, Actor requestedBy, DateTimeOffset now)
    {
        if (Stage != ApplicationStage.Submitted)
            throw new InvalidOperationException("only a submitted application can wait for a sign-off");
        var request = SignOffRequest.Create(outcome, reason, why, requestedBy, now);
        SignOffs.Add(request);
        Stage = ApplicationStage.AwaitingSignOff;
        return request;
    }

    public void ReturnSignOff(Actor by, string note, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        var pending = PendingSignOff ?? throw new InvalidOperationException("nothing waits for a sign-off");
        pending.Close(SignOffStatus.Returned, by, now, note);
        Stage = ApplicationStage.Submitted;
    }

    public void ApproveSignOff(OfficerDecision decision, Actor by, string? note, DateTimeOffset now)
    {
        var pending = PendingSignOff ?? throw new InvalidOperationException("nothing waits for a sign-off");
        if (OfficerDecision is not null)
            throw new InvalidOperationException("a decided application cannot be decided again");
        pending.Close(SignOffStatus.Approved, by, now, note);
        decision.SignOff(by, now);
        OfficerDecision = decision;
        Stage = ApplicationStage.Decided;
    }

    public ChangeResult Withdraw(string reason, Actor by, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (IsClosed)
            return new Refused(null, "not_withdrawable", "A decided or withdrawn application cannot be withdrawn.");

        PendingSignOff?.Close(SignOffStatus.Returned, by, now, "Withdrawn");
        Stage = ApplicationStage.Withdrawn;
        WithdrawnAt = now;
        WithdrawnBy = by.Name;
        WithdrawnByUserId = by.UserId;
        WithdrawalReason = reason.Trim();
        return new Changed();
    }
}
