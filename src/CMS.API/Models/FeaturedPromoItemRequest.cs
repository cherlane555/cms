namespace CMS.API.Models;

public class FeaturedPromoItemRequest
{
    public int Pkid { get; set; }
    public DateTime ScheduleOn { get; set; }
    public short TrainingCenterPkid { get; set; }
    public byte Slot { get; set; }
    public int PromotionPkid { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
