namespace Application.Dtos;

public record RegisterWebhookRequest(string TargetUrl, string? Events = "check.completed");

