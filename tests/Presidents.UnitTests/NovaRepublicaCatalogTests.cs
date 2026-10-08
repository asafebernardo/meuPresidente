using Presidents.Domain.Catalog;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;

namespace Presidents.UnitTests;

public sealed class NovaRepublicaCatalogTests
{
    [Fact]
    public void Ementa_de_saude_vence_credito_orcamentario()
    {
        Assert.Equal("saude", EmentaTopics.Match("Abre crédito suplementar ao Ministério da Saúde."));
        Assert.Equal("administracao-publica", EmentaTopics.Match("Abre crédito suplementar ao Orçamento Fiscal."));
        Assert.Null(EmentaTopics.Match("   "));
    }

    [Fact]
    public void Data_limite_fica_com_o_mandato_que_comeca_nesse_dia()
    {
        var collor = Mandate(new DateOnly(1990, 3, 15), new DateOnly(1992, 10, 2));
        var itamar = Mandate(new DateOnly(1992, 10, 2), new DateOnly(1995, 1, 1));
        var mandates = new List<Presidency> { collor, itamar };

        Assert.Equal(itamar.Id, MandateCoverage.Find(mandates, new DateOnly(1992, 10, 2))!.Id);
        Assert.Equal(collor.Id, MandateCoverage.Find(mandates, new DateOnly(1992, 10, 1))!.Id);
        Assert.False(MandateCoverage.CoversFullYear(mandates, collor, 1992));
        Assert.True(MandateCoverage.CoversFullYear(mandates, itamar, 1994));
    }

    [Fact]
    public void Metricas_acumulam_inflacao_e_separam_moeda_antes_do_real()
    {
        Assert.Equal("21,0", PeriodMetrics.Format(PeriodMetrics.Compound([10m, 10m])));
        Assert.Contains("21,0%", PeriodMetrics.AccumulatedInflation([10m, 10m]));
        Assert.Contains("não há variação percentual", PeriodMetrics.NominalWage(new DateOnly(1990, 2, 1), 2004.37m, new DateOnly(1994, 6, 1), 64.79m));
        Assert.Contains("42,9%", PeriodMetrics.NominalWage(new DateOnly(1995, 1, 1), 70m, new DateOnly(1995, 5, 1), 100m));
        Assert.Contains("queda de 1,9 ponto percentual", PeriodMetrics.UnemploymentSpan(8.0m, 6.1m));
    }

    private static Presidency Mandate(DateOnly start, DateOnly end)
    {
        var mandate = new Presidency { ArrivalMethod = ArrivalMethod.NotInformed };
        mandate.SetPeriod(start, end);
        return mandate;
    }
}
