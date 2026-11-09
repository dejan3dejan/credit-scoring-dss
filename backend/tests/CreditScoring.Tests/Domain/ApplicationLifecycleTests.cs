using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Domain;

public class ApplicationLifecycleTests
{
    private static readonly Actor Manager = new(Guid.Parse("0192f000-0000-7000-8000-0000000000e1"), "Marko Manager");

    private static CreditApplication Submitted() => TestData.ContractApplication();

    private static CreditApplication Decided()
    {
        var application = Submitted();
        application.RecordDecision(OfficerDecision.Record(null, FinalDecision.Reject, "no income proof", TestData.Officer, TestData.Now));
        return application;
    }

    [Fact]
    public void A_new_application_is_submitted_and_assigned_to_its_submitter()
    {
        var application = Submitted();

        Assert.Equal(ApplicationStage.Submitted, application.Stage);
        Assert.Equal(TestData.OfficerUser.Id, application.AssignedToUserId);
        Assert.Same(TestData.OfficerUser, application.AssignedTo);
    }

    [Theory]
    [InlineData(Recommendation.Reject, FinalDecision.Approve, 12_000, SignOffReason.OverridesReject)]
    [InlineData(Recommendation.Approve, FinalDecision.Approve, SignOffRules.LargeLoanAmount + 1, SignOffReason.LargeAmount)]
    [InlineData(Recommendation.Reject, FinalDecision.Approve, SignOffRules.LargeLoanAmount + 1, SignOffReason.OverridesReject)]
    public void Approving_against_a_reject_or_a_large_amount_needs_a_sign_off(
        Recommendation recommendation, FinalDecision outcome, int amount, SignOffReason why)
    {
        Assert.Equal(why, SignOffRules.ReasonFor(recommendation, outcome, amount));
    }

    [Theory]
    [InlineData(Recommendation.Approve, FinalDecision.Approve, SignOffRules.LargeLoanAmount)]
    [InlineData(Recommendation.Review, FinalDecision.Approve, 12_000)]
    [InlineData(Recommendation.Approve, FinalDecision.Reject, 35_000)]
    [InlineData(Recommendation.Reject, FinalDecision.Reject, 35_000)]
    [InlineData(null, FinalDecision.Approve, 12_000)]
    public void Other_decisions_need_no_sign_off(Recommendation? recommendation, FinalDecision outcome, int amount)
    {
        Assert.Null(SignOffRules.ReasonFor(recommendation, outcome, amount));
    }

    [Fact]
    public void Recording_the_final_decision_closes_the_application()
    {
        var application = Decided();

        Assert.Equal(ApplicationStage.Decided, application.Stage);
        Assert.NotNull(application.OfficerDecision);
    }

    [Fact]
    public void A_sign_off_request_waits_for_a_manager()
    {
        var application = Submitted();

        var request = application.RequestSignOff(FinalDecision.Approve, " guarantor ", SignOffReason.OverridesReject, TestData.Officer, TestData.Now);

        Assert.Equal(ApplicationStage.AwaitingSignOff, application.Stage);
        Assert.Same(request, application.PendingSignOff);
        Assert.Equal((FinalDecision.Approve, "guarantor", SignOffStatus.Pending, "Tester", TestData.Officer.UserId),
            (request.Outcome, request.Reason, request.Status, request.RequestedBy, request.RequestedByUserId!.Value));
        Assert.Null(application.OfficerDecision);
    }

    [Fact]
    public void Withdrawing_records_who_why_and_when()
    {
        var application = Submitted();

        Assert.IsType<Changed>(application.Withdraw(" client found a cheaper loan ", TestData.Officer, TestData.Now));

        Assert.Equal(ApplicationStage.Withdrawn, application.Stage);
        Assert.Equal(("client found a cheaper loan", "Tester", TestData.Now), (application.WithdrawalReason, application.WithdrawnBy, application.WithdrawnAt!.Value));
    }

    [Fact]
    public void Withdrawing_needs_a_reason()
    {
        Assert.ThrowsAny<ArgumentException>(() => Submitted().Withdraw(" ", TestData.Officer, TestData.Now));
    }

    [Fact]
    public void Withdrawing_closes_a_pending_sign_off_as_returned()
    {
        var application = Submitted();
        var request = application.RequestSignOff(FinalDecision.Approve, "x", SignOffReason.LargeAmount, TestData.Officer, TestData.Now);

        application.Withdraw("client cancelled", TestData.Officer, TestData.Now);

        Assert.Equal(SignOffStatus.Returned, request.Status);
        Assert.Null(application.PendingSignOff);
    }

    [Fact]
    public void A_decided_or_withdrawn_application_cannot_be_withdrawn_or_reassigned()
    {
        var withdrawn = Submitted();
        withdrawn.Withdraw("gone", TestData.Officer, TestData.Now);

        foreach (var application in new[] { Decided(), withdrawn })
        {
            Assert.Equal("not_withdrawable", Assert.IsType<Refused>(application.Withdraw("again", TestData.Officer, TestData.Now)).Code);
            Assert.Equal("not_assignable", Assert.IsType<Refused>(application.AssignTo(TestData.OfficerUser)).Code);
        }
    }

    [Fact]
    public void Reassigning_changes_the_owner()
    {
        var application = Submitted();
        var other = User.Create("other", "Other Officer", UserRole.Officer, TestData.Now, TestData.BranchId);

        Assert.IsType<Changed>(application.AssignTo(other));

        Assert.Equal(other.Id, application.AssignedToUserId);
    }

    [Fact]
    public void Only_the_assignee_or_someone_who_acts_for_the_branch_may_act()
    {
        var application = Submitted();

        Assert.True(application.MayBeHandledBy(TestData.OfficerUser.Id, actsForBranch: false));
        Assert.False(application.MayBeHandledBy(Guid.NewGuid(), actsForBranch: false));
        Assert.True(application.MayBeHandledBy(Manager.UserId, actsForBranch: true));
    }

    [Fact]
    public void A_note_is_trimmed_and_attributed()
    {
        var note = ApplicationNote.Write(Guid.NewGuid(), "  called the employer  ", TestData.Officer, TestData.Now);

        Assert.Equal(("called the employer", "Tester", TestData.Officer.UserId, TestData.Now), (note.Text, note.Author, note.AuthorUserId!.Value, note.CreatedAt));
        Assert.ThrowsAny<ArgumentException>(() => ApplicationNote.Write(Guid.NewGuid(), " ", TestData.Officer, TestData.Now));
    }

    [Fact]
    public void A_withdrawn_application_is_not_rescored()
    {
        var application = Submitted();
        application.Decision = CreditDecision.Unavailable("unreachable", TestData.Now);
        application.Withdraw("client cancelled", TestData.Officer, TestData.Now);

        Assert.Equal("This application was withdrawn.", Rescoring.Refusal(application));
    }
}
