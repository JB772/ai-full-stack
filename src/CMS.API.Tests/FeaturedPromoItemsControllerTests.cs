using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/featured-promo-items. A fresh factory (and therefore a fresh in-memory
/// repository) is created per test. The seed is one week (2026-03-16 Mon .. 2026-03-22 Sun): centre 1
/// (台北) has Mon slots 1/2/3 and Tue slots 1/2; centre 2 (新竹) has a single Mon slot 1.
///
/// What distinguishes this table: two NOT-NULL outbound FKs (so responses carry TrainingCenter /
/// Promotion nav objects); a (ScheduleOn, TrainingCenter, Slot) UNIQUE constraint surfaced as a 409;
/// the scheduler's one-week ScheduleOn range + TrainingCenter tab filter; and a slot up/down move that
/// swaps with the adjacent row.
/// </summary>
public class FeaturedPromoItemsControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public FeaturedPromoItemsControllerTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly DateOnly WeekMonday = new(2026, 3, 16);
    private static readonly DateOnly WeekSunday = new(2026, 3, 22);

    private static FeaturedPromoItemRequest ValidRequest() => new()
    {
        ScheduleOn = new DateOnly(2026, 3, 18), // Wednesday — empty in the seed
        TrainingCenterPkid = 1,
        Slot = 1,
        PromotionPkid = 10,
        Topic = "新標題",
        Description = "新說明"
    };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededItems_WithFkNavObjects()
    {
        var items = await _client.GetFromJsonAsync<List<FeaturedPromoItem>>("/api/featured-promo-items");

        Assert.NotNull(items);
        Assert.Equal(6, items!.Count);

        var mondaySlot1 = items.Single(i => i.Pkid == 1);
        Assert.Equal("台北", mondaySlot1.TrainingCenter!.Name);
        Assert.Equal("20251204_SkillTrainAI", mondaySlot1.Promotion!.PromoCode);
    }

    // ---------- One-week ScheduleOn filter ----------

    [Fact]
    public async Task Query_ByOneWeekScheduleOnRange_ReturnsOnlyThatWeek()
    {
        // The whole seed happens to fall inside 3/16–3/22, so add a row outside the week and prove it is excluded.
        await _client.PostAsJsonAsync("/api/featured-promo-items", new FeaturedPromoItemRequest
        {
            ScheduleOn = new DateOnly(2026, 3, 25), // next week
            TrainingCenterPkid = 1, Slot = 1, PromotionPkid = 10, Topic = "下週", Description = "下週說明"
        });

        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/query", new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            ScheduleOnFrom = WeekMonday,
            ScheduleOnTo = WeekSunday
        });

        var items = await response.Content.ReadFromJsonAsync<List<FeaturedPromoItem>>();

        Assert.Equal(5, items!.Count); // centre-1 Mon 1/2/3 + Tue 1/2, next-week row excluded
        Assert.All(items, i => Assert.InRange(i.ScheduleOn, WeekMonday, WeekSunday));
        Assert.All(items, i => Assert.Equal((short)1, i.TrainingCenterPkid));
    }

    [Fact]
    public async Task Query_ScheduleOnRange_IsInclusiveOfBothEnds()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/query", new FeaturedPromoItemQuery
        {
            ScheduleOnFrom = WeekMonday,
            ScheduleOnTo = WeekMonday // just Monday
        });

        var items = await response.Content.ReadFromJsonAsync<List<FeaturedPromoItem>>();

        Assert.All(items!, i => Assert.Equal(WeekMonday, i.ScheduleOn));
        Assert.Equal(4, items!.Count); // centre-1 Mon 1/2/3 + centre-2 Mon 1
    }

    // ---------- TrainingCenter filter ----------

    [Fact]
    public async Task Query_ByTrainingCenter_IsolatesThatCentre()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/query",
            new FeaturedPromoItemQuery { TrainingCenterPkid = 2 });

        var items = await response.Content.ReadFromJsonAsync<List<FeaturedPromoItem>>();

        var only = Assert.Single(items!);
        Assert.Equal(6, only.Pkid);
        Assert.Equal("新竹", only.TrainingCenter!.Name);
    }

    [Fact]
    public async Task Query_ByTrainingCenterAndSlot_Combine()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/query",
            new FeaturedPromoItemQuery { TrainingCenterPkid = 1, Slot = 3 });

        var items = await response.Content.ReadFromJsonAsync<List<FeaturedPromoItem>>();

        var only = Assert.Single(items!);
        Assert.Equal(3, only.Pkid); // only Mon slot 3 exists for centre 1 (pkid 3)
    }

    [Fact]
    public async Task Query_ByKeyword_MatchesPromoCode()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/query",
            new FeaturedPromoItemQuery { Keyword = "GoogleAI" });

        var items = await response.Content.ReadFromJsonAsync<List<FeaturedPromoItem>>();

        Assert.NotEmpty(items!);
        Assert.All(items!, i => Assert.Equal("251211_GoogleAI", i.Promotion!.PromoCode));
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_Existing_ReturnsItem()
    {
        var item = await _client.GetFromJsonAsync<FeaturedPromoItem>("/api/featured-promo-items/2");

        Assert.NotNull(item);
        Assert.Equal((byte)2, item!.Slot);
        Assert.Equal("251211_GoogleAI", item.Promotion!.PromoCode);
    }

    [Fact]
    public async Task GetByPkid_Missing_Returns404()
    {
        var response = await _client.GetAsync("/api/featured-promo-items/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidItem_Returns201WithGeneratedPkidAndNav()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<FeaturedPromoItem>();
        Assert.Equal(7, created!.Pkid); // follows the six seeded rows
        Assert.Equal("台北", created.TrainingCenter!.Name);
        Assert.Equal("20251204_SkillTrainAI", created.Promotion!.PromoCode);
    }

    [Fact]
    public async Task Create_IntoAnOccupiedSlot_Returns409()
    {
        var request = ValidRequest();
        request.ScheduleOn = WeekMonday; // Mon slot 1 for centre 1 already taken (pkid 1)

        var response = await _client.PostAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("已有排程", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_SlotOutOfRange_Returns400()
    {
        var request = ValidRequest();
        request.Slot = 4; // valid range is 1-3

        var response = await _client.PostAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingTopic_Returns400()
    {
        var request = ValidRequest();
        request.Topic = "";

        var response = await _client.PostAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_Existing_Returns204AndPersists()
    {
        var request = ValidRequest();
        request.Pkid = 2;
        request.ScheduleOn = WeekMonday;
        request.Slot = 2; // its own slot — not a conflict
        request.Topic = "改過的標題";

        var response = await _client.PutAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<FeaturedPromoItem>("/api/featured-promo-items/2");
        Assert.Equal("改過的標題", updated!.Topic);
    }

    [Fact]
    public async Task Update_IntoAnotherRowsSlot_Returns409()
    {
        var request = ValidRequest();
        request.Pkid = 2;          // Mon slot 2
        request.ScheduleOn = WeekMonday;
        request.Slot = 1;          // Mon slot 1 belongs to pkid 1

        var response = await _client.PutAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400()
    {
        var request = ValidRequest();
        request.Pkid = 0;

        var response = await _client.PutAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Missing_Returns404()
    {
        var request = ValidRequest();
        request.Pkid = 999;

        var response = await _client.PutAsJsonAsync("/api/featured-promo-items", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_Existing_Returns204()
    {
        var response = await _client.DeleteAsync("/api/featured-promo-items/6");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/featured-promo-items/6")).StatusCode);
    }

    [Fact]
    public async Task Delete_Missing_Returns404()
    {
        var response = await _client.DeleteAsync("/api/featured-promo-items/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Move (slot up/down) ----------

    [Fact]
    public async Task Move_Down_IntoOccupiedSlot_SwapsTheTwoRows()
    {
        // Mon centre-1: slot 1 = pkid 1, slot 2 = pkid 2. Move pkid 1 down → the two swap.
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/1/move",
            new FeaturedPromoItemMoveRequest { Direction = "down" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var one = await _client.GetFromJsonAsync<FeaturedPromoItem>("/api/featured-promo-items/1");
        var two = await _client.GetFromJsonAsync<FeaturedPromoItem>("/api/featured-promo-items/2");
        Assert.Equal((byte)2, one!.Slot);
        Assert.Equal((byte)1, two!.Slot);
    }

    [Fact]
    public async Task Move_Down_IntoEmptySlot_JustMoves()
    {
        // Tue centre-1: slot 2 = pkid 5, slot 3 empty. Move pkid 5 down → slot 3, nothing to swap.
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/5/move",
            new FeaturedPromoItemMoveRequest { Direction = "down" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var five = await _client.GetFromJsonAsync<FeaturedPromoItem>("/api/featured-promo-items/5");
        Assert.Equal((byte)3, five!.Slot);
    }

    [Fact]
    public async Task Move_Up_FromSlot1_Returns409OutOfRange()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/1/move",
            new FeaturedPromoItemMoveRequest { Direction = "up" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("邊界", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Move_Missing_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/999/move",
            new FeaturedPromoItemMoveRequest { Direction = "down" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Move_InvalidDirection_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/featured-promo-items/1/move",
            new FeaturedPromoItemMoveRequest { Direction = "sideways" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
