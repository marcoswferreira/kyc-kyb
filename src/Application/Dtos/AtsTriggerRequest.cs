using System.ComponentModel.DataAnnotations;

namespace Application.Dtos;

public record AtsTriggerRequest(
    [Required] string CandidateName,
    [Required] string Document,
    [Required] string JobTitle,
    [Required] string StageName,
    string CallbackUrl = "http://localhost:5000/api/v1/ats/callback");

