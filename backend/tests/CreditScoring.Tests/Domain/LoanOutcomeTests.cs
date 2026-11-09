using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class LoanOutcomeTests
{
    private static readonly Actor Risk = new(Guid.Parse("0192f000-0000-7000-8000-0000000000f1"), "Rita Risk");

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
}
