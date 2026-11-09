using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Tests.Decisions;

public class OfficerDecisionRuleTests
{
    private static CreditDecisionService Service() =>
        TestServices.DecisionService(new FakeMlClient(), new ManualTimeProvider(TestData.Now));

    private static readonly Actor Manager = new(Guid.Parse("0192f000-0000-7000-8000-0000000000e1"), "Marko Manager");

    private static CreditApplication Scored(Recommendation recommendation, int? loanAmount = null)
    {
        var application = loanAmount is { } amount ? TestData.Application(TestData.Client, amount) : TestData.ContractApplication();
        var pBad = recommendation switch
        {
            Recommendation.Approve => 0.1,
            Recommendation.Review => 0.25,
            _ => 0.4,
        };
        application.Decision = CreditDecision.Scored(
            pBad, TestData.ModelId, TestData.Threshold, recommendation, TestData.Now, TestData.ReviewFrom);
        return application;
    }

    private static CreditApplication Unavailable()
    {
        var application = TestData.ContractApplication();
        application.Decision = CreditDecision.Unavailable("unreachable", TestData.Now);
        return application;
    }

    [Fact]
    public void Agreeing_without_a_reason_is_recorded_at_the_current_time()
    {
        var result = Service().RecordOfficerDecision(Scored(Recommendation.Approve), FinalDecision.Approve, null, TestData.Officer, actsForBranch: false);

        var recorded = Assert.IsType<DecisionRecorded>(result);
        Assert.False(recorded.Decision.IsOverride);
        Assert.Equal(TestData.Now, recorded.Decision.DecidedAt);
    }

    [Fact]
    public void Override_without_a_reason_is_refused_on_the_reason_field()
    {
        var result = Service().RecordOfficerDecision(Scored(Recommendation.Approve), FinalDecision.Reject, "  ", TestData.Officer, actsForBranch: false);

        var refused = Assert.IsType<DecisionRefused>(result);
        Assert.Equal(("reason", "reason_required_override"), (refused.Field, refused.Code));
        Assert.Contains("differs from the recommendation", refused.Message);
    }

    [Fact]
    public void A_managers_override_with_a_reason_is_recorded_as_an_override()
    {
        var result = Service().RecordOfficerDecision(Scored(Recommendation.Reject), FinalDecision.Approve, "guarantor added", Manager, actsForBranch: true);

        var recorded = Assert.IsType<DecisionRecorded>(result);
        Assert.True(recorded.Decision.IsOverride);
        Assert.Null(recorded.Decision.SignedOffBy);
    }

    [Fact]
    public void An_officer_approving_against_a_reject_asks_for_a_sign_off()
    {
        var application = Scored(Recommendation.Reject);

        var result = Service().RecordOfficerDecision(application, FinalDecision.Approve, "guarantor added", TestData.Officer, actsForBranch: false);

        var requested = Assert.IsType<SignOffRequested>(result);
        Assert.Equal((SignOffReason.OverridesReject, FinalDecision.Approve, "guarantor added"), (requested.Request.Why, requested.Request.Outcome, requested.Request.Reason));
        Assert.Equal(ApplicationStage.AwaitingSignOff, application.Stage);
        Assert.Null(application.OfficerDecision);
    }

    [Fact]
    public void An_officer_approving_a_large_loan_asks_for_a_sign_off()
    {
        var application = Scored(Recommendation.Approve, SignOffRules.LargeLoanAmount + 1);

        var requested = Assert.IsType<SignOffRequested>(
            Service().RecordOfficerDecision(application, FinalDecision.Approve, null, TestData.Officer, actsForBranch: false));

        Assert.Equal(SignOffReason.LargeAmount, requested.Request.Why);
    }

    [Fact]
    public void Only_the_assignee_or_a_manager_decides()
    {
        var stranger = new Actor(Guid.NewGuid(), "Someone Else");

        Assert.IsType<NotAssignee>(Service().RecordOfficerDecision(Scored(Recommendation.Approve), FinalDecision.Approve, null, stranger, actsForBranch: false));
        Assert.IsType<DecisionRecorded>(Service().RecordOfficerDecision(Scored(Recommendation.Approve), FinalDecision.Approve, null, stranger, actsForBranch: true));
    }

    [Fact]
    public void Only_a_submitted_application_can_be_decided()
    {
        var withdrawn = Scored(Recommendation.Approve);
        withdrawn.Withdraw("client cancelled", TestData.Officer, TestData.Now);
        var waiting = Scored(Recommendation.Reject);
        waiting.RequestSignOff(FinalDecision.Approve, "x", SignOffReason.OverridesReject, TestData.Officer, TestData.Now);

        Assert.IsType<NotDecidable>(Service().RecordOfficerDecision(withdrawn, FinalDecision.Approve, null, TestData.Officer, actsForBranch: false));
        Assert.IsType<NotDecidable>(Service().RecordOfficerDecision(waiting, FinalDecision.Reject, "x", TestData.Officer, actsForBranch: true));
    }

    private static CreditApplication AwaitingSignOff()
    {
        var application = Scored(Recommendation.Reject);
        Service().RecordOfficerDecision(application, FinalDecision.Approve, "guarantor added", TestData.Officer, actsForBranch: false);
        return application;
    }

    [Fact]
    public void An_approved_sign_off_becomes_the_final_decision_naming_both()
    {
        var application = AwaitingSignOff();

        Assert.IsType<Changed>(Service().ReviewSignOff(application, approve: true, note: null, Manager));

        var decision = application.OfficerDecision!;
        Assert.Equal(ApplicationStage.Decided, application.Stage);
        Assert.Equal((FinalDecision.Approve, "guarantor added", "Tester", true), (decision.Outcome, decision.Reason, decision.DecidedBy, decision.IsOverride));
        Assert.Equal(("Marko Manager", Manager.UserId, TestData.Now), (decision.SignedOffBy, decision.SignedOffByUserId!.Value, decision.SignedOffAt!.Value));
        var request = Assert.Single(application.SignOffs);
        Assert.Equal((SignOffStatus.Approved, "Marko Manager"), (request.Status, request.ReviewedBy));
    }

    [Fact]
    public void A_returned_sign_off_needs_a_note_and_lets_the_officer_decide_again()
    {
        var application = AwaitingSignOff();

        var refused = Assert.IsType<Refused>(Service().ReviewSignOff(application, approve: false, note: " ", Manager));
        Assert.Equal(("note", "note_required"), (refused.Field, refused.Code));
        Assert.Equal(ApplicationStage.AwaitingSignOff, application.Stage);

        Assert.IsType<Changed>(Service().ReviewSignOff(application, approve: false, note: "get the guarantor's income first", Manager));
        Assert.Equal(ApplicationStage.Submitted, application.Stage);
        Assert.Equal(("get the guarantor's income first", SignOffStatus.Returned), (application.SignOffs.Single().Note, application.SignOffs.Single().Status));

        Assert.IsType<DecisionRecorded>(
            Service().RecordOfficerDecision(application, FinalDecision.Reject, "no guarantor income", TestData.Officer, actsForBranch: false));
    }

    [Fact]
    public void Reviewing_without_a_pending_request_is_refused()
    {
        var refused = Assert.IsType<Refused>(Service().ReviewSignOff(Scored(Recommendation.Approve), approve: true, note: null, Manager));

        Assert.Equal((null, "no_pending_sign_off"), (refused.Field, refused.Code));
    }

    [Fact]
    public void Review_case_needs_a_reason_but_is_not_an_override()
    {
        var refused = Assert.IsType<DecisionRefused>(
            Service().RecordOfficerDecision(Scored(Recommendation.Review), FinalDecision.Approve, null, TestData.Officer, actsForBranch: false));
        Assert.Equal(("reason", "reason_required_review"), (refused.Field, refused.Code));
        Assert.Contains("closer review", refused.Message);

        var recorded = Assert.IsType<DecisionRecorded>(
            Service().RecordOfficerDecision(Scored(Recommendation.Review), FinalDecision.Reject, "two recent delinquencies", TestData.Officer, actsForBranch: false));
        Assert.False(recorded.Decision.IsOverride);
    }

    [Fact]
    public void Decision_without_a_score_needs_a_reason()
    {
        var refused = Assert.IsType<DecisionRefused>(
            Service().RecordOfficerDecision(Unavailable(), FinalDecision.Approve, null, TestData.Officer, actsForBranch: false));
        Assert.Equal(("reason", "reason_required_no_recommendation"), (refused.Field, refused.Code));
        Assert.Contains("no recommendation", refused.Message);

        var recorded = Assert.IsType<DecisionRecorded>(
            Service().RecordOfficerDecision(Unavailable(), FinalDecision.Approve, "manual check", TestData.Officer, actsForBranch: false));
        Assert.False(recorded.Decision.IsOverride);
    }

    [Fact]
    public void Second_decision_is_refused_as_already_decided()
    {
        var application = Scored(Recommendation.Approve);
        application.RecordDecision(OfficerDecision.Record(Recommendation.Approve, FinalDecision.Approve, null, TestData.Officer, TestData.Now));

        Assert.IsType<AlreadyDecided>(
            Service().RecordOfficerDecision(application, FinalDecision.Reject, "changed my mind", TestData.Officer, actsForBranch: false));
    }
}
