using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;

namespace Presidents.Infrastructure.Persistence;

internal static class Config
{
    public static void Publication<T>(EntityTypeBuilder<T> builder) where T : PublishableEntity
    {
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Provenance).HasConversion<string>().HasMaxLength(32);
    }
}

internal sealed class PresidentConfiguration : IEntityTypeConfiguration<President>
{
    public void Configure(EntityTypeBuilder<President> builder)
    {
        builder.ToTable("Presidents");
        Config.Publication(builder);
        builder.Property(x => x.Name).HasMaxLength(FieldLimits.Name).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(FieldLimits.Title).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Party).HasMaxLength(FieldLimits.Party);
        builder.Property(x => x.VicePresident).HasMaxLength(FieldLimits.Name);
        builder.Property(x => x.Biography).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.HistoricalContext).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.PhotoUrl).HasMaxLength(FieldLimits.Url);
        builder.Property(x => x.WikipediaUrl).HasMaxLength(FieldLimits.Url);
        builder.Property(x => x.OfficialBiographyUrl).HasMaxLength(FieldLimits.Url);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.LongText);
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasMany(x => x.Presidencies).WithOne(x => x.President).HasForeignKey(x => x.PresidentId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_President_Life", "\"DeathDate\" IS NULL OR \"BirthDate\" IS NULL OR \"DeathDate\" >= \"BirthDate\"");
            table.HasCheckConstraint("CK_President_Span", "\"EndDate\" IS NULL OR \"StartDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
        });
    }
}

internal sealed class PresidencyConfiguration : IEntityTypeConfiguration<Presidency>
{
    public void Configure(EntityTypeBuilder<Presidency> builder)
    {
        builder.ToTable("Presidencies");
        Config.Publication(builder);
        builder.Property(x => x.Party).HasMaxLength(FieldLimits.Party);
        builder.Property(x => x.VicePresident).HasMaxLength(FieldLimits.Name);
        builder.Property(x => x.PoliticalContext).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.DivergenceNote).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.GovernmentType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ArrivalMethod).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => x.StartDate);
        builder.ToTable(table => table.HasCheckConstraint("CK_Presidency_Period", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        Config.Publication(builder);
        builder.Property(x => x.Name).HasMaxLength(FieldLimits.Name).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.Summary);
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class EventConfiguration : IEntityTypeConfiguration<HistoricalEvent>
{
    public void Configure(EntityTypeBuilder<HistoricalEvent> builder)
    {
        builder.ToTable("HistoricalEvents");
        Config.Publication(builder);
        builder.Property(x => x.Title).HasMaxLength(FieldLimits.Title).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.NeutralSummary).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.DivergenceNote).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Importance).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SummaryKind).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => x.EventDate);
        builder.HasIndex(x => x.Title);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasOne(x => x.President).WithMany().HasForeignKey(x => x.PresidentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Presidency).WithMany().HasForeignKey(x => x.PresidencyId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(table => table.HasCheckConstraint("CK_Event_Period", "\"EndDate\" IS NULL OR \"EventDate\" IS NULL OR \"EndDate\" >= \"EventDate\""));
    }
}

internal sealed class LawConfiguration : IEntityTypeConfiguration<Law>
{
    public void Configure(EntityTypeBuilder<Law> builder)
    {
        builder.ToTable("Laws");
        Config.Publication(builder);
        builder.Property(x => x.Number).HasMaxLength(40).IsRequired();
        builder.Property(x => x.NumberNormalized).HasMaxLength(40);
        builder.Property(x => x.Title).HasMaxLength(FieldLimits.Title).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.FullTextUrl).HasMaxLength(FieldLimits.Url);
        builder.Property(x => x.Proposer).HasMaxLength(FieldLimits.Name);
        builder.Property(x => x.LegislativeProcessNote).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.VetoNote).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.DivergenceNote).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.OperationalStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Origin).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => x.Number);
        builder.HasIndex(x => x.Year);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.Kind, x.NumberNormalized, x.Year }).IsUnique();
        builder.HasOne(x => x.PresidentInOffice).WithMany().HasForeignKey(x => x.PresidentInOfficeId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");
        Config.Publication(builder);
        builder.Property(x => x.Name).HasMaxLength(FieldLimits.Title).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.Objective).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.LongText);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasOne(x => x.President).WithMany().HasForeignKey(x => x.PresidentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Presidency).WithMany().HasForeignKey(x => x.PresidencyId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(table => table.HasCheckConstraint("CK_Policy_Period", "\"EndDate\" IS NULL OR \"StartDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
    }
}

internal sealed class SourceConfiguration : IEntityTypeConfiguration<SourceRecord>
{
    public void Configure(EntityTypeBuilder<SourceRecord> builder)
    {
        builder.ToTable("Sources");
        Config.Publication(builder);
        builder.Property(x => x.Name).HasMaxLength(FieldLimits.Title).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(FieldLimits.Url);
        builder.Property(x => x.Publisher).HasMaxLength(FieldLimits.Name);
        builder.Property(x => x.Author).HasMaxLength(FieldLimits.Name);
        builder.Property(x => x.Title).HasMaxLength(FieldLimits.Title);
        builder.Property(x => x.Notes).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.LongText);
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ReliabilityLevel).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.Url).IsUnique().HasFilter("\"Url\" IS NOT NULL");
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class FactSourceConfiguration : IEntityTypeConfiguration<FactSource>
{
    public void Configure(EntityTypeBuilder<FactSource> builder)
    {
        builder.ToTable("FactSources");
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Excerpt).HasMaxLength(FieldLimits.Excerpt);
        builder.Property(x => x.Annotation).HasMaxLength(FieldLimits.Summary);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.SourceId);
        builder.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_FactSource_Excerpt", "\"Excerpt\" IS NULL OR char_length(\"Excerpt\") <= 400"));
    }
}

internal sealed class StatementConfiguration : IEntityTypeConfiguration<SourcedStatement>
{
    public void Configure(EntityTypeBuilder<SourcedStatement> builder)
    {
        builder.ToTable("Statements");
        Config.Publication(builder);
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Text).HasMaxLength(FieldLimits.Summary).IsRequired();
        builder.Property(x => x.Attribution).HasMaxLength(FieldLimits.Name);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.Summary);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

internal sealed class DivergenceConfiguration : IEntityTypeConfiguration<SourceDivergence>
{
    public void Configure(EntityTypeBuilder<SourceDivergence> builder)
    {
        builder.ToTable("SourceDivergences");
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Description).HasMaxLength(FieldLimits.Summary).IsRequired();
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

internal sealed class IndicatorConfiguration : IEntityTypeConfiguration<Indicator>
{
    public void Configure(EntityTypeBuilder<Indicator> builder)
    {
        builder.ToTable("Indicators");
        Config.Publication(builder);
        builder.Property(x => x.Name).HasMaxLength(FieldLimits.Name).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(FieldLimits.Slug).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(FieldLimits.Unit).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.SearchText).HasMaxLength(FieldLimits.Summary);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasOne(x => x.MethodologySource).WithMany().HasForeignKey(x => x.MethodologySourceId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class IndicatorObservationConfiguration : IEntityTypeConfiguration<IndicatorObservation>
{
    public void Configure(EntityTypeBuilder<IndicatorObservation> builder)
    {
        builder.ToTable("IndicatorObservations");
        Config.Publication(builder);
        builder.Property(x => x.Value).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(FieldLimits.Summary);
        builder.HasIndex(x => new { x.IndicatorId, x.ReferenceDate }).IsUnique();
        builder.HasOne(x => x.Indicator).WithMany().HasForeignKey(x => x.IndicatorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PresidentIndicatorConfiguration : IEntityTypeConfiguration<PresidentIndicator>
{
    public void Configure(EntityTypeBuilder<PresidentIndicator> builder)
    {
        builder.ToTable("PresidentIndicators");
        Config.Publication(builder);
        builder.Property(x => x.Value).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(FieldLimits.Summary);
        builder.HasIndex(x => new { x.PresidentId, x.IndicatorId, x.Year }).IsUnique();
        builder.HasOne(x => x.President).WithMany().HasForeignKey(x => x.PresidentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Indicator).WithMany(x => x.Values).HasForeignKey(x => x.IndicatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CategoryLinkConfiguration : IEntityTypeConfiguration<ContentCategoryLink>
{
    public void Configure(EntityTypeBuilder<ContentCategoryLink> builder)
    {
        builder.ToTable("ContentCategoryLinks");
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.CategoryId }).IsUnique();
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IngestionConfiguration : IEntityTypeConfiguration<IngestionRecord>
{
    public void Configure(EntityTypeBuilder<IngestionRecord> builder)
    {
        builder.ToTable("IngestionRecords");
        builder.Property(x => x.AdapterName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(FieldLimits.Url);
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        builder.Property(x => x.ContentHash).HasMaxLength(FieldLimits.Hash).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(FieldLimits.Summary);
        builder.Property(x => x.RawContent).HasMaxLength(20000);
        builder.HasIndex(x => new { x.AdapterName, x.ContentHash });
        builder.HasIndex(x => x.SourceUrl);
        builder.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Law).WithMany().HasForeignKey(x => x.LawId).OnDelete(DeleteBehavior.SetNull);
    }
}
