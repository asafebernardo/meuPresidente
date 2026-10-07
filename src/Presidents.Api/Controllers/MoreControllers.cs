using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Presidents.Application.Comparison;
using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Application.Search;
using Presidents.Application.Services;
using Presidents.Domain.Enums;

namespace Presidents.Api.Controllers;

[Route("api/v1/policies")]
public sealed class PoliciesController(PolicyService policies, QueryService queries, PublicationService publication) : ApiController
{
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PolicyCardDto>> Get(string slug, CancellationToken cancellationToken)
    {
        var policy = await queries.PolicyAsync(slug, cancellationToken);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] PolicyForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await policies.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] PolicyForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await policies.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangePolicyAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await policies.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/sources")]
public sealed class SourcesController(SourceService sources, QueryService queries, PublicationService publication, StatementService statements) : ApiController
{
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<SourcePageDto>> Get(string slug, CancellationToken cancellationToken)
    {
        var source = await queries.SourceAsync(slug, cancellationToken);
        return source is null ? NotFound() : Ok(source);
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] SourceForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await sources.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] SourceForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await sources.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangeSourceAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpPost("links")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Link([FromBody] FactSourceForm form, CancellationToken cancellationToken) =>
        Ok(await statements.LinkSourceAsync(form, Actor, cancellationToken));

    [HttpDelete("links/{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Unlink(Guid id, CancellationToken cancellationToken)
    {
        await statements.UnlinkSourceAsync(id, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sources.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/indicators")]
public sealed class IndicatorsController(IndicatorService indicators, PublicationService publication) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IReadOnlyList<IndicatorForm>> List(CancellationToken cancellationToken)
    {
        var items = await indicators.ListPublicAsync(cancellationToken);
        return items.Select(item => new IndicatorForm { Id = item.Id, Name = item.Name, Unit = item.Unit, Description = item.Description, MethodologySourceId = item.MethodologySourceId }).ToList();
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] IndicatorForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await indicators.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] IndicatorForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await indicators.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("values")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> AddValue([FromBody] IndicatorValueForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await indicators.SaveValueAsync(form, Actor, cancellationToken));
    }

    [HttpPost("values/{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> ValueStatus(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangeIndicatorValueAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("values/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteValue(Guid id, CancellationToken cancellationToken)
    {
        await indicators.DeleteValueAsync(id, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await indicators.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/statements")]
public sealed class StatementsController(StatementService statements, PublicationService publication) : ApiController
{
    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] StatementForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await statements.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] StatementForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await statements.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangeStatementAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await statements.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/search")]
public sealed class SearchController(QueryService queries) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("search")]
    public Task<SearchResponse> Get([FromQuery] string? q, CancellationToken cancellationToken) => queries.SearchAsync(q, cancellationToken);
}

[Route("api/v1/compare")]
public sealed class CompareController(QueryService queries) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public Task<ComparisonResult> Get([FromQuery] string[] slug, CancellationToken cancellationToken) =>
        queries.CompareAsync(slug, cancellationToken);
}

[Route("api/v1/timeline")]
public sealed class TimelineController(QueryService queries) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public Task<TimelineDto> Get(CancellationToken cancellationToken) => queries.TimelineAsync(cancellationToken);
}

[Route("api/v1/answers")]
public sealed class AnswersController(IHistoricalAnswerService answers) : ApiController
{
    public sealed class QuestionBody
    {
        public string Question { get; set; } = "";
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("search")]
    public Task<HistoricalAnswer> Ask([FromBody] QuestionBody body, CancellationToken cancellationToken) =>
        answers.AnswerAsync(body.Question ?? "", cancellationToken);
}

[Route("api/v1/imports")]
public sealed class ImportsController(ImportCoordinator imports) : ApiController
{
    [HttpPost("{channel}")]
    [Authorize(Policy = "Editor")]
    public Task<ImportResultDto> Import(string channel, [FromBody] ImportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ImportChannel>(channel, ignoreCase: true, out var parsed))
            throw new Presidents.Domain.Common.DomainException("Canal de importação desconhecido. Use Legislation, News ou Academic.");
        return imports.ImportAsync(parsed, request, Actor, cancellationToken);
    }
}

[Route("api/v1/admin")]
public sealed class AdminController(QueryService queries) : ApiController
{
    [HttpGet("stats")]
    [Authorize(Policy = "Editor")]
    public Task<AdminStatsDto> Stats(CancellationToken cancellationToken) => queries.AdminStatsAsync(Actor, cancellationToken);
}
