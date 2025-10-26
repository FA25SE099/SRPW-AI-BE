using RiceProduction.Domain.Enums;

public class ExpertPlotResponse
{
    public Guid Id { get; set; }
    public decimal Area { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public string? SoilType { get; set; }
    public PlotStatus Status { get; set; }
    public Guid FarmerId { get; set; }
}