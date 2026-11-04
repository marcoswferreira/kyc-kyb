using System.ComponentModel.DataAnnotations;

namespace Application.Dtos;

public record CreateTenantRequest(
    [Required] string Name,
    [Required] string Slug,
    int RateLimitPerMinute = 60);

