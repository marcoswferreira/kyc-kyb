using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using FluentAssertions;
using Infrastructure.Audit;
using Infrastructure.Database;
using Infrastructure.Database.Repositories;
using Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests.MessagingAndAudit;

public class MessagingAndAuditTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MessagingAndAuditTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ChannelMessageBus_ShouldPublishAndDeliverEventsAsynchronously()
    {
        var bus = new ChannelMessageBus();
        var topic = "orders.kyc.created";
        var expectedEvent = new CheckProcessingStartedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var receivedEvents = new List<CheckProcessingStartedEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var item in bus.SubscribeAsync<CheckProcessingStartedEvent>(topic, cts.Token))
            {
                receivedEvents.Add(item);
                if (receivedEvents.Count >= 1)
                {
                    break;
                }
            }
        });

        await bus.PublishAsync(topic, expectedEvent, CancellationToken.None);
        await subscriberTask;

        receivedEvents.Should().ContainSingle();
        receivedEvents[0].CheckId.Should().Be(expectedEvent.CheckId);
    }

    [Fact]
    public async Task DomainEventDispatcher_ShouldPersistAuditEntriesInCryptographicHashChain()
    {
        var options = new DbContextOptionsBuilder<KycKybDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new KycKybDbContext(options);
        var auditRepo = new AuditLogRepository(db);
        var dispatcher = new DomainEventDispatcher(auditRepo, NullLogger<DomainEventDispatcher>.Instance);

        var tenantId = Guid.NewGuid();
        var checkId = Guid.NewGuid();

        // Evento 1: Criação
        var ev1 = new CheckCreatedEvent(Guid.NewGuid(), DateTime.UtcNow, checkId, tenantId, "***.982.247-**", SubjectType.Person, CheckPurpose.PrevencaoAFraude);
        await dispatcher.DispatchAsync(ev1);

        // Evento 2: Início de processamento
        var ev2 = new CheckProcessingStartedEvent(Guid.NewGuid(), DateTime.UtcNow, checkId, tenantId);
        await dispatcher.DispatchAsync(ev2);

        // Evento 3: Deliberação humana (LGPD Art. 20)
        var ev3 = new HumanReviewDeliberatedEvent(Guid.NewGuid(), DateTime.UtcNow, checkId, tenantId, "compliance_officer_01", HumanReviewDecision.ApprovedByHuman, "Conformidade Bacen 3978");
        await dispatcher.DispatchAsync(ev3);

        // Validar trilha de auditoria encadeada
        var entries = await auditRepo.ListByTenantAsync(tenantId, 0, 10);
        entries.Should().HaveCount(3);

        var entry1 = entries.First(e => e.Action.Contains("CHECKCREATEDEVENT"));
        var entry2 = entries.First(e => e.Action.Contains("CHECKPROCESSINGSTARTEDEVENT"));
        var entry3 = entries.First(e => e.Action.Contains("HUMANREVIEWDELIBERATEDEVENT"));

        // Encadeamento: PreviousHash de entry2 deve ser Hash de entry1
        entry1.PreviousHash.Should().Contain("GENESIS_BLOCK");
        entry2.PreviousHash.Should().Be(entry1.Hash);
        entry3.PreviousHash.Should().Be(entry2.Hash);

        // Hash não-vazio e válido SHA-256 (64 hex characters)
        entry1.Hash.Should().HaveLength(64);
        entry2.Hash.Should().HaveLength(64);
        entry3.Hash.Should().HaveLength(64);
    }

    [Fact]
    public async Task ProviderWebhook_Endpoint_ShouldAcceptAndAcknowledgeWithoutApiKey()
    {
        var client = _factory.CreateClient();

        var payload = new ProviderWebhookPayload(
            EventId: "evt_idwall_98765",
            ProviderName: "Idwall",
            CheckReferenceId: Guid.NewGuid().ToString(),
            DocumentNumber: "52998224725",
            Status: "COMPLETED",
            Data: JsonSerializer.SerializeToElement(new { score = 850, risk = "LOW" }),
            Signature: "sha256_mock_signature_123456",
            Timestamp: DateTime.UtcNow);

        var response = await client.PostAsJsonAsync("/api/v1/webhooks/providers/idwall", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ProviderWebhookResponse>();
        body.Should().NotBeNull();
        body!.Acknowledged.Should().BeTrue();
        body.Message.Should().ContainEquivalentOf("Idwall");
        body.IngestionReceiptId.Should().NotBeNullOrWhiteSpace();
    }
}
