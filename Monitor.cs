using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using System.Reflection.Metadata;

namespace SqlAzureDatabaseBasicMonitor
{
    public class Monitor
    {
        internal BlobServiceClient BlobServiceClient { get; set; }
        internal BlobContainerClient BlobContainerClient { get; set; }
        internal string ContainerName { get; set; }
        internal string SqlAzureConnectionString { get; set; }
        internal string CsvSeparator { get; set; }
        internal bool ExtendedPerformanceMonitoring { get; set; }
        internal AppendBlobClient PerformanceLog { get; set; }
        internal AppendBlobClient ErrorLog { get; set; }
        internal AppendBlobClient ExtendedPerformanceLog { get; set; }
        internal AppendBlobClient WaitStatsLog { get; set; }
        internal SqlConnection cn { get; set; }

        internal ILogger Log { get; set; }

        [FunctionName("Monitor")]
        public void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            // Moving Azure Function log to class property
            this.Log = log;
            this.Log.LogInformation($"SQL Azure Basic monitor function executed at: {DateTime.Now}");

            // Set up our Azure Blob connection
            string blob_storage_connection_string = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING", EnvironmentVariableTarget.Process);
            this.BlobServiceClient = new BlobServiceClient(blob_storage_connection_string);
            this.ContainerName = Environment.GetEnvironmentVariable("LOGGING_CONTAINER_NAME", EnvironmentVariableTarget.Process);

            this.SqlAzureConnectionString = Environment.GetEnvironmentVariable("SQL_AZURE_CONNECTION_STRING", EnvironmentVariableTarget.Process);
            this.CsvSeparator = Environment.GetEnvironmentVariable("LOG_FILE_CSV_SEPARATOR", EnvironmentVariableTarget.Process);

            // Verifying object is not null before turning it to lower, defaults to false if config is not 1 or true
#nullable enable
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            string? SettingHold = Environment.GetEnvironmentVariable("ENABLE_EXTENDED_SQL_PERFORMANCE_MONITORING", EnvironmentVariableTarget.Process);
            this.ExtendedPerformanceMonitoring = SettingHold == "1" 
                || (SettingHold.IsNullOrEmpty() ?
                    "" : SettingHold
                ).ToLower() == "true";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#nullable restore

            // Make sure our log files are ready and accessible
            PrepareLogFiles().Wait();
            // Actually check if the DB is working
            MonitorViaQuery().Wait();
        }

        public async Task PrepareLogFiles()
        {
            // Try to connect to the container, create it if it doesn't exist
            try
            {
                this.BlobContainerClient = BlobServiceClient.GetBlobContainerClient(this.ContainerName);
                await BlobContainerClient.CreateAsync();
            }
            catch (RequestFailedException e)
            {
                this.Log.LogDebug(e.Message);
            }

            // Create the files if they don't exist already, date pattern for the file name,
            // this also allows for the file to auto move to a new date based file daily
            try
            {
                this.PerformanceLog = this.BlobContainerClient.GetAppendBlobClient(string.Format("performance-{0:yyyy'-'MM'-'dd}.csv", DateTime.Now));
                await this.PerformanceLog.CreateIfNotExistsAsync();

                this.ErrorLog = this.BlobContainerClient.GetAppendBlobClient(string.Format("error-{0:yyyy'-'MM'-'dd}.csv", DateTime.Now));
                await this.ErrorLog.CreateIfNotExistsAsync();

                if(this.ExtendedPerformanceMonitoring)
                {
                    this.ExtendedPerformanceLog = this.BlobContainerClient.GetAppendBlobClient(string.Format("extended-performance-{0:yyyy'-'MM'-'dd}.csv", DateTime.Now));
                    await this.ExtendedPerformanceLog.CreateIfNotExistsAsync();

                    this.WaitStatsLog = this.BlobContainerClient.GetAppendBlobClient(string.Format("wait_stats-{0:yyyy'-'MM'-'dd}.csv", DateTime.Now));
                    await this.WaitStatsLog.CreateIfNotExistsAsync();
                }
            }
            catch (RequestFailedException e)
            {
                // We expect the container to exist already, so we don't need to worry about that exception
                // otherwise full abort
                if(e.ErrorCode != BlobErrorCode.ContainerAlreadyExists)
                {
                    this.Log.LogError(e.Message);
                    throw;
                }
            }
        }

        public async Task MonitorViaQuery()
        {   
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                // No headers to prevent headaches reading elsewhere
                HasHeaderRecord = false,
                Delimiter = this.CsvSeparator
            };
            try
            {
                // Attempt to connect to SQL
                this.cn = new SqlConnection(this.SqlAzureConnectionString);
                // With a valid connection, get the last minute of performance data (should be 4 entries)
                await OutputMonitorData<SqlPerformance>(query: "SELECT * FROM sys.dm_db_resource_stats WHERE end_time > DATEADD(mi, -1, current_timestamp) ORDER BY end_time", config: config, this.PerformanceLog);
                // Write out our records to an append blob
                this.Log.LogInformation($"SQL Peformance information logged at {DateTime.Now}");

                if(this.ExtendedPerformanceMonitoring)
                {
                    // This SQL perf query gathers a ton of performance data around waits, locking, blocking, and other query issues
                    // This is a modified version of the Azure_Sql_Perf_Stats script that was used in Microsoft Support for query 
                    // monitoring originally created by Jack Li and modified by Rohit Nayak. 
                    string extended_performance_monitor_query = @"
                                                  SELECT
    CURRENT_TIMESTAMP as reading_time, 
	sess.session_id, 
	req.request_id, 
	null ecid,
	req.blocking_session_id, 
	null blocking_ecid,
	null task_state,
	LEFT (ISNULL (req.wait_type, ''), 50) AS wait_type, 
	req.wait_time AS wait_duration_ms, 
	LEFT (ISNULL (req.wait_resource, ''), 40) AS wait_resource, 
    null resource_description,
	LEFT (req.last_wait_type, 50) AS last_wait_type, 
    sess_tran.open_transaction_count as open_trans, 
    
    LEFT (CASE COALESCE(req.transaction_isolation_level, sess.transaction_isolation_level)
      WHEN 0 THEN '0-Read Committed' 
      WHEN 1 THEN '1-Read Uncommitted (NOLOCK)' 
      WHEN 2 THEN '2-Read Committed' 
      WHEN 3 THEN '3-Repeatable Read' 
      WHEN 4 THEN '4-Serializable' 
      WHEN 5 THEN '5-Snapshot' 
      ELSE CONVERT (varchar(30), req.transaction_isolation_level) + '-UNKNOWN' 
    END, 30) AS transaction_isolation_level, 
    sess.is_user_process, 
	req.cpu_time AS request_cpu_time, 
    req.logical_reads AS request_logical_reads,
    req.reads AS request_reads,
    req.writes AS request_writes,
    sess.memory_usage as memory_usage, 
	sess.cpu_time AS session_cpu_time, 
	sess.reads AS session_reads, 
	sess.writes AS session_writes, 
	sess.logical_reads AS session_logical_reads, 
    sess.total_scheduled_time, 
	sess.total_elapsed_time, 
	sess.last_request_start_time, 
	sess.last_request_end_time, 
	sess.row_count AS session_row_count, 
    sess.prev_error, 
	req.open_resultset_count AS open_resultsets, 
	req.total_elapsed_time AS request_total_elapsed_time, 
    CONVERT (decimal(5,2), req.percent_complete) AS percent_complete, 
	req.estimated_completion_time AS est_completion_time, 
    
    /* Taken from query 2 of the perf stats script, combining and will verify performance */

    LEFT (COALESCE (reqtrans.name, sesstrans.name, ''), 24) AS tran_name, 
    COALESCE (reqtrans.transaction_begin_time, sesstrans.transaction_begin_time) AS transaction_begin_time, 
    LEFT (CASE COALESCE (reqtrans.transaction_type, sesstrans.transaction_type)
		WHEN 1 THEN '1-Read/write'
		WHEN 2 THEN '2-Read only'
		WHEN 3 THEN '3-System'
		WHEN 4 THEN '4-Distributed'
    ELSE CONVERT (varchar(30), COALESCE (reqtrans.transaction_type, sesstrans.transaction_type)) + '-UNKNOWN' 
    END, 15) AS tran_type, 

    LEFT (CASE COALESCE (reqtrans.transaction_state, sesstrans.transaction_state)
		WHEN 0 THEN '0-Initializing'
		WHEN 1 THEN '1-Initialized'
		WHEN 2 THEN '2-Active'
		WHEN 3 THEN '3-Ended'
		WHEN 4 THEN '4-Preparing'
		WHEN 5 THEN '5-Prepared'
		WHEN 6 THEN '6-Committed'
		WHEN 7 THEN '7-Rolling back'
		WHEN 8 THEN '8-Rolled back'
    ELSE CONVERT (varchar(30), COALESCE (reqtrans.transaction_state, sesstrans.transaction_state)) + '-UNKNOWN'
    END, 15) AS tran_state, 

    req.start_time AS request_start_time, 
	LEFT (req.status, 15) AS request_status, 
	req.command, 
	req.plan_handle, 
	req.sql_handle, 
	req.statement_start_offset, 
    req.statement_end_offset, 
	req.database_id, 
	req.[user_id], 
	req.executing_managed_code, 
	null pending_io_count, 
	sess.login_time, 
    LEFT (sess.[host_name], 20) AS [host_name], 
	LEFT (ISNULL (sess.program_name, ''), 50) AS [program_name], 
	ISNULL (sess.host_process_id, 0) AS host_process_id, 
    ISNULL (sess.client_version, 0) AS client_version, 
	LEFT (ISNULL (sess.client_interface_name, ''), 30) AS client_interface_name, 
    LEFT (ISNULL (sess.login_name, ''), 30) AS login_name, 
	LEFT (ISNULL (sess.nt_domain, ''), 30) AS nt_domain, 
	LEFT (ISNULL (sess.nt_user_name, ''), 20) AS nt_user_name, 
    ISNULL (conn.net_packet_size, 0) AS net_packet_size, 
	LEFT (ISNULL (conn.client_net_address, ''), 20) AS client_net_address, 
	conn.most_recent_sql_handle, 
    LEFT (sess.status, 15) AS session_status,
    null scheduler_id, 
    sess.group_id as group_id,
    sess.[context_info] as [context_info]
    
  FROM sys.dm_exec_sessions sess 
  /* Join hints are required here to work around bad QO join order/type decisions (ultimately by-design, caused by the lack of accurate DMV card estimates) */
  LEFT OUTER MERGE JOIN sys.dm_exec_requests req  ON sess.session_id = req.session_id
  --LEFT OUTER MERGE JOIN sys.dm_os_tasks tasks ON tasks.session_id = sess.session_id AND tasks.request_id = req.request_id 
  /* The following two DMVs removed due to perf impact, no predicate pushdown (SQLBU #488971) */
  --  LEFT OUTER MERGE JOIN sys.dm_os_workers workers ON tasks.worker_address = workers.worker_address
  --  LEFT OUTER MERGE JOIN sys.dm_os_threads threads ON workers.thread_address = threads.thread_address
  LEFT OUTER MERGE JOIN sys.dm_exec_connections conn on conn.session_id = sess.session_id
  left outer merge join sys.dm_tran_session_transactions sess_tran on sess.session_id=sess_tran.session_id
  LEFT OUTER MERGE JOIN sys.dm_tran_active_transactions reqtrans ON req.transaction_id = reqtrans.transaction_id
  LEFT OUTER MERGE JOIN sys.dm_tran_active_transactions sesstrans ON sesstrans.transaction_id = sess_tran.transaction_id
  WHERE 
    /* Get execution state for all active queries... */
    
    (req.session_id IS not NULL AND (sess.is_user_process = 1 OR req.status  NOT IN ('background', 'sleeping')))
    /* ... and also any head blockers, even though they may not be running a query at the moment. */
    OR (sess.session_id IN (SELECT DISTINCT blocking_session_id FROM sys.dm_exec_requests WHERE blocking_session_id != 0))
  /* redundant due to the use of join hints, but added here to suppress warning message */
  OPTION (FORCE ORDER, recompile)";
                    await OutputMonitorData<ExtendedSqlPerformance>(extended_performance_monitor_query, config, this.ExtendedPerformanceLog);

                    // Gathering wait stats for analysis, because this is the bread and butter of sql performance tuning
                    string wait_stats_query = @"
SELECT CURRENT_TIMESTAMP as reading_time, LEFT (wait_type, 45) AS wait_type, waiting_tasks_count, wait_time_ms, max_wait_time_ms, signal_wait_time_ms
  FROM sys.dm_db_wait_stats
  WHERE waiting_tasks_count > 0 OR wait_time_ms > 0 OR signal_wait_time_ms > 0
  ORDER BY wait_time_ms DESC";
                    await OutputMonitorData<SqlWaitStats>(wait_stats_query, config, this.WaitStatsLog);
                    this.Log.LogInformation($"SQL Extended Peformance information logged at {DateTime.Now}");
                }
            }
            catch (SqlException ex)
            {
                // If we have failed to connect, prep the writing steam and output the full client error
                using var stream_ex = this.ErrorLog.OpenWriteAsync(false);
                using StreamWriter logWriter_ex = new(await stream_ex, System.Text.Encoding.ASCII);
                using CsvWriter csv = new(logWriter_ex, config);
                csv.WriteRecord(new SqlError { ErrorMessage = ex.Message });
                await logWriter_ex.FlushAsync();
                this.Log.LogWarning($"SQL Error logged at {DateTime.Now}");
            }
        }

        public async Task OutputMonitorData<T>(string query, CsvConfiguration config, AppendBlobClient output_log)
        {
            // Setup our streams
            using var stream = output_log.OpenWriteAsync(false);
            using StreamWriter logWriter = new(await stream, System.Text.Encoding.ASCII);
            var results = cn.QueryAsync<T>(query);

            // Write out our records to an append blob
            using CsvWriter csv = new(logWriter, config);
            csv.WriteRecords<T>(await results);
            await logWriter.FlushAsync();
        }
    }
}
