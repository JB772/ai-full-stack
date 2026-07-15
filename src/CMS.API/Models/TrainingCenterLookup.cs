namespace CMS.API.Models;

/// <summary>訓練中心下拉／分頁用精簡模型 (只帶顯示與排序所需欄位)。</summary>
public class TrainingCenterLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
