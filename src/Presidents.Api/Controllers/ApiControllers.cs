using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Application.Search;
using Presidents.Application.Security;
using Presidents.Application.Services;
using Presidents.Domain.Enums;

namespace Presidents.Api.Controllers;

[ApiController]
[EnableRateLimiting("public")]
public abstract class ApiController : ControllerBase
{
    protected ActorContext Actor => new(
        User.FindFirstValue(ClaimTypes.NameIdentifier),
        AppRoles.All.Where(User.IsInRole).ToArray());
}

[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth) : ApiController
{
    public sealed class LoginBody
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginBody body, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(body.Email, body.Password, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }
}

[Route("api/v1/home")]
public sealed class HomeController(QueryService queries) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public Task<HomePageDto> Get(CancellationToken cancellationToken) => queries.HomeAsync(cancellationToken);
}

[Route("api/v1/presidents")]
public sealed class PresidentsController(PresidentService presidents, PublicationService publication) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResult<PresidentListItemDto>> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        presidents.ListPublicAsync(q, page, pageSize, cancellationToken);

    [HttpGet("admin")]
    [Authorize(Policy = "Editor")]
    public Task<PagedResult<AdminRow>> AdminList([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        presidents.ListAdminAsync(Actor, q, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}/form")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<PresidentForm>> Form(Guid id, CancellationToken cancellationToken)
    {
        var form = await presidents.GetFormAsync(id, Actor, cancellationToken);
        return form is null ? NotFound() : Ok(form);
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] PresidentForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        var id = await presidents.SaveAsync(form, Actor, cancellationToken);
        return Created($"/api/v1/presidents/{id}", id);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] PresidentForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await presidents.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangePresidentAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await presidents.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/public/presidents")]
public sealed class PublicPresidentsController(QueryService queries) : ApiController
{
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PresidentDetailDto>> Get(string slug, CancellationToken cancellationToken)
    {
        var president = await queries.PresidentAsync(slug, cancellationToken);
        return president is null ? NotFound() : Ok(president);
    }
}

[Route("api/v1/presidencies")]
public sealed class PresidenciesController(PresidencyService presidencies, PublicationService publication) : ApiController
{
    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] PresidencyForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await presidencies.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] PresidencyForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await presidencies.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangePresidencyAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await presidencies.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/categories")]
public sealed class CategoriesController(CategoryService categories, PublicationService publication) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IReadOnlyList<CategoryDto>> List(CancellationToken cancellationToken)
    {
        var items = await categories.ListPublicAsync(cancellationToken);
        return items.Select(item => new CategoryDto(item.Id, item.Name, item.Slug, item.Description)).ToList();
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] CategoryForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await categories.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] CategoryForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await categories.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangeCategoryAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/events")]
public sealed class EventsController(EventService events, QueryService queries, PublicationService publication) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResult<EventCardDto>> List([FromQuery] EventQuery query, CancellationToken cancellationToken) =>
        queries.EventsAsync(query, cancellationToken);

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventCardDto>> Get(string slug, CancellationToken cancellationToken)
    {
        var item = await queries.EventAsync(slug, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] EventForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await events.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] EventForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await events.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangeEventAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await events.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/laws")]
public sealed class LawsController(LawService laws, QueryService queries, PublicationService publication) : ApiController
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResult<LawCardDto>> List([FromQuery] LawQuery query, CancellationToken cancellationToken) =>
        queries.LawsAsync(query, cancellationToken);

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<LawCardDto>> Get(string slug, CancellationToken cancellationToken)
    {
        var law = await queries.LawAsync(slug, cancellationToken);
        return law is null ? NotFound() : Ok(law);
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Create([FromBody] LawForm form, CancellationToken cancellationToken)
    {
        form.Id = null;
        return Ok(await laws.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Editor")]
    public async Task<ActionResult<Guid>> Update(Guid id, [FromBody] LawForm form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return Ok(await laws.SaveAsync(form, Actor, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusForm form, CancellationToken cancellationToken)
    {
        await publication.ChangeLawAsync(id, form.Status, Actor, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await laws.DeleteAsync(id, Actor, cancellationToken);
        return NoContent();
    }
}
