namespace CMS.API.Models;

/// <summary>Slim Promotion2 row for the PromoCode → Promotion_pkid lookup.</summary>
public class PromotionLookup
{
    public int Pkid { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
