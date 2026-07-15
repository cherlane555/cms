namespace CMS.API.Models;

/// <summary>Search DTO. ScheduleFrom/ScheduleTo carry the Mon–Sun week window.</summary>
public class FeaturedPromoItemQuery
{
    public short? TrainingCenterPkid { get; set; }
    public DateTime? ScheduleFrom { get; set; }
    public DateTime? ScheduleTo { get; set; }
}
