using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities;
//status: kế thừa StandardPlan, không status
public class EmergencyProtocol : StandardPlan
{
    // Navigation properties - one to many
    public ICollection<Threshold> Thresholds { get; set; } = new List<Threshold>();
}
