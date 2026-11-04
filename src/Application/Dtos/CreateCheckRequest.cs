using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.Dtos;

public record CreateCheckRequest(
    [Required] string Document,
    [Required] SubjectType SubjectType,
    [Required] string Name,
    [Required] CheckPurpose Purpose,
    [Required] LegalBasis LegalBasis,
    [Required] bool ConsentObtained,
    DateTime? ConsentTimestamp = null,
    string? WebhookUrl = null,
    string? CustomReference = null,
    int RetentionDays = 30);

