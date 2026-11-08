using System.Net;

namespace SchoolAdmission.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class HoldLifecycleTests(PostgresFixture pg) : IAsyncLifetime
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
    public async Task Hold_Returns201_WithContract_AndCountsMove()
    {
        var classId = await _client.CreateClass(3);
        var student = TestClient.NewStudentId();

        var (response, body) = await _client.Hold(classId, student);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("h_", body.Str("holdId"));
        Assert.Equal(classId, body.Str("classId"));
        Assert.Equal(student, body.Str("studentId"));
        Assert.Equal("HELD", body.Str("status"));
        var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange((expiresAt - DateTimeOffset.UtcNow).TotalSeconds, 590, 601);

        var cls = await _client.GetClass(classId);
        Assert.Equal(1, cls.Int("heldSeats"));
        Assert.Equal(0, cls.Int("confirmedSeats"));
        Assert.Equal(2, cls.Int("availableSeats"));

        var hold = await _client.GetHold(body.Str("holdId"));
        Assert.Equal("HELD", hold.Str("status"));
        Assert.Equal(classId, hold.Str("classId"));
    }

    [Fact]
    public async Task Hold_WhenClassFull_Returns409()
    {
        var classId = await _client.CreateClass(1);
        await _client.HoldOk(classId, TestClient.NewStudentId());

        var (response, body) = await _client.Hold(classId, TestClient.NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SEAT_UNAVAILABLE", body.Str("error"));
    }

    [Fact]
    public async Task Hold_SameStudentTwiceInSameClass_Returns409_ButOtherClassIsFine()
    {
        var classA = await _client.CreateClass(10);
        var classB = await _client.CreateClass(10);
        var student = TestClient.NewStudentId();

        var holdId = await _client.HoldOk(classA, student);
        var (dup, dupBody) = await _client.Hold(classA, student);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("ALREADY_APPLIED", dupBody.Str("error"));

        // Still blocked after the admission is confirmed.
        await _client.Confirm(holdId, student);
        var (dup2, _) = await _client.Hold(classA, student);
        Assert.Equal(HttpStatusCode.Conflict, dup2.StatusCode);

        // A different class is a separate application.
        var (other, _) = await _client.Hold(classB, student);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        var cls = await _client.GetClass(classA);
        Assert.Equal(1, cls.Int("confirmedSeats"));
        Assert.Equal(0, cls.Int("heldSeats"));
    }

    [Fact]
    public async Task Hold_MissingOrInvalidStudentHeader_Returns400()
    {
        var classId = await _client.CreateClass(5);

        var (missing, missingBody) = await _client.Hold(classId, studentId: null!);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("STUDENT_ID_REQUIRED", missingBody.Str("error"));

        var (tooShort, _) = await _client.Hold(classId, "12345");
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        var (letters, _) = await _client.Hold(classId, "2015269123456789A");
        Assert.Equal(HttpStatusCode.BadRequest, letters.StatusCode);

        var (me, _) = await _client.SendJson(HttpMethod.Get, "/me/holds");
        Assert.Equal(HttpStatusCode.BadRequest, me.StatusCode);
    }

    [Fact]
    public async Task Hold_UnknownClass_Returns404()
    {
        var (response, body) = await _client.Hold("c_nope", TestClient.NewStudentId());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CLASS_NOT_FOUND", body.Str("error"));
    }

    [Fact]
    public async Task Confirm_MovesSeatFromHeldToConfirmed_AndIsIdempotent()
    {
        var classId = await _client.CreateClass(5);
        var student = TestClient.NewStudentId();
        var holdId = await _client.HoldOk(classId, student);

        var (first, firstBody) = await _client.Confirm(holdId, student);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(holdId, firstBody.Str("holdId"));
        Assert.Equal("CONFIRMED", firstBody.Str("status"));

        // Confirm again (double click, retry, duplicate request): accepted, no double counting.
        var (second, secondBody) = await _client.Confirm(holdId, student);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("CONFIRMED", secondBody.Str("status"));

        // ...even when the repeats arrive simultaneously.
        var parallel = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => _client.Confirm(holdId, student)));
        Assert.All(parallel, r => Assert.Equal(HttpStatusCode.OK, r.Response.StatusCode));

        var cls = await _client.GetClass(classId);
        Assert.Equal(1, cls.Int("confirmedSeats"));
        Assert.Equal(0, cls.Int("heldSeats"));
        Assert.Equal(4, cls.Int("availableSeats"));

        var hold = await _client.GetHold(holdId);
        Assert.Equal("CONFIRMED", hold.Str("status"));
    }

    [Fact]
    public async Task Confirm_ByAnotherStudent_Returns403_AndUnknownHold_Returns404()
    {
        var classId = await _client.CreateClass(5);
        var owner = TestClient.NewStudentId();
        var intruder = TestClient.NewStudentId();
        var holdId = await _client.HoldOk(classId, owner);

        var (forbidden, forbiddenBody) = await _client.Confirm(holdId, intruder);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("NOT_OWNER", forbiddenBody.Str("error"));

        var (cancelForbidden, _) = await _client.Cancel(holdId, intruder);
        Assert.Equal(HttpStatusCode.Forbidden, cancelForbidden.StatusCode);

        var (notFound, notFoundBody) = await _client.Confirm("h_missing", owner);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("HOLD_NOT_FOUND", notFoundBody.Str("error"));

        var (getMissing, _) = await _client.SendJson(HttpMethod.Get, "/holds/h_missing");
        Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);

        // The hold is untouched.
        Assert.Equal("HELD", (await _client.GetHold(holdId)).Str("status"));
    }

    [Fact]
    public async Task Cancel_ReleasesSeat_IsIdempotent_AndCannotCancelConfirmed()
    {
        var classId = await _client.CreateClass(1);
        var student = TestClient.NewStudentId();
        var holdId = await _client.HoldOk(classId, student);
        Assert.Equal(0, (await _client.GetClass(classId)).Int("availableSeats"));

        var (cancel, _) = await _client.Cancel(holdId, student);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal("CANCELLED", (await _client.GetHold(holdId)).Str("status"));
        Assert.Equal(1, (await _client.GetClass(classId)).Int("availableSeats"));

        var (again, _) = await _client.Cancel(holdId, student);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        // The cancelled hold cannot be confirmed any more.
        var (confirm, confirmBody) = await _client.Confirm(holdId, student);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("HOLD_CANCELLED", confirmBody.Str("error"));

        // The student may apply again, and the freed seat can be taken.
        var holdId2 = await _client.HoldOk(classId, student);
        await _client.Confirm(holdId2, student);
        var (cancelConfirmed, cancelConfirmedBody) = await _client.Cancel(holdId2, student);
        Assert.Equal(HttpStatusCode.Conflict, cancelConfirmed.StatusCode);
        Assert.Equal("HOLD_CONFIRMED", cancelConfirmedBody.Str("error"));

        var (unknown, _) = await _client.Cancel("h_missing", student);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task MyHolds_ReturnsOnlyOwnHolds_WithClassName()
    {
        var className = $"Class 8 - Day Shift {Guid.NewGuid():N}";
        var classId = await _client.CreateClass(5, className);
        var me = TestClient.NewStudentId();
        var someoneElse = TestClient.NewStudentId();

        var mine = await _client.HoldOk(classId, me);
        await _client.HoldOk(classId, someoneElse);

        var (response, body) = await _client.SendJson(HttpMethod.Get, "/me/holds", studentId: me);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = body.EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal(mine, item.Str("holdId"));
        Assert.Equal(classId, item.Str("classId"));
        Assert.Equal(className, item.Str("className"));
        Assert.Equal("HELD", item.Str("status"));
        Assert.True(item.TryGetProperty("expiresAt", out _));
    }
}
