using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace owasp_zap_vsts_tool.Models
{
    internal class Report
    {
        internal IEnumerable<Issue> Issues { get; set; }
    }
}
