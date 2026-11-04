using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.Dtos;

public record ReviewCheckRequest(
    [Required] string ReviewerId,
    [Required] HumanReviewDecision Decision,
    [Required] string Notes);

