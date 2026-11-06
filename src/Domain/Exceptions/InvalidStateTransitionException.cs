using Domain.StateMachine;

namespace Domain.Exceptions;

/// <summary>
/// Exceção lançada quando ocorre uma tentativa de transição proibida na máquina de estados de Background Check.
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    public CheckState CurrentState { get; }
    public CheckState TargetState { get; }

    public InvalidStateTransitionException(CheckState currentState, CheckState targetState, string? message = null)
        : base(message ?? $"Transição de estado inválida: Não é permitido transicionar de '{currentState}' para '{targetState}'.")
    {
        CurrentState = currentState;
        TargetState = targetState;
    }
}

