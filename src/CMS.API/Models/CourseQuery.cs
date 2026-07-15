namespace CMS.API.Models;

/// <summary>課程 (Course) 查詢條件。</summary>
public class CourseQuery
{
    /// <summary>關鍵字：比對 Title、OfficialTitle、CourseId、ProdCourseId、FriendlyUrl</summary>
    public string? Keyword { get; set; }

    /// <summary>原廠外鍵 (精確比對)</summary>
    public short? PartnerPkid { get; set; }

    /// <summary>課程群組外鍵 (精確比對)</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態外鍵 (精確比對)</summary>
    public byte? PublishStatusPkid { get; set; }

    /// <summary>上架日期下限 (含)</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>上架日期上限 (含)</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>下架日期下限 (含)</summary>
    public DateOnly? ScheduleOffFrom { get; set; }

    /// <summary>下架日期上限 (含)</summary>
    public DateOnly? ScheduleOffTo { get; set; }

    /// <summary>允許重聽 (null = 不篩選)</summary>
    public bool? CanRepeat { get; set; }
}
