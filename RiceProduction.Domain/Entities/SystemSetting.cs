using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities
{
    public class SystemSetting : BaseAuditableEntity
    {
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string SettingCategory { get; set; } = string.Empty;
        public string SettingDescription { get; set; } = string.Empty;
    }
}
