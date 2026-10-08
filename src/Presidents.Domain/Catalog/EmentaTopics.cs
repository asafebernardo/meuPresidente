using Presidents.Domain.Common;

namespace Presidents.Domain.Catalog;

/// <summary>
/// Classifica uma ementa por palavras. Não é o índice oficial do Senado.
/// </summary>
public static class EmentaTopics
{
    private static readonly (string Slug, string[] Terms)[] Rules =
    [
        ("saude", ["saude", "anvisa", "hospital", "medicament", "enfermagem", "vigilancia sanitaria"]),
        ("educacao", ["educacao", "educacional", "ensino", "escola", "universid", "fundeb", "magisterio", "estudantil", "docencia", "pedagogia", "alfabetiz"]),
        ("previdencia", ["previdenc", "aposentador", "inss"]),
        ("assistencia-social", ["assistencia social", "bolsa familia", "prestacao continuada"]),
        ("meio-ambiente", ["meio ambiente", "ambiental", "florest", "clima", "recurso hidric", "ibama"]),
        ("tributacao", ["tribut", "imposto", "cofins", "simples nacional", "imposto de renda"]),
        ("trabalho", ["trabalh", "emprego", "salario", "fgts"]),
        ("agricultura", ["agricol", "agrari", "rural", "fundiari", "pecuaria"]),
        ("energia", ["energia", "eletrica", "petroleo", "gas natural", "biocombust"]),
        ("transportes", ["transporte", "rodovia", "ferrovia", "aeroport", "transito", "ponte"]),
        ("habitacao", ["habitac", "moradia"]),
        ("defesa", ["forcas armadas", "exercito", "marinha", "aeronautica", "defesa nacional"]),
        ("seguranca", ["seguranca publica", "policia", "penitenci", "arma de fogo", "violencia"]),
        ("direitos-humanos", ["direitos humanos", "crianca e do adolescente", "estatuto do idoso", "pessoa com deficiencia", "igualdade racial"]),
        ("cultura", ["cultura", "patrimonio cultural"]),
        ("esportes", ["esporte", "desporto", "basquete", "futebol", "voleibol", "olimpiad"]),
        ("comunicacao", ["radiodifus", "telecomunic", "comunicacao social"]),
        ("ciencia-e-tecnologia", ["ciencia", "tecnolog", "inovacao"]),
        ("industria", ["industria", "propriedade industrial"]),
        ("relacoes-internacionais", ["tratado", "convencao internacional", "acordo internacional", "relacoes exteriores"]),
        ("justica", ["processo civil", "processo penal", "poder judiciario", "advocacia", "notario", "cartorio"]),
        ("infraestrutura", ["saneamento", "infraestrutura"]),
        ("politica", ["partido politico", "eleitoral", "eleicao", "inelegib"]),
        ("economia", ["sistema financeiro", "banco central", "cambio", "exportac"]),
        ("administracao-publica", ["credito suplementar", "credito adicional", "credito especial", "orcament", "servidor publico", "licitacao", "administracao publica"])
    ];

    public static string? Match(string? ementa)
    {
        var folded = TextNormalizer.Fold(ementa);
        if (folded.Length == 0)
            return null;

        foreach (var (slug, terms) in Rules)
        {
            foreach (var term in terms)
            {
                if (folded.Contains(term, StringComparison.Ordinal))
                    return slug;
            }
        }

        return null;
    }
}
