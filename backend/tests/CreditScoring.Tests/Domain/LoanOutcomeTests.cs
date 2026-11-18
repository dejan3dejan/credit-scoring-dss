using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class LoanOutcomeTests
{
    private static readonly Actor Risk = new(Guid.Parse("0192f000-0000-7000-8000-0000000000f1"), "Rita Risk");
    private static readonly Actor Checker = new(Guid.Parse("0192f000-0000-7000-8000-0000000000f2"), "Rade Risk");

    private static CreditApplication DecidedAs(FinalDecision outcome)
    {
        var application = TestData.ContractApplication();
        application.RecordDecision(OfficerDecision.Record(null, outcome, "checked by hand", TestData.Officer, TestData.Now));
        return application;
    }

    [Fact]
    public void An_issued_loan_gets_its_outcome_once()
    {
        var application = DecidedAs(FinalDecision.Approve);

        Assert.IsType<Changed>(application.RecordOutcome(LoanOutcomeKind.Defaulted, " missed three payments ", Risk, TestData.Now));

        var outcome = application.LoanOutcome!;
        Assert.Equal((LoanOutcomeKind.Defaulted, "missed three payments", "Rita Risk", Risk.UserId, TestData.Now),
            (outcome.Outcome, outcome.Note, outcome.RecordedBy, outcome.RecordedByUserId!.Value, outcome.RecordedAt));
        var again = Assert.IsType<Refused>(application.RecordOutcome(LoanOutcomeKind.Repaid, null, Risk, TestData.Now));
        Assert.Equal((null, "outcome_recorded"), (again.Field, again.Code));
        Assert.Equal(LoanOutcomeKind.Defaulted, application.LoanOutcome!.Outcome);
    }

    [Fact]
    public void Only_an_approved_decision_issued_a_loan()
    {
        var rejected = DecidedAs(FinalDecision.Reject);
        var open = TestData.ContractApplication();

        foreach (var application in new[] { rejected, open })
        {
            var refused = Assert.IsType<Refused>(application.RecordOutcome(LoanOutcomeKind.Repaid, null, Risk, TestData.Now));
            Assert.Equal("no_loan", refused.Code);
            Assert.Null(application.LoanOutcome);
        }
    }

    [Fact]
    public void An_unknown_outcome_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecidedAs(FinalDecision.Approve).RecordOutcome((LoanOutcomeKind)9, null, Risk, TestData.Now));
    }

    private static CreditApplication WithOutcome(LoanOutcomeKind outcome)
    {
        var application = DecidedAs(FinalDecision.Approve);
        Assert.IsType<Changed>(application.RecordOutcome(outcome, "from the servicing report", Risk, TestData.Now));
        return application;
    }

    [Fact]
    public void A_recorded_outcome_is_corrected_with_a_reason_and_keeps_who_first_recorded_it()
    {
        var application = WithOutcome(LoanOutcomeKind.Repaid);
        var later = TestData.Now.AddDays(3);

        Assert.IsType<Changed>(application.CorrectOutcome(LoanOutcomeKind.Defaulted, " charged off after the report ", Checker, later));

        var outcome = application.LoanOutcome!;
        Assert.Equal((LoanOutcomeKind.Defaulted, "charged off after the report", "Rade Risk", Checker.UserId, later),
            (outcome.Outcome, outcome.CorrectionReason, outcome.CorrectedBy, outcome.CorrectedByUserId!.Value, outcome.CorrectedAt!.Value));
        Assert.Equal(("from the servicing report", "Rita Risk", TestData.Now), (outcome.Note, outcome.RecordedBy, outcome.RecordedAt));
    }

    [Fact]
    public void Only_a_recorded_outcome_can_be_corrected()
    {
        var refused = Assert.IsType<Refused>(DecidedAs(FinalDecision.Approve).CorrectOutcome(LoanOutcomeKind.Repaid, "typo", Checker, TestData.Now));
        Assert.Equal((null, "no_outcome"), (refused.Field, refused.Code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void A_correction_needs_a_reason(string? reason)
    {
        var application = WithOutcome(LoanOutcomeKind.Repaid);

        var refused = Assert.IsType<Refused>(application.CorrectOutcome(LoanOutcomeKind.Defaulted, reason, Checker, TestData.Now));

        Assert.Equal(("reason", "reason_required"), (refused.Field, refused.Code));
        Assert.Equal(LoanOutcomeKind.Repaid, application.LoanOutcome!.Outcome);
    }

    [Fact]
    public void A_correction_has_to_change_the_outcome()
    {
        var refused = Assert.IsType<Refused>(WithOutcome(LoanOutcomeKind.Defaulted).CorrectOutcome(LoanOutcomeKind.Defaulted, "again", Checker, TestData.Now));
        Assert.Equal(("outcome", "same_outcome"), (refused.Field, refused.Code));
    }
}
