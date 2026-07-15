using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Slot 上／下移動請求。對應 UI 上的「+」(往下, 1→2) 與「-」(往上, 2→1)。</summary>
public class FeaturedPromoItemMoveRequest
{
    /// <summary>方向：<c>down</c> = Slot +1、<c>up</c> = Slot -1。</summary>
    [Required]
    [RegularExpression("up|down", ErrorMessage = "方向必須為 up 或 down。")]
    public string Direction { get; set; } = string.Empty;
}

/// <summary>MoveAsync 的結果：找不到資料列、已達邊界 (無法再移)、或成功移動 (含與相鄰列互換)。</summary>
public enum MoveResult
{
    NotFound,
    OutOfRange,
    Moved
}
