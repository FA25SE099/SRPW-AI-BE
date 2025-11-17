public class FarmerPlanStageViewResponse
{
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public List<FarmerCultivationTaskResponse> Tasks { get; set; } = new();
}