using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlAzureDatabaseBasicMonitor
{
    internal class SqlWaitStats
    {
        public DateTime? reading_time { get; set; }
        public string wait_type { get; set; }
        public long waiting_tasks_count { get; set; }
        public long wait_time_ms { get; set; }
        public long max_wait_time_ms { get; set; }
        public long signal_wait_time_ms { get; set; }
    }
}
