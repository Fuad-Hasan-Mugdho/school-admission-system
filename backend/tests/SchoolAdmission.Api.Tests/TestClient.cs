using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Api.Tests;

/// <summary>Small typed wrapper over HttpClient mirroring the public contract.</summary>
public static class TestClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static long _studentSeed = DateTime.UtcNow.Ticks % 1_000_000_000L;

    /// <summary>A fresh, valid-looking 17-digit birth registration number.</summary>
    public static string NewStudentId()
    {
        var n = Interlocked.Increment(ref _studentSeed);
        return ("2015" + n.ToString("D13"))[..17];
    }

    public static HttpRequestMessage WithStudent(this HttpRequestMessage request, string? studentId)
    {
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }
        return request;
    }

    public static async Task<(HttpResponseMessage Response, JsonElement Body)> SendJson(
        this HttpClient client, HttpMethod method, string path, object? body = null, string? studentId = null)
    {
        var request = new HttpRequestMessage(method, path).WithStudent(studentId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var element = string.IsNullOrWhiteSpace(text)
            ? default
            : JsonDocument.Parse(text).RootElement.Clone();
        return (response, element);
    }

    public static async Task<string> CreateClass(this HttpClient client, int totalSeats, string? name = null)
    {
        var (response, body) = await client.SendJson(HttpMethod.Post, "/classes",
            new { name = name ?? $"Test Class {Guid.NewGuid():N}", totalSeats });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return body.GetProperty("id").GetString()!;
    }

    public static async Task<JsonElement> GetClass(this HttpClient client, string classId)
    {
        var (response, body) = await client.SendJson(HttpMethod.Get, $"/classes/{classId}");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    public static async Task<(HttpResponseMessage Response, JsonElement Body)> Hold(
        this HttpClient client, string classId, string studentId) =>
        await client.SendJson(HttpMethod.Post, $"/classes/{classId}/holds", studentId: studentId);

    public static async Task<string> HoldOk(this HttpClient client, string classId, string studentId)
    {
        var (response, body) = await client.Hold(classId, studentId);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return body.GetProperty("holdId").GetString()!;
    }

    public static async Task<(HttpResponseMessage Response, JsonElement Body)> Confirm(
        this HttpClient client, string holdId, string studentId) =>
        await client.SendJson(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId: studentId);

    public static async Task<(HttpResponseMessage Response, JsonElement Body)> Cancel(
        this HttpClient client, string holdId, string studentId) =>
        await client.SendJson(HttpMethod.Delete, $"/holds/{holdId}", studentId: studentId);

    public static async Task<JsonElement> GetHold(this HttpClient client, string holdId)
    {
        var (response, body) = await client.SendJson(HttpMethod.Get, $"/holds/{holdId}");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    public static int Int(this JsonElement e, string name) => e.GetProperty(name).GetInt32();
    public static string Str(this JsonElement e, string name) => e.GetProperty(name).GetString()!;
}
