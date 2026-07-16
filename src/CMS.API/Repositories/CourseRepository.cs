using System.Data;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseRepository(IDbConnectionFactory connectionFactory, RowAuditWriter auditWriter) : ICourseRepository
{
    private const string TableName = "Course";

    // Course is the first table in this repo with outbound FKs, so it is the first to multi-map nav
    // objects. Partner and PublishStatus are inner joins (NOT NULL FKs); CourseGroup is LEFT JOIN
    // because CourseGroup_pkid is nullable — Dapper yields a null nav object when the id is null.
    //
    // The six count subqueries are the delete guard, read off the entity the controller already loaded
    // for its 404. CourseInCertification / CourseJobCategories are ON DELETE CASCADE, so those counts
    // are the only thing standing between a delete and silent destruction of the junction rows.
    //
    // Column order matters for the multi-map: all Course columns (scalars, aliased FK ids, counts)
    // come first, then Partner (pkid, Name), CourseGroup (pkid, Description), PublishStatus
    // (pkid, Description). splitOn = "pkid,pkid,pkid" marks the start of each nav object.
    private const string SelectColumns = """
        SELECT c.pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl, c.DisplayOrder,
               c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid,
               c.PublishStatus_pkid AS PublishStatusPkid, c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice,
               c.LearningCredit, c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
               c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
               (SELECT COUNT(*) FROM CourseFAQ f             WHERE f.Course_pkid = c.pkid) AS CourseFaqCount,
               (SELECT COUNT(*) FROM CourseInCertification i WHERE i.Course_pkid = c.pkid) AS CertificationCount,
               (SELECT COUNT(*) FROM CourseJobCategories j   WHERE j.Course_pkid = c.pkid) AS JobCategoryCount,
               (SELECT COUNT(*) FROM CourseRelatedLink r     WHERE r.Course_pkid = c.pkid) AS RelatedLinkCount,
               (SELECT COUNT(*) FROM HotCourse h             WHERE h.Course_pkid = c.pkid) AS HotCourseCount,
               (SELECT COUNT(*) FROM CourseRecomm cr
                 WHERE cr.CourseId = c.CourseId OR cr.RecommCourseId = c.CourseId)          AS RecommCount,
               p.pkid, p.Name,
               cg.pkid, cg.Description,
               ps.pkid, ps.Description
        FROM Course c
        JOIN Partner p           ON p.pkid  = c.Partner_pkid
        LEFT JOIN CourseGroup cg ON cg.pkid = c.CourseGroup_pkid
        JOIN PublishStatus ps    ON ps.pkid = c.PublishStatus_pkid
        """;

    private const string OrderBy = " ORDER BY c.DisplayOrder ASC, c.pkid ASC";

    private const string SplitOn = "pkid,pkid,pkid";

    private static Course Map(Course course, CourseNavPartner partner, CourseNavCourseGroup? group, CourseNavPublishStatus status)
    {
        course.Partner = partner;
        course.CourseGroup = group; // null for courses with no group (LEFT JOIN)
        course.PublishStatus = status;
        return course;
    }

    public async Task<IEnumerable<Course>> GetAllAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<Course, CourseNavPartner, CourseNavCourseGroup, CourseNavPublishStatus, Course>(
            $"{SelectColumns}{OrderBy}", Map, splitOn: SplitOn);
    }

    public async Task<IEnumerable<Course>> QueryAsync(CourseQuery query)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("""
                (c.Title LIKE @Keyword
                 OR c.OfficialTitle LIKE @Keyword
                 OR c.CourseId LIKE @Keyword
                 OR c.ProdCourseId LIKE @Keyword
                 OR c.FriendlyUrl LIKE @Keyword)
                """);
            parameters.Add("Keyword", $"%{query.Keyword.Trim()}%");
        }

        if (query.PartnerPkid.HasValue)
        {
            where.Add("c.Partner_pkid = @PartnerPkid");
            parameters.Add("PartnerPkid", query.PartnerPkid.Value);
        }

        if (query.CourseGroupPkid.HasValue)
        {
            where.Add("c.CourseGroup_pkid = @CourseGroupPkid");
            parameters.Add("CourseGroupPkid", query.CourseGroupPkid.Value);
        }

        if (query.PublishStatusPkid.HasValue)
        {
            where.Add("c.PublishStatus_pkid = @PublishStatusPkid");
            parameters.Add("PublishStatusPkid", query.PublishStatusPkid.Value);
        }

        if (query.ScheduleOnFrom.HasValue)
        {
            where.Add("c.ScheduleOn >= @ScheduleOnFrom");
            parameters.Add("ScheduleOnFrom", query.ScheduleOnFrom.Value);
        }

        if (query.ScheduleOnTo.HasValue)
        {
            where.Add("c.ScheduleOn <= @ScheduleOnTo");
            parameters.Add("ScheduleOnTo", query.ScheduleOnTo.Value);
        }

        if (query.ScheduleOffFrom.HasValue)
        {
            where.Add("c.ScheduleOff >= @ScheduleOffFrom");
            parameters.Add("ScheduleOffFrom", query.ScheduleOffFrom.Value);
        }

        if (query.ScheduleOffTo.HasValue)
        {
            where.Add("c.ScheduleOff <= @ScheduleOffTo");
            parameters.Add("ScheduleOffTo", query.ScheduleOffTo.Value);
        }

        if (query.CanRepeat.HasValue)
        {
            where.Add("c.CanRepeat = @CanRepeat");
            parameters.Add("CanRepeat", query.CanRepeat.Value);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBy;

        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<Course, CourseNavPartner, CourseNavCourseGroup, CourseNavPublishStatus, Course>(
            sql, Map, parameters, splitOn: SplitOn);
    }

    public async Task<Course?> GetByPkidAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        var rows = await conn.QueryAsync<Course, CourseNavPartner, CourseNavCourseGroup, CourseNavPublishStatus, Course>(
            $"{SelectColumns} WHERE c.pkid = @Pkid", Map, new { Pkid = pkid }, splitOn: SplitOn);
        return rows.SingleOrDefault();
    }

    /// <summary>pkid is an int IDENTITY — generated by SQL Server, never sent by the caller.</summary>
    public async Task<int> CreateAsync(CourseRequest request)
    {
        const string sql = """
            INSERT INTO Course (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
                Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, Hour,
                ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
                TowardCertOrExam, Note, OtherInfo, CanRepeat)
            VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
                @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
                @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
                @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var pkid = await conn.ExecuteScalarAsync<int>(sql, request, tx);
        var created = await LoadByPkidAsync(conn, pkid, tx);
        await auditWriter.LogInsertAsync(TableName, created!, conn, tx);

        tx.Commit();
        return pkid;
    }

    /// <summary>Every non-key column is writable; pkid identifies the row and is never re-written.</summary>
    public async Task<bool> UpdateAsync(CourseRequest request)
    {
        const string sql = """
            UPDATE Course
            SET Title = @Title,
                OfficialTitle = @OfficialTitle,
                CourseId = @CourseId,
                ProdCourseId = @ProdCourseId,
                FriendlyUrl = @FriendlyUrl,
                DisplayOrder = @DisplayOrder,
                Partner_pkid = @PartnerPkid,
                CourseGroup_pkid = @CourseGroupPkid,
                PublishStatus_pkid = @PublishStatusPkid,
                ScheduleOn = @ScheduleOn,
                ScheduleOff = @ScheduleOff,
                Hour = @Hour,
                ListPrice = @ListPrice,
                LearningCredit = @LearningCredit,
                Material = @Material,
                Objective = @Objective,
                Target = @Target,
                Prerequisites = @Prerequisites,
                Outline = @Outline,
                TowardCertOrExam = @TowardCertOrExam,
                Note = @Note,
                OtherInfo = @OtherInfo,
                CanRepeat = @CanRepeat
            WHERE pkid = @Pkid;
            """;

        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var before = await LoadByPkidAsync(conn, request.Pkid, tx);
        if (before is null)
        {
            return false;
        }

        await conn.ExecuteAsync(sql, request, tx);
        var after = await LoadByPkidAsync(conn, request.Pkid, tx);
        await auditWriter.LogUpdateAsync(TableName, before, after!, conn, tx);

        tx.Commit();
        return true;
    }

    /// <summary>
    /// Unguarded: FK_CourseInCertification_Course and FK_CourseJobCategories_Course are ON DELETE
    /// CASCADE, so SQL Server deletes the junction rows instead of rejecting. CoursesController.Delete
    /// checks the counts first — see its 409 guard.
    /// </summary>
    public async Task<bool> DeleteAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var row = await LoadByPkidAsync(conn, pkid, tx);
        if (row is null)
        {
            return false;
        }

        await conn.ExecuteAsync("DELETE FROM Course WHERE pkid = @Pkid", new { Pkid = pkid }, tx);
        await auditWriter.LogDeleteAsync(TableName, row, conn, tx);

        tx.Commit();
        return true;
    }

    /// <summary>Loads a row (with nav objects) on an existing open connection/transaction. The audit's
    /// changed-column comparison ignores nav objects, so reloading before/after is safe (see PublishStatusRepository).</summary>
    private static async Task<Course?> LoadByPkidAsync(IDbConnection conn, int pkid, IDbTransaction tx)
    {
        var rows = await conn.QueryAsync<Course, CourseNavPartner, CourseNavCourseGroup, CourseNavPublishStatus, Course>(
            $"{SelectColumns} WHERE c.pkid = @Pkid", Map, new { Pkid = pkid }, tx, splitOn: SplitOn);
        return rows.SingleOrDefault();
    }
}
