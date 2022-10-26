using System;

namespace SqlAzureDatabaseBasicMonitor
{
    public class SqlPerformance
    {
        public DateTime? EndTime { get; set; }
        public decimal? AvgCpuPercent { get; set; }
        public decimal? AvgDataIoPercent { get; set; }
        public decimal? AvgLogWritePercent { get; set; }
        public decimal? AvgMemoryUsagePercent { get; set; }
        public decimal? XtpStoragePercent { get; set; }
        public decimal? MaxWorkerPercent { get; set; }
        public decimal? MaxSessionPercent { get; set; }
        public int? DtuLimit { get; set; }
        public decimal? AvgLoginRatePercent { get; set; }
        public decimal? AvgInstanceCpuPercent { get; set; }
        public decimal? AvgInstanceMemoryPercent { get; set; }
        public int? CpuLimit { get; set; }
        public int? ReplicaRole { get; set; }
    }
}
