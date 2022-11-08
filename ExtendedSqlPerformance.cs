using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlAzureDatabaseBasicMonitor
{
#nullable enable
    public class ExtendedSqlPerformance
    {
        public DateTime? reading_time { get; set; }
        public short session_id { get; set; }
        public int? request_id { get; set; }
        public int? ecid { get; set; }
        public short blocking_session_id { get; set; }
        public int? blocking_ecid { get; set; }
        public string? task_state { get; set; }
        public string? wait_type { get; set; }
        public int wait_duration_ms { get; set; }
        public string? wait_resource { get; set; }
        public string? resource_description { get; set; }
        public string? last_wait_type { get; set; }
        public int? open_trans { get; set; }
        public string? transaction_isolation_level { get; set; }
        public bool? is_user_process { get; set; }
        public int? request_cpu_time { get; set; }
        public long request_logical_reads { get; set; }
        public long request_reads { get; set; }
        public long request_writes { get; set; }
        public int? memory_usage { get; set; }
        public int? session_cpu_time { get; set; }
        public long session_reads { get; set; }
        public long session_writes { get; set; }
        public long session_logical_reads { get; set; }
        public int? total_scheduled_time { get; set; }
        public int? total_elapsed_time { get; set; }
        public DateTime? last_request_start_time { get; set; }
        public DateTime? last_request_end_time { get; set; }
        public long session_row_count { get; set; }
        public int? prev_error { get; set; }
        public int? open_resultsets { get; set; }
        public int? request_total_elapsed_time { get; set; }
        public decimal? percent_complete { get; set; }
        public long est_completion_time { get; set; }
        public string? tran_name { get; set; }
        public DateTime? transaction_begin_time { get; set; }
        public string? tran_type { get; set; }
        public string? tran_state { get; set; }
        public DateTime? request_start_time { get; set; }
        public string? request_status { get; set; }
        public string? command { get; set; }
        public byte[]? plan_handle { get; set; }
        public byte[]? sql_handle { get; set; }
        public int? statement_start_offset { get; set; }
        public int? statement_end_offset { get; set; }
        public short database_id { get; set; }
        public int? user_id { get; set; }
        public bool? executing_managed_code { get; set; }
        public int? pending_io_count { get; set; }
        public DateTime? login_time { get; set; }
        public string? host_name { get; set; }
        public string? program_name { get; set; }
        public int? host_process_id { get; set; }
        public int? client_version { get; set; }
        public string? client_interface_name { get; set; }
        public string? login_name { get; set; }
        public string? nt_domain { get; set; }
        public string? nt_user_name { get; set; }
        public int? net_packet_size { get; set; }
        public string? client_net_address { get; set; }
        public byte[]? most_recent_sql_handle { get; set; }
        public string? session_status { get; set; }
        public int? scheduler_id { get; set; }
        public int? group_id { get; set; }
        public byte[]? context_info { get; set; }
#nullable restore
    }
}
