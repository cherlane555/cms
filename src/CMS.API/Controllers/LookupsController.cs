using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/lookups")]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _repository;

    public LookupsController(ILookupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Slim AppUser list for the role's user multi-select.</summary>
    [HttpGet("app-users")]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken ct)
    {
        var users = await _repository.GetAppUsersAsync(ct);
        return Ok(users);
    }

    /// <summary>Slim PublishStatus list for FK dropdowns (e.g. Course).</summary>
    [HttpGet("publish-statuses")]
    public async Task<ActionResult<IEnumerable<PublishStatusLookup>>> GetPublishStatuses(CancellationToken ct)
    {
        var statuses = await _repository.GetPublishStatusesAsync(ct);
        return Ok(statuses);
    }

    /// <summary>Slim Partner list for FK dropdowns (e.g. Course, Certification).</summary>
    [HttpGet("partners")]
    public async Task<ActionResult<IEnumerable<PartnerLookup>>> GetPartners(CancellationToken ct)
    {
        var partners = await _repository.GetPartnersAsync(ct);
        return Ok(partners);
    }

    /// <summary>Slim CourseGroup list for FK dropdowns (e.g. Course, PartnerCourseGroup).</summary>
    [HttpGet("course-groups")]
    public async Task<ActionResult<IEnumerable<CourseGroupLookup>>> GetCourseGroups(CancellationToken ct)
    {
        var groups = await _repository.GetCourseGroupsAsync(ct);
        return Ok(groups);
    }

    /// <summary>Slim Certification list for the Course certifications multi-select.</summary>
    [HttpGet("certifications")]
    public async Task<ActionResult<IEnumerable<CertificationLookup>>> GetCertifications(CancellationToken ct)
    {
        var certifications = await _repository.GetCertificationsAsync(ct);
        return Ok(certifications);
    }

    /// <summary>Slim JobCategory list for the Course job-categories multi-select.</summary>
    [HttpGet("job-categories")]
    public async Task<ActionResult<IEnumerable<JobCategoryLookup>>> GetJobCategories(CancellationToken ct)
    {
        var categories = await _repository.GetJobCategoriesAsync(ct);
        return Ok(categories);
    }

    /// <summary>Slim TrainingCenter list for the FeaturedPromoItem tabs.</summary>
    [HttpGet("training-centers")]
    public async Task<ActionResult<IEnumerable<TrainingCenterLookup>>> GetTrainingCenters(CancellationToken ct)
    {
        var centers = await _repository.GetTrainingCentersAsync(ct);
        return Ok(centers);
    }

    /// <summary>Resolve a Promotion2 row by its unique PromoCode (FeaturedPromoItem form lookup).</summary>
    [HttpGet("promotions/by-code/{code}")]
    public async Task<ActionResult<PromotionLookup>> GetPromotionByCode(string code, CancellationToken ct)
    {
        var promotion = await _repository.GetPromotionByCodeAsync(code, ct);
        return promotion is null ? NotFound() : Ok(promotion);
    }
}
