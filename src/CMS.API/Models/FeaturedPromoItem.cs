namespace CMS.API.Models;

/// <summary>Response model. PromoCode is looked up from Promotion2 via Promotion_pkid.</summary>
public class FeaturedPromoItem
{
    public int Pkid { get; set; }
    public DateTime ScheduleOn { get; set; }
    public short TrainingCenterPkid { get; set; }
    public byte Slot { get; set; }
    public int PromotionPkid { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PromoCode { get; set; } = string.Empty;
}
