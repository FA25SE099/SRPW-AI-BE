using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities;
//status: Is Active: true false nhưng không sử dụng
public class WeatherProtocol : BaseAuditableEntity
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    public string? SourceLink { get; set; }

    public List<string>? ImageLinks { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public ICollection<Threshold> Thresholds { get; set; } = new List<Threshold>();
}