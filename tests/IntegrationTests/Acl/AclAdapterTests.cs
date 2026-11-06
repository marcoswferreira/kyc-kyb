using Application.Acl;
using Application.Acl.Models;
using Application.Interfaces;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Acl.Adapters;
using Infrastructure.Providers;
using Xunit;

namespace IntegrationTests.Acl;

public class AclAdapterTests
{
    private readonly IEnumerable<IBackgroundCheckProvider> _providers = new IBackgroundCheckProvider[]
    {
        new ReceitaFederalMockProvider(),
        new TribunaisProcessosMockProvider(),
        new ListasRestritivasMockProvider(),
        new TrabalhoEscravoMockProvider(),
        new QsaSociosMockProvider()
    };

    [Fact]
    public async Task ReceitaFederalAdapter_ShouldMapToCanonicalCadastralProfile()
    {
        var adapter = new ReceitaFederalAdapter(_providers);

        var result = await adapter.QueryCanonicalAsync("52998224725", SubjectType.Person);

        result.IsSuccess.Should().BeTrue();
        result.ProviderKey.Should().Be("ReceitaFederal");
        result.Data.Should().NotBeNull();
        result.Data!.Document.Should().Be("52998224725");
        result.Data.SubjectType.Should().Be(SubjectType.Person);
        result.Data.CadastralStatus.Should().Be("REGULAR");
        result.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ReceitaFederalAdapter_WhenProviderFails_ShouldReturnFailureResult()
    {
        var adapter = new ReceitaFederalAdapter(_providers);

        // Document ending with 999 triggers simulated 503
        var result = await adapter.QueryCanonicalAsync("111222999", SubjectType.Person);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("indisponível");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task TribunaisAdapter_ShouldMapToCanonicalJudicialRecords()
    {
        var adapter = new TribunaisAdapter(_providers);

        // Document with 666 has criminal process
        var result = await adapter.QueryCanonicalAsync("12366678901", SubjectType.Person);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().NotBeEmpty();
        var record = result.Data.First();
        record.Court.Should().Be("TJSP");
        record.RiskLevel.Should().Be(RiskIndicator.Red);
        record.Role.Should().Be("Réu/Executado");
    }

    [Fact]
    public async Task ListasRestritivasAdapter_ShouldMapPepAndSanctions()
    {
        var adapter = new ListasRestritivasAdapter(_providers);

        // Document with 777 has PEP, 888 has OFAC
        var resultPep = await adapter.QueryCanonicalAsync("12377778901", SubjectType.Person);
        resultPep.IsSuccess.Should().BeTrue();
        resultPep.Data.Should().Contain(s => s.Category == "Pessoa Exposta Politicamente");

        var resultOfac = await adapter.QueryCanonicalAsync("12388878901", SubjectType.Person);
        resultOfac.IsSuccess.Should().BeTrue();
        resultOfac.Data.Should().Contain(s => s.ListCode == "OFAC-SDN" && s.RiskLevel == RiskIndicator.Red);
    }

    [Fact]
    public async Task TrabalhoEscravoAdapter_ShouldMapLaborSanctions()
    {
        var adapter = new TrabalhoEscravoAdapter(_providers);

        // Document with 555 triggers slave labor listing
        var result = await adapter.QueryCanonicalAsync("12355578901", SubjectType.Person);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
        var labor = result.Data.First();
        labor.EmployerName.Should().Contain("Fazenda Santa Fé");
        labor.RescuedWorkersCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QsaSociosAdapter_ShouldMapCompanyPartners()
    {
        var adapter = new QsaSociosAdapter(_providers);

        var result = await adapter.QueryCanonicalAsync("11222333000181", SubjectType.Company);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
        result.Data!.Count.Should().Be(2);
        result.Data.Should().Contain(p => p.Qualification == "Sócio-Administrador" && p.SharePercentage == 60.0m);
    }
}

