using System.Net;
using System.Net.Http.Json;

namespace SchoolAdmission.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ClassTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(pg.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateClass_Returns201_WithContractFields_AndAppearsInList()
    {
        var name = $"Class 6 - Morning Shift {Guid.NewGuid():N}";
        var (response, body) = await _client.SendJson(HttpMethod.Post, "/classes", new { name, totalSeats = 120 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("c_", body.Str("id"));
        Assert.Equal(name, body.Str("name"));
        Assert.Equal(120, body.Int("totalSeats"));
        Assert.Equal($"/classes/{body.Str("id")}", response.Headers.Location?.ToString());

        var (listResponse, list) = await _client.SendJson(HttpMethod.Get, "/classes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var mine = list.EnumerateArray().Single(c => c.Str("id") == body.Str("id"));
        Assert.Equal(120, mine.Int("totalSeats"));
        Assert.Equal(0, mine.Int("heldSeats"));
        Assert.Equal(0, mine.Int("confirmedSeats"));
        Assert.Equal(120, mine.Int("availableSeats"));

        var single = await _client.GetClass(body.Str("id"));
        Assert.Equal(name, single.Str("name"));
        Assert.Equal(120, single.Int("availableSeats"));
    }

    [Theory]
    [InlineData("{\"name\":\"\",\"totalSeats\":10}")]
    [InlineData("{\"name\":\"Class 7\",\"totalSeats\":0}")]
    [InlineData("{\"name\":\"Class 7\",\"totalSeats\":-5}")]
    [InlineData("{\"name\":\"Class 7\"}")]
    [InlineData("{\"totalSeats\":10}")]
    [InlineData("{\"name\":\"Class 7\",\"totalSeats\":\"lots\"}")]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task CreateClass_InvalidInput_Returns400_WithJsonError(string rawBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/classes")
        {
            Content = new StringContent(rawBody, System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("VALIDATION_ERROR", error.Str("error"));
        Assert.False(string.IsNullOrWhiteSpace(error.Str("message")));
    }

    [Fact]
    public async Task GetClass_Unknown_Returns404()
    {
        var (response, body) = await _client.SendJson(HttpMethod.Get, "/classes/c_doesnotexist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CLASS_NOT_FOUND", body.Str("error"));
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
