using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using TimeBlock.Core.Contracts;

namespace TimeBlock.Tests;

public class TimeBlockFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAlive = new("DataSource=file:timeblockmem?mode=memory&cache=shared");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", "DataSource=file:timeblockmem?mode=memory&cache=shared");
        builder.UseSetting("Worker:Enabled", "false");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keepAlive.Dispose();
        }

        base.Dispose(disposing);
    }
}

public class ApiIntegrationTests : IClassFixture<TimeBlockFactory>
{
    private readonly TimeBlockFactory _factory;

    public ApiIntegrationTests(TimeBlockFactory factory)
    {
        _factory = factory;
    }

    private static int _counter;

    private async Task<HttpClient> CreateUserAsync()
    {
        var email = $"user{Interlocked.Increment(ref _counter)}{Guid.NewGuid():N}@test.dev";
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Tester", "password123", null));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiClient.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.Token);
        return client;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static DateOnly MondayOfCurrentWeek => TimeBlock.Core.Recurrence.RecurrenceEngine.StartOfWeek(Today);

    [Fact]
    public async Task RegisterAndMeReturnsUser()
    {
        var client = await CreateUserAsync();

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", ApiClient.JsonOptions);

        Assert.NotNull(me);
        Assert.Equal("Tester", me!.DisplayName);
        Assert.Equal(8, me.DailyCapacityHours);
    }

    [Fact]
    public async Task RegisterRejectsDuplicateEmail()
    {
        var email = $"dup{Guid.NewGuid():N}@test.dev";
        var first = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "A", "password123", null));
        var second = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "B", "password123", null));

        Assert.True(first.IsSuccessStatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GroupJoinAndSharedTimeline()
    {
        var alice = await CreateUserAsync();
        var bob = await CreateUserAsync();

        var createResponse = await alice.PostAsJsonAsync("/api/groups", new CreateGroupRequest("Study crew"));
        createResponse.EnsureSuccessStatusCode();
        var group = await createResponse.Content.ReadFromJsonAsync<GroupDto>(ApiClient.JsonOptions);

        var joinResponse = await bob.PostAsJsonAsync("/api/groups/join", new JoinGroupRequest(group!.InviteCode));
        Assert.True(joinResponse.IsSuccessStatusCode);

        var start = new DateTime(2030, 3, 5, 19, 0, 0, DateTimeKind.Utc);
        var blockResponse = await alice.PostAsJsonAsync("/api/blocks", new CreateBlockRequest(
            "Study slot", "study", BlockPrivacy.Details, start, start.AddHours(2), group.Id));
        blockResponse.EnsureSuccessStatusCode();

        var timeline = await bob.GetFromJsonAsync<GroupTimelineDto>(
            $"/api/groups/{group.Id}/timeline?weekStart=2030-03-04", ApiClient.JsonOptions);

        Assert.NotNull(timeline);
        Assert.Equal(2, timeline!.Group.Members.Count);
        var entry = Assert.Single(timeline.Blocks);
        Assert.Equal("Study slot", entry.Title);
        Assert.Equal("Tester", entry.OwnerName);
    }

    [Fact]
    public async Task BusyOnlyHidesTitleFromOthers()
    {
        var alice = await CreateUserAsync();
        var bob = await CreateUserAsync();

        var createResponse = await alice.PostAsJsonAsync("/api/groups", new CreateGroupRequest("Gym squad"));
        var group = await createResponse.Content.ReadFromJsonAsync<GroupDto>(ApiClient.JsonOptions);
        await bob.PostAsJsonAsync("/api/groups/join", new JoinGroupRequest(group!.InviteCode));

        var start = new DateTime(2030, 3, 6, 6, 0, 0, DateTimeKind.Utc);
        await alice.PostAsJsonAsync("/api/blocks", new CreateBlockRequest(
            "Gym", "gym", BlockPrivacy.BusyOnly, start, start.AddHours(1), group.Id));

        var timeline = await bob.GetFromJsonAsync<GroupTimelineDto>(
            $"/api/groups/{group.Id}/timeline?weekStart=2030-03-04", ApiClient.JsonOptions);
        Assert.Equal("Busy", Assert.Single(timeline!.Blocks).Title);

        var own = await alice.GetFromJsonAsync<GroupTimelineDto>(
            $"/api/groups/{group.Id}/timeline?weekStart=2030-03-04", ApiClient.JsonOptions);
        Assert.Equal("Gym", Assert.Single(own!.Blocks).Title);
    }

    [Fact]
    public async Task ConfirmStatusFeedsFollowThrough()
    {
        var client = await CreateUserAsync();

        var start = MondayOfCurrentWeek.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var created = await client.PostAsJsonAsync("/api/blocks", new CreateBlockRequest(
            "Deep work", "work", BlockPrivacy.Details, start, start.AddHours(3), null));
        var block = await created.Content.ReadFromJsonAsync<BlockDto>(ApiClient.JsonOptions);

        var patch = await client.PatchAsJsonAsync($"/api/blocks/{block!.Id}/status", new UpdateBlockStatusRequest(BlockStatus.Done));
        patch.EnsureSuccessStatusCode();

        var stats = await client.GetFromJsonAsync<List<WeekFollowThroughDto>>(
            "/api/stats/follow-through?weeks=4", ApiClient.JsonOptions);

        Assert.Contains(stats!, w => w.Done == 1 && Math.Abs(w.Rate - 1.0) < 0.001);
    }

    [Fact]
    public async Task OverloadFlagsStackedDays()
    {
        var client = await CreateUserAsync();
        await client.PutAsJsonAsync("/api/me", new UpdateSettingsRequest(6, false));

        var day = new DateTime(2030, 3, 4);
        await client.PostAsJsonAsync("/api/blocks", new CreateBlockRequest("Morning", "work", BlockPrivacy.Details, day.AddHours(8), day.AddHours(12), null));
        await client.PostAsJsonAsync("/api/blocks", new CreateBlockRequest("Afternoon", "work", BlockPrivacy.Details, day.AddHours(13), day.AddHours(18), null));

        var loads = await client.GetFromJsonAsync<List<DayLoad>>(
            "/api/blocks/overload?from=2030-03-04&to=2030-03-10", ApiClient.JsonOptions);

        var monday = Assert.Single(loads!, l => l.Date == new DateOnly(2030, 3, 4));
        Assert.True(monday.Overloaded);
        Assert.Equal(9, monday.PlannedHours);
        Assert.Equal(6, monday.CapacityHours);
    }

    [Fact]
    public async Task NonMemberCannotSeeGroupTimeline()
    {
        var alice = await CreateUserAsync();
        var bob = await CreateUserAsync();

        var createResponse = await alice.PostAsJsonAsync("/api/groups", new CreateGroupRequest("Private"));
        var group = await createResponse.Content.ReadFromJsonAsync<GroupDto>(ApiClient.JsonOptions);

        var response = await bob.GetAsync($"/api/groups/{group!.Id}/timeline");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TemplatesMaterializeOccurrences()
    {
        var client = await CreateUserAsync();

        var create = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest(
            "Evening study", "study", BlockPrivacy.Details, null,
            "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR", new TimeOnly(20, 0), 120,
            Today, Today.AddDays(6), "UTC"));
        create.EnsureSuccessStatusCode();

        var blocks = await client.GetFromJsonAsync<List<BlockDto>>(
            $"/api/blocks?from={Today:yyyy-MM-dd}&to={Today.AddDays(7):yyyy-MM-dd}", ApiClient.JsonOptions);

        Assert.Equal(5, blocks!.Count);
        Assert.All(blocks, b => Assert.Equal("Evening study", b.Title));
        Assert.All(blocks, b => Assert.Equal(new TimeOnly(20, 0), TimeOnly.FromDateTime(b.StartUtc)));
    }
}
