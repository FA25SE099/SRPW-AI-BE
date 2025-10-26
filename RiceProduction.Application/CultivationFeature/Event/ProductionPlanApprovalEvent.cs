using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.CultivationFeature.Event
{
    public class ProductionPlanApprovalEvent : INotification
    {
        public Guid PlanId { get; set; }

    }
}
