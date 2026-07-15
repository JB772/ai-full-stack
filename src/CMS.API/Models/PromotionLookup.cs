namespace CMS.API.Models;

/// <summary>
/// 促銷 (Promotion2) 下拉／查詢用精簡模型。上稿編輯表單輸入 PromoCode 後，前端以此清單解析出 Promotion_pkid，
/// 並可帶出 Topic／Description 供參考。
/// </summary>
public class PromotionLookup
{
    public int Pkid { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
