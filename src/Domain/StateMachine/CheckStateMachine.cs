using Domain.Exceptions;

namespace Domain.StateMachine;

/// <summary>
/// Máquina de estados finita e determinística para orquestração do ciclo de vida de Background Checks.
/// Garante que o status do cliente nunca seja binário ou ambíguo, travando transições ilegais.
/// </summary>
public sealed class CheckStateMachine
{
    private static readonly Dictionary<CheckState, HashSet<CheckState>> AllowedTransitions = new()
    {
        [CheckState.Pending] = [CheckState.Processing, CheckState.Failed, CheckState.Purged],
        [CheckState.Processing] = [CheckState.AwaitingHumanReview, CheckState.Failed, CheckState.Purged],
        [CheckState.AwaitingHumanReview] = [CheckState.Approved, CheckState.Rejected, CheckState.RequiresMoreInfo, CheckState.Purged],
        [CheckState.RequiresMoreInfo] = [CheckState.Processing, CheckState.Approved, CheckState.Rejected, CheckState.Purged],
        [CheckState.Approved] = [CheckState.Purged],
        [CheckState.Rejected] = [CheckState.Purged],
        [CheckState.Failed] = [CheckState.Pending, CheckState.Purged],
        [CheckState.Purged] = [] // Estado terminal imutável
    };

    public CheckState CurrentState { get; private set; }

    public CheckStateMachine(CheckState initialState = CheckState.Pending)
    {
        CurrentState = initialState;
    }

    public bool CanTransitionTo(CheckState targetState)
    {
        return AllowedTransitions.TryGetValue(CurrentState, out var targets) && targets.Contains(targetState);
    }

    public void TransitionTo(CheckState targetState)
    {
        if (!CanTransitionTo(targetState))
        {
            throw new InvalidStateTransitionException(CurrentState, targetState);
        }

        CurrentState = targetState;
    }

    public IReadOnlyCollection<CheckState> GetAllowedNextStates()
    {
        return AllowedTransitions.TryGetValue(CurrentState, out var targets)
            ? targets
            : Array.Empty<CheckState>();
    }
}

