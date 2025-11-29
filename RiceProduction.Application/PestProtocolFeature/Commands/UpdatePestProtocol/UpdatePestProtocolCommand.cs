using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.PestProtocolFeature.Commands.UpdatePestProtocol;

public class UpdatePestProtocolCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; }

    /// <summary>
    /// List of image links/URLs for pest protocol images
    /// </summary>
    public List<string>? ImageLinks { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
