using System;

namespace SqlAzureDatabaseBasicMonitor
{
    public class SqlPerformance
    {
        public DateTime? end_time { get; set; }
        public decimal? avg_cpu_percent { get; set; }
        public decimal? avg_data_io_percent { get; set; }
        public decimal? avg_log_write_percent { get; set; }
        public decimal? avg_memory_usage_percent { get; set; }
        public decimal? xtp_storage_percent { get; set; }
        public decimal? max_worker_percent { get; set; }
        public decimal? max_session_percent { get; set; }
        public int? dtu_limit { get; set; }
        public decimal? avg_login_rate_percent { get; set; }
        public decimal? avg_instance_cpu_percent { get; set; }
        public decimal? avg_instance_memory_percent { get; set; }
        public int? cpu_limit { get; set; }
        public int? replica_role { get; set; }
    }
}
