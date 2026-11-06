using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using Domain.StateMachine;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests;

public class StateMachineTests
{
    [Fact]
    public void CheckStateMachine_ShouldAllow_ValidForwardTransitions()
    {
        var sm = new CheckStateMachine(CheckState.Pending);

        sm.CanTransitionTo(CheckState.Processing).Should().BeTrue();
        sm.TransitionTo(CheckState.Processing);
        sm.CurrentState.Should().Be(CheckState.Processing);

        sm.CanTransitionTo(CheckState.AwaitingHumanReview).Should().BeTrue();
        sm.TransitionTo(CheckState.AwaitingHumanReview);
        sm.CurrentState.Should().Be(CheckState.AwaitingHumanReview);

        sm.CanTransitionTo(CheckState.Approved).Should().BeTrue();
        sm.TransitionTo(CheckState.Approved);
        sm.CurrentState.Should().Be(CheckState.Approved);

        sm.CanTransitionTo(CheckState.Purged).Should().BeTrue();
        sm.TransitionTo(CheckState.Purged);
        sm.CurrentState.Should().Be(CheckState.Purged);
    }

    [Fact]
    public void CheckStateMachine_ShouldAllow_RejectionAndMoreInfoTransitions()
    {
        var sm1 = new CheckStateMachine(CheckState.AwaitingHumanReview);
        sm1.CanTransitionTo(CheckState.Rejected).Should().BeTrue();
        sm1.TransitionTo(CheckState.Rejected);
        sm1.CurrentState.Should().Be(CheckState.Rejected);

        var sm2 = new CheckStateMachine(CheckState.AwaitingHumanReview);
        sm2.CanTransitionTo(CheckState.RequiresMoreInfo).Should().BeTrue();
        sm2.TransitionTo(CheckState.RequiresMoreInfo);
        sm2.CurrentState.Should().Be(CheckState.RequiresMoreInfo);

        sm2.CanTransitionTo(CheckState.Processing).Should().BeTrue();
        sm2.TransitionTo(CheckState.Processing);
        sm2.CurrentState.Should().Be(CheckState.Processing);
    }

    [Fact]
    public void CheckStateMachine_ShouldAllow_FailureAndRetryTransitions()
    {
        var sm = new CheckStateMachine(CheckState.Pending);
        sm.TransitionTo(CheckState.Failed);
        sm.CurrentState.Should().Be(CheckState.Failed);

        sm.CanTransitionTo(CheckState.Pending).Should().BeTrue();
        sm.TransitionTo(CheckState.Pending);
        sm.CurrentState.Should().Be(CheckState.Pending);
    }

    [Fact]
    public void CheckStateMachine_ShouldThrow_WhenTransitionIsProhibited()
    {
        var sm = new CheckStateMachine(CheckState.Processing);

        // Cannot skip directly from Processing to Approved
        var act1 = () => sm.TransitionTo(CheckState.Approved);
        act1.Should().Throw<InvalidStateTransitionException>()
            .Where(e => e.CurrentState == CheckState.Processing && e.TargetState == CheckState.Approved);

        // Purged is terminal
        var smPurged = new CheckStateMachine(CheckState.Purged);
        var act2 = () => smPurged.TransitionTo(CheckState.Pending);
        act2.Should().Throw<InvalidStateTransitionException>();
        smPurged.GetAllowedNextStates().Should().BeEmpty();
    }

    [Fact]
    public void BackgroundCheck_Lifecycle_ShouldEnforceDeterministicStatesAndEmitEvents()
    {
        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "12345678901",
            "123.456.789-01",
            "***.456.789-**",
            "Teste de Estado",
            CheckPurpose.ConcessaoDeCredito,
            LegalBasis.PrevencaoAFraude,
            true);

        check.State.Should().Be(CheckState.Pending);
        check.DomainEvents.Should().ContainSingle(e => e is CheckCreatedEvent);

        // Processing
        check.StartProcessing();
        check.State.Should().Be(CheckState.Processing);
        check.DomainEvents.Should().Contain(e => e is CheckProcessingStartedEvent);

        // Complete -> AwaitingHumanReview
        var findings = new List<CheckFinding>
        {
            CheckFinding.Create(check.Id, "Receita", "Cadastral", RiskIndicator.Green, "Regular", "Situação cadastral regular", "Explicacao", "ev-1", "{}")
        };
        check.Complete(RiskIndicator.Yellow, findings, "{}", "fake-hash");
        check.State.Should().Be(CheckState.AwaitingHumanReview);
        check.DomainEvents.Should().Contain(e => e is CheckCompletedAwaitingReviewEvent);

        // Human Review -> Approved
        var reviewResult = check.RecordHumanReview("analyst-123", HumanReviewDecision.ApprovedByHuman, "Aprovado com ressalvas");
        reviewResult.IsSuccess.Should().BeTrue();
        check.State.Should().Be(CheckState.Approved);
        check.DomainEvents.Should().Contain(e => e is HumanReviewDeliberatedEvent);

        // Purge
        check.PurgePersonalData();
        check.State.Should().Be(CheckState.Purged);
        check.IsPurged.Should().BeTrue();
        check.DomainEvents.Should().Contain(e => e is DataRetentionPurgedEvent);

        // Purged check cannot transition
        var act = () => check.RecordHumanReview("analyst-123", HumanReviewDecision.ApprovedByHuman, "Tentativa em purgado");
        act.Should().Throw<InvalidStateTransitionException>();
    }
}
