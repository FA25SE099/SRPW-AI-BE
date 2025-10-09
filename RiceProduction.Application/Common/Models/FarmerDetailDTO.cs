using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Models
{
    public class FarmerDetailDTO : FarmerDTO
    {
       public List<PlotDTO> Plots { get; set; }
       public List<FarmLogDTO> FarmLogs { get; set; }
       public List<PlotCultivationDTO> PlotCultivations { get; set; }
       public List<CultivationTaskDTO> CultivationTasks { get; set; }
    }

    public class FarmLogDTO
    {

    }

    public class PlotCultivationDTO
    {

    }

    public class CultivationTaskDTO
    {

    }
}
