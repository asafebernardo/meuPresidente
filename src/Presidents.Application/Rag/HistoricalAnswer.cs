using Presidents.Domain.Enums;

namespace Presidents.Application.Rag;

public sealed record EvidenceItem(
    Guid SourceId,
    string Statement,
    string SourceName,
    string? Url,
    DateOnly? Date,
    ReliabilityLevel Reliability,
    string ReliabilityLabel);

public sealed record EvidencePack(IReadOnlyList<EvidenceItem> Items);

public sealed record CitationDto(
    string Statement,
    string SourceName,
    string? Url,
    DateOnly? Date,
    string Reliability);

public sealed record HistoricalAnswer(string Text, bool InsufficientEvidence, IReadOnlyList<CitationDto> Citations);

public static class HistoricalAnswerComposer
{
    public const string InsufficientEvidence = "Não encontrei fontes suficientes no banco para afirmar isso.";

    public static HistoricalAnswer Compose(EvidencePack pack, IReadOnlyCollection<Guid>? selectedSourceIds)
    {
        if (pack.Items.Count == 0)
            return new HistoricalAnswer(InsufficientEvidence, true, []);

        var chosen = selectedSourceIds is null
            ? pack.Items.ToList()
            : pack.Items.Where(item => selectedSourceIds.Contains(item.SourceId)).ToList();

        if (chosen.Count == 0)
            return new HistoricalAnswer(InsufficientEvidence, true, []);

        var text = string.Join(Environment.NewLine + Environment.NewLine, chosen.Select(item => item.Statement).Distinct());
        var citations = chosen.Select(item => new CitationDto(
            item.Statement,
            item.SourceName,
            item.Url,
            item.Date,
            item.ReliabilityLabel)).ToList();

        return new HistoricalAnswer(text, false, citations);
    }
}

public interface ILlmClient
{
    bool IsEnabled { get; }
    Task<IReadOnlyList<Guid>> SelectSourcesAsync(string question, EvidencePack pack, CancellationToken cancellationToken);
}

public interface IHistoricalAnswerService
{
    Task<HistoricalAnswer> AnswerAsync(string question, CancellationToken cancellationToken);
}
