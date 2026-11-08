using System.Net;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// The core guarantee: however many requests arrive at once, held + confirmed never exceeds totalSeats.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConcurrencyTests(PostgresFixture pg)
{
    [Fact]
    public async Task ManyParallelHolds_NeverOversellSeats()
    {
        const int totalSeats = 7;
        const int applicants = 120;

        await using var factory = new ApiFactory(pg.ConnectionString);
        using var client = factory.CreateClient();
        var classId = await client.CreateClass(totalSeats);

        var students = Enumerable.Range(0, applicants).Select(_ => TestClient.NewStudentId()).ToList();

        // Start every request at the same moment.
        using var gate = new ManualResetEventSlim(false);
        var tasks = students.Select(async s =>
        {
            gate.Wait();
            return await client.Hold(classId, s);
        }).ToList();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        var created = results.Count(r => r.Response.StatusCode == HttpStatusCode.Created);
        var conflicts = results.Count(r => r.Response.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(totalSeats, created);
        Assert.Equal(applicants - totalSeats, conflicts);
        Assert.All(results.Where(r => r.Response.StatusCode == HttpStatusCode.Conflict),
            r => Assert.Equal("SEAT_UNAVAILABLE", r.Body.Str("error")));

        var cls = await client.GetClass(classId);
        Assert.Equal(totalSeats, cls.Int("heldSeats"));
        Assert.Equal(0, cls.Int("confirmedSeats"));
        Assert.Equal(0, cls.Int("availableSeats"));
    }

    [Fact]
    public async Task SameStudent_ParallelHolds_OnlyOneSucceeds()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        using var client = factory.CreateClient();
        var classId = await client.CreateClass(50);
        var student = TestClient.NewStudentId();

        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => client.Hold(classId, student)));

        Assert.Equal(1, results.Count(r => r.Response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(29, results.Count(r => r.Response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, (await client.GetClass(classId)).Int("heldSeats"));
    }

    [Fact]
    public async Task MixedHoldCancelConfirmStorm_KeepsInvariant()
    {
        const int totalSeats = 5;
        await using var factory = new ApiFactory(pg.ConnectionString);
        using var client = factory.CreateClient();
        var classId = await client.CreateClass(totalSeats);

        // Round 1: 40 applicants race for 5 seats.
        var round1 = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ =>
        {
            var s = TestClient.NewStudentId();
            return client.Hold(classId, s).ContinueWith(t => (Student: s, t.Result.Response, t.Result.Body));
        }));
        var winners = round1.Where(r => r.Response.StatusCode == HttpStatusCode.Created)
            .Select(r => (r.Student, HoldId: r.Body.Str("holdId"))).ToList();
        Assert.Equal(totalSeats, winners.Count);

        // Round 2: half the winners cancel, half confirm, while 40 new applicants hammer the class.
        var actions = new List<Task>();
        for (var i = 0; i < winners.Count; i++)
        {
            var w = winners[i];
            actions.Add(i % 2 == 0 ? client.Cancel(w.HoldId, w.Student) : client.Confirm(w.HoldId, w.Student));
        }
        var round2 = Enumerable.Range(0, 40).Select(_ => client.Hold(classId, TestClient.NewStudentId())).ToList();
        await Task.WhenAll(actions.Concat(round2));

        var cls = await client.GetClass(classId);
        Assert.True(cls.Int("heldSeats") + cls.Int("confirmedSeats") <= totalSeats,
            $"held={cls.Int("heldSeats")} confirmed={cls.Int("confirmedSeats")} total={totalSeats}");
        Assert.Equal(totalSeats - cls.Int("heldSeats") - cls.Int("confirmedSeats"), cls.Int("availableSeats"));
        // Cancelled seats (3 of 5) were re-taken by the storm, so the class is full again.
        Assert.Equal(totalSeats, cls.Int("heldSeats") + cls.Int("confirmedSeats"));
        Assert.Equal(2, cls.Int("confirmedSeats"));
    }

    /// <summary>
    /// Bonus: two independent API processes sharing one PostgreSQL database. The row lock lives in
    /// the database, not in process memory, so the invariant holds across processes too.
    /// </summary>
    [Fact]
    public async Task TwoBackendInstances_SharingOneDatabase_NeverOversell()
    {
        const int totalSeats = 6;
        const int applicantsPerInstance = 50;

        await using var instanceA = new ApiFactory(pg.ConnectionString);
        await using var instanceB = new ApiFactory(pg.ConnectionString);
        using var clientA = instanceA.CreateClient();
        using var clientB = instanceB.CreateClient();

        var classId = await clientA.CreateClass(totalSeats);
        Assert.Equal(totalSeats, (await clientB.GetClass(classId)).Int("availableSeats"));

        using var gate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, applicantsPerInstance * 2).Select(async i =>
        {
            var client = i % 2 == 0 ? clientA : clientB;
            gate.Wait();
            return await client.Hold(classId, TestClient.NewStudentId());
        }).ToList();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(totalSeats, results.Count(r => r.Response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(applicantsPerInstance * 2 - totalSeats,
            results.Count(r => r.Response.StatusCode == HttpStatusCode.Conflict));

        var fromA = await clientA.GetClass(classId);
        var fromB = await clientB.GetClass(classId);
        Assert.Equal(totalSeats, fromA.Int("heldSeats"));
        Assert.Equal(totalSeats, fromB.Int("heldSeats"));
        Assert.Equal(0, fromB.Int("availableSeats"));
    }
}
