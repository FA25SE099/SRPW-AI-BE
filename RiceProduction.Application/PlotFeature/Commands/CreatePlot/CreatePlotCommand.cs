using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Commands.CreatePlot
{
    public class CreatePlotCommand : IRequest<Result<PlotResponse>>
    {
        [Required(ErrorMessage = "Farmer ID is required")]
        public Guid FarmerId { get; set; }

        [Required(ErrorMessage = "SoThua is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SoThua must be greater than 0")]
        public int? SoThua { get; set; }

        [Required(ErrorMessage = "SoTo is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SoTo must be greater than 0")]
        public int? SoTo { get; set; }

        [Required(ErrorMessage = "Area is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Area must be greater than 0")]
        public decimal? Area { get; set; }

        [StringLength(100, ErrorMessage = "Soil type cannot exceed 100 characters")]
        public string? SoilType { get; set; }

        /// <summary>
        /// Optional: Rice variety name for initial cultivation
        /// </summary>
        [StringLength(200, ErrorMessage = "Rice variety name cannot exceed 200 characters")]
        public string? RiceVarietyName { get; set; }

        /// <summary>
        /// Optional: Cluster manager ID for polygon assignment task
        /// </summary>
        public Guid? ClusterManagerId { get; set; }
    }
}

