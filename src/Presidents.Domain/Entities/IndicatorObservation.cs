using Presidents.Domain.Common;

namespace Presidents.Domain.Entities;

public sealed class IndicatorObservation : PublishableEntity
{
    public Guid IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }
    public DateOnly ReferenceDate { get; set; }
    public decimal Value { get; set; }
    public Guid SourceId { get; set; }
    public SourceRecord? Source { get; set; }
    public string? Note { get; set; }
}
