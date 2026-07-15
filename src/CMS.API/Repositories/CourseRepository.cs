using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseRepository : ICourseRepository
{
    private const string TableName = "Course";

    private readonly IDbConnectionFactory _factory;
    private readonly IRowAuditWriter _audit;

    public CourseRepository(IDbConnectionFactory factory, IRowAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    // CourseGroup_pkid is nullable -> LEFT JOIN.
    private const string BaseSelect = @"
SELECT c.pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder, c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid,
       c.PublishStatus_pkid AS PublishStatusPkid, c.ScheduleOn, c.ScheduleOff, c.Hour,
       c.ListPrice, c.LearningCredit, c.Material, c.Objective, c.Target, c.Prerequisites,
       c.Outline, c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
       p.Name AS PartnerName, g.Description AS CourseGroupDescription,
       s.Description AS PublishStatusDescription
FROM Course c
JOIN Partner p ON p.pkid = c.Partner_pkid
LEFT JOIN CourseGroup g ON g.pkid = c.CourseGroup_pkid
JOIN PublishStatus s ON s.pkid = c.PublishStatus_pkid";

    public async Task<IEnumerable<Course>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<Course>(new CommandDefinition(
            $"{BaseSelect}\nORDER BY c.DisplayOrder ASC, c.pkid ASC", cancellationToken: ct));
    }

    public async Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add(@"(c.Title LIKE @Keyword OR c.OfficialTitle LIKE @Keyword
                        OR c.CourseId LIKE @Keyword OR c.ProdCourseId LIKE @Keyword)");
            p.Add("@Keyword", $"%{query.Keyword.Trim()}%");
        }
        if (query.PartnerPkid.HasValue)
        {
            where.Add("c.Partner_pkid = @PartnerPkid");
            p.Add("@PartnerPkid", query.PartnerPkid.Value);
        }
        if (query.CourseGroupPkid.HasValue)
        {
            where.Add("c.CourseGroup_pkid = @CourseGroupPkid");
            p.Add("@CourseGroupPkid", query.CourseGroupPkid.Value);
        }
        if (query.PublishStatusPkid.HasValue)
        {
            where.Add("c.PublishStatus_pkid = @PublishStatusPkid");
            p.Add("@PublishStatusPkid", query.PublishStatusPkid.Value);
        }

        var whereClause = where.Count > 0 ? $"\nWHERE {string.Join(" AND ", where)}" : string.Empty;
        var sql = $"{BaseSelect}{whereClause}\nORDER BY c.DisplayOrder ASC, c.pkid ASC";

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<Course>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        var course = await conn.QuerySingleOrDefaultAsync<Course>(new CommandDefinition(
            $"{BaseSelect}\nWHERE c.pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        if (course is null)
        {
            return null;
        }

        var certIds = await conn.QueryAsync<int>(new CommandDefinition(
            "SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid ORDER BY Certification_pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        course.CertificationPkids = certIds.ToList();

        var jobIds = await conn.QueryAsync<short>(new CommandDefinition(
            "SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid ORDER BY JobCategory_pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        course.JobCategoryPkids = jobIds.ToList();

        return course;
    }

    public async Task<Course> CreateAsync(CourseRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO Course (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
                    Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff,
                    [Hour], ListPrice, LearningCredit, Material, Objective, Target, Prerequisites,
                    Outline, TowardCertOrExam, Note, OtherInfo, CanRepeat)
VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
        @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff,
        @Hour, @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites,
        @Outline, @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);",
            ToParams(request), tx, cancellationToken: ct));

        await SyncAssociationsAsync(conn, tx, pkid, request, ct);

        // Audit inside the same transaction; the FK labels are display-only, so an entity
        // built from the request (Title first) is enough for the audit row.
        await _audit.LogInsertAsync(TableName, ApplyRequest(new Course { Pkid = pkid }, request), conn, tx, ct);
        tx.Commit();

        return (await GetByIdAsync(pkid, ct))!;
    }

    public async Task<bool> UpdateAsync(CourseRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the "before" row first so the audit can list exactly the changed columns.
        var before = await conn.QuerySingleOrDefaultAsync<Course>(new CommandDefinition(
            $"{BaseSelect}\nWHERE c.pkid = @Pkid",
            new { request.Pkid }, tx, cancellationToken: ct));
        if (before is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
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
    [Hour] = @Hour,
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
WHERE pkid = @Pkid;",
            ToParams(request), tx, cancellationToken: ct));

        if (affected == 0)
        {
            tx.Rollback();
            return false;
        }

        await SyncAssociationsAsync(conn, tx, request.Pkid, request, ct);

        await _audit.LogUpdateAsync(TableName, before, ApplyRequest(before, request), conn, tx, ct);
        tx.Commit();
        return true;
    }

    public async Task<int> CountBlockingDependentsAsync(int pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT (SELECT COUNT(*) FROM CourseFAQ WHERE Course_pkid = @Pkid)
     + (SELECT COUNT(*) FROM CourseRelatedLink WHERE Course_pkid = @Pkid)
     + (SELECT COUNT(*) FROM HotCourse WHERE Course_pkid = @Pkid)",
            new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken ct = default)
    {
        // CourseInCertification / CourseJobCategories rows go via ON DELETE CASCADE.
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the row first so its first string column is still available for the audit.
        var row = await conn.QuerySingleOrDefaultAsync<Course>(new CommandDefinition(
            $"{BaseSelect}\nWHERE c.pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (row is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Course WHERE pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (affected == 0)
        {
            return false;
        }

        await _audit.LogDeleteAsync(TableName, row, conn, tx, ct);
        tx.Commit();
        return true;
    }

    /// <summary>
    /// The "after" image for the audit diff: the before row with the updatable columns applied.
    /// FK labels and n-n lists are copied from before so only real Course columns show up as changed.
    /// </summary>
    private static Course ApplyRequest(Course before, CourseRequest request) => new()
    {
        Pkid = before.Pkid,
        Title = request.Title,
        OfficialTitle = request.OfficialTitle,
        CourseId = request.CourseId,
        ProdCourseId = request.ProdCourseId,
        FriendlyUrl = request.FriendlyUrl,
        DisplayOrder = request.DisplayOrder,
        PartnerPkid = request.PartnerPkid,
        CourseGroupPkid = request.CourseGroupPkid,
        PublishStatusPkid = request.PublishStatusPkid,
        ScheduleOn = request.ScheduleOn.Date,
        ScheduleOff = request.ScheduleOff.Date,
        Hour = request.Hour,
        ListPrice = request.ListPrice,
        LearningCredit = request.LearningCredit,
        Material = request.Material,
        Objective = request.Objective,
        Target = request.Target,
        Prerequisites = request.Prerequisites,
        Outline = request.Outline,
        TowardCertOrExam = request.TowardCertOrExam,
        Note = request.Note,
        OtherInfo = request.OtherInfo,
        CanRepeat = request.CanRepeat,
        PartnerName = before.PartnerName,
        CourseGroupDescription = before.CourseGroupDescription,
        PublishStatusDescription = before.PublishStatusDescription,
        CertificationPkids = before.CertificationPkids,
        JobCategoryPkids = before.JobCategoryPkids
    };

    private static object ToParams(CourseRequest request) => new
    {
        request.Pkid,
        request.Title,
        request.OfficialTitle,
        request.CourseId,
        request.ProdCourseId,
        request.FriendlyUrl,
        request.DisplayOrder,
        request.PartnerPkid,
        request.CourseGroupPkid,
        request.PublishStatusPkid,
        ScheduleOn = request.ScheduleOn.Date,
        ScheduleOff = request.ScheduleOff.Date,
        request.Hour,
        request.ListPrice,
        request.LearningCredit,
        request.Material,
        request.Objective,
        request.Target,
        request.Prerequisites,
        request.Outline,
        request.TowardCertOrExam,
        request.Note,
        request.OtherInfo,
        request.CanRepeat
    };

    // n-n sync: delete-then-reinsert on the same connection/transaction.
    private static async Task SyncAssociationsAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        int coursePkid,
        CourseRequest request,
        CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid",
            new { Pkid = coursePkid }, tx, cancellationToken: ct));
        if (request.CertificationPkids is { Count: > 0 })
        {
            var rows = request.CertificationPkids.Distinct()
                .Select(id => new { CoursePkid = coursePkid, CertificationPkid = id });
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@CoursePkid, @CertificationPkid)",
                rows, tx, cancellationToken: ct));
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseJobCategories WHERE Course_pkid = @Pkid",
            new { Pkid = coursePkid }, tx, cancellationToken: ct));
        if (request.JobCategoryPkids is { Count: > 0 })
        {
            var rows = request.JobCategoryPkids.Distinct()
                .Select(id => new { CoursePkid = coursePkid, JobCategoryPkid = id });
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@CoursePkid, @JobCategoryPkid)",
                rows, tx, cancellationToken: ct));
        }
    }
}
