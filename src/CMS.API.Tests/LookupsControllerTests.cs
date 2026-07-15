using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for the two lookups the FeaturedPromoItem scheduler depends on: the training-centre
/// tabs and the PromoCode → Promotion_pkid resolution list.
/// </summary>
public class LookupsControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public LookupsControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetTrainingCenters_ReturnsCentresInDisplayOrder()
    {
        var centers = await _client.GetFromJsonAsync<List<TrainingCenterLookup>>("/api/lookups/training-centers");

        Assert.NotNull(centers);
        Assert.Equal(["台北", "新竹", "台中"], centers!.Select(c => c.Name));
    }

    [Fact]
    public async Task GetPromoCodes_ReturnsPromotionsForLookup()
    {
        var promos = await _client.GetFromJsonAsync<List<PromotionLookup>>("/api/lookups/promo-codes");

        Assert.NotNull(promos);
        Assert.Equal(3, promos!.Count);

        // The scheduler resolves an entered PromoCode to its pkid — the pair must round-trip.
        var google = promos.Single(p => p.PromoCode == "251211_GoogleAI");
        Assert.Equal(20, google.Pkid);
        Assert.Equal("Google AI工具一次掌握", google.Topic);
    }
}
