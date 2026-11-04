namespace Application.Interfaces;

public interface IRiskPolicyEngine
{
    PolicyEvaluationResult Evaluate(Guid checkId, IEnumerable<ProviderResult> providerResults);
}

