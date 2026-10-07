using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Presidents.IntegrationTests;

public sealed class PublicApiTests(ApiFactory fixture) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_and_public_pages_are_available()
    {
        var client = fixture.Factory.CreateClient();
        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home", Json);
        Assert.True(home.GetProperty("counts").GetProperty("presidents").GetInt32() >= 3);
        Assert.True(home.GetProperty("counts").GetProperty("categories").GetInt32() >= 25);

        var president = await client.GetFromJsonAsync<JsonElement>("/api/v1/public/presidents/juscelino-kubitschek", Json);
        Assert.Equal("juscelino-kubitschek", president.GetProperty("slug").GetString());
        Assert.True(president.GetProperty("demonstration").GetBoolean());
        Assert.Contains("31/01/1956", president.GetProperty("mandate").GetString());
    }

    [Fact]
    public async Task Law_search_does_not_treat_publication_as_authorship()
    {
        var client = fixture.Factory.CreateClient();
        var search = await client.GetFromJsonAsync<JsonElement>("/api/v1/search?q=Lei%208.080", Json);
        Assert.Contains(search.GetProperty("laws").EnumerateArray(), law => law.GetProperty("url").GetString() == "/leis/lei-8080-1990");

        var law = await client.GetFromJsonAsync<JsonElement>("/api/v1/laws/lei-8080-1990", Json);
        Assert.False(law.GetProperty("attributedAsAuthor").GetBoolean());
        Assert.True(law.GetProperty("presidentInOffice").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task Comparison_has_no_ranking_and_keeps_separate_mandates()
    {
        var client = fixture.Factory.CreateClient();
        var raw = await client.GetStringAsync("/api/v1/compare?slug=getulio-vargas&slug=juscelino-kubitschek&slug=fernando-henrique-cardoso");
        Assert.DoesNotContain("\"score\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"rank\"", raw, StringComparison.OrdinalIgnoreCase);

        var comparison = JsonSerializer.Deserialize<JsonElement>(raw, Json);
        Assert.Contains("não produz ranking", comparison.GetProperty("disclaimer").GetString());
        Assert.Equal(3, comparison.GetProperty("columns").GetArrayLength());
        var vargas = comparison.GetProperty("columns").EnumerateArray().Single(column => column.GetProperty("slug").GetString() == "getulio-vargas");
        Assert.Equal(0, vargas.GetProperty("lawCount").GetInt32());
        Assert.Contains("·", vargas.GetProperty("mandate").GetString());
    }

    [Fact]
    public async Task Answers_refuse_missing_evidence_and_cite_stored_statements()
    {
        var client = fixture.Factory.CreateClient();
        var missing = await client.PostAsJsonAsync("/api/v1/answers", new { question = "Quando Brasília foi fundada?" });
        var missingBody = await missing.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(missingBody.GetProperty("insufficientEvidence").GetBoolean());
        Assert.Equal("Não encontrei fontes suficientes no banco para afirmar isso.", missingBody.GetProperty("text").GetString());

        var grounded = await client.PostAsJsonAsync("/api/v1/answers", new { question = "Plano de Metas" });
        var groundedBody = await grounded.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(groundedBody.GetProperty("insufficientEvidence").GetBoolean());
        Assert.Contains("Arquivo Nacional", groundedBody.GetProperty("text").GetString());
        Assert.Contains(groundedBody.GetProperty("citations").EnumerateArray(), citation =>
            (citation.GetProperty("url").GetString() ?? "").Contains("http", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Authentication_protects_admin_and_blocks_unsourced_publication()
    {
        var client = fixture.Factory.CreateClient();
        var anonymous = await client.GetAsync("/api/v1/admin/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/api/v1/presidents", new { name = "Rascunho", fullName = "Presidente Rascunho de Teste" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = await created.Content.ReadFromJsonAsync<Guid>();

        var hidden = await client.GetAsync("/api/v1/public/presidents/presidente-rascunho-de-teste");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var publish = await client.PostAsJsonAsync($"/api/v1/presidents/{id}/status", new { status = "Published" });
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);

        var removed = await client.DeleteAsync($"/api/v1/presidents/{id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }
}
