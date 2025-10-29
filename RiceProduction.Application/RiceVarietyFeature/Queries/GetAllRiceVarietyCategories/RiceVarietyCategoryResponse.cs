namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarietyCategories
{
    public class RiceVarietyCategoryResponse
    {
        public Guid Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int MinGrowthDays { get; set; }
        public int MaxGrowthDays { get; set; }
        public bool IsActive { get; set; }
    }
}

