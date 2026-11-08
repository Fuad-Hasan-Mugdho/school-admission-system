using System.Net;

namespace SchoolAdmission.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ExpiryTests(PostgresFixture pg)
{
    [Fact]
    public async Task Hold_ExpiresAutomatically_SeatReturns_AndCannotBeConfirmed()
    {
        // 1-second holds, sweeper every 250 ms.
        await using var factory = new ApiFactory(pg.ConnectionString, holdDurationSeconds: 1, sweepIntervalSeconds: 0.25);
        using var client = factory.CreateClient();

        var classId = await client.CreateClass(1);
        var student = TestClient.NewStudentId();
        var holdId = await client.HoldOk(classId, student);

        Assert.Equal("HELD", (await client.GetHold(holdId)).Str("status"));
        Assert.Equal(0, (await client.GetClass(classId)).Int("availableSeats"));

        // Nobody calls any "expire" endpoint - time simply passes.
        await Task.Delay(TimeSpan.FromSeconds(1.6));

        Assert.Equal("EXPIRED", (await client.GetHold(holdId)).Str("status"));
        var cls = await client.GetClass(classId);
        Assert.Equal(0, cls.Int("heldSeats"));
        Assert.Equal(1, cls.Int("availableSeats"));

        var (confirm, body) = await client.Confirm(holdId, student);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("HOLD_EXPIRED", body.Str("error"));

        var (me, meBody) = await client.SendJson(HttpMethod.Get, "/me/holds", studentId: student);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("EXPIRED", meBody.EnumerateArray().Single(h => h.Str("holdId") == holdId).Str("status"));

        // The seat is free again: the same student can re-apply and another student can take it.
        var holdId2 = await client.HoldOk(classId, student);
        Assert.NotEqual(holdId, holdId2);
    }

    [Fact]
    public async Task ExpiredHold_IsReportedExpired_EvenBeforeSweeperRuns()
    {
        // Sweeper effectively disabled (runs every hour) - reads must still compute expiry.
        await using var factory = new ApiFactory(pg.ConnectionString, holdDurationSeconds: 1, sweepIntervalSeconds: 3600);
        using var client = factory.CreateClient();

        var classId = await client.CreateClass(1);
        var student = TestClient.NewStudentId();
        var holdId = await client.HoldOk(classId, student);

        await Task.Delay(TimeSpan.FromSeconds(1.3));

        Assert.Equal("EXPIRED", (await client.GetHold(holdId)).Str("status"));
        Assert.Equal(1, (await client.GetClass(classId)).Int("availableSeats"));

        // And a new applicant gets the seat.
        var (response, _) = await client.Hold(classId, TestClient.NewStudentId());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
