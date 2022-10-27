using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace SqlAzureDatabaseBasicMonitor
{
    public class Monitor
    {
        internal BlobServiceClient BlobServiceClient { get; set; }
        internal BlobContainerClient BlobContainerClient { get; set; }
        internal string ContainerName { get; set; }
        internal string SqlAzureConnectionString { get; set; }
        internal string CsvSeparator { get; set; }
        internal AppendBlobClient PerformanceLog { get; set; }
        internal AppendBlobClient ErrorLog { get; set; }

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
            // Set up our output streams
            using var stream = this.PerformanceLog.OpenWriteAsync(false);
            using StreamWriter logWriter = new(await stream, System.Text.Encoding.ASCII);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                // No headers to prevent headaches reading elsewhere
                HasHeaderRecord = false,
                Delimiter = this.CsvSeparator
            };
            try
            {
                // Attempt to connect to SQL
                using SqlConnection cn = new SqlConnection(this.SqlAzureConnectionString);
                await cn.OpenAsync();

                // With a valid connection, get the last minute of performance data (should be 4 entries)
                SqlCommand cmd = new("SELECT * FROM sys.dm_db_resource_stats WHERE end_time > DATEADD(mi, -1, current_timestamp) ORDER BY end_time", cn);
                SqlDataReader reader = await cmd.ExecuteReaderAsync();
                List<SqlPerformance> perf = new();

                // I probably made this overly complex, using a class to get all the performance data
                // See the SqlDataReaderExtensions.cs file to understand the Get process for null data
                while (reader.Read())
                {
                    perf.Add(new SqlPerformance
                    {
                        EndTime = reader.Get<DateTime?>(0),
                        AvgCpuPercent = reader.Get<decimal?>(1),
                        AvgDataIoPercent = reader.Get<decimal?>(2),
                        AvgLogWritePercent = reader.Get<decimal?>(3),
                        AvgMemoryUsagePercent = reader.Get<decimal?>(4),
                        XtpStoragePercent = reader.Get<decimal?>(5),
                        MaxWorkerPercent = reader.Get<decimal?>(6),
                        MaxSessionPercent = reader.Get<decimal?>(7),
                        DtuLimit = reader.Get<int?>(8),
                        AvgLoginRatePercent = reader.Get<decimal?>(9),
                        AvgInstanceCpuPercent = reader.Get<decimal?>(10),
                        AvgInstanceMemoryPercent = reader.Get<decimal?>(11),
                        CpuLimit = reader.Get<int?>(12),
                        ReplicaRole = reader.Get<int?>(13)
                    });
                }
                // Write out our records to an append blob
                using CsvWriter csv = new(logWriter, config);
                csv.WriteRecords<SqlPerformance>(perf);
                await logWriter.FlushAsync();
                this.Log.LogInformation($"SQL Peformance information logged at {DateTime.Now}");
            }
            catch (SqlException ex)
            {
                // If we have failed to connect, prep the writing steam and output the full client error
                using var stream_ex = this.PerformanceLog.OpenWriteAsync(false);
                using StreamWriter logWriter_ex = new(await stream_ex, System.Text.Encoding.ASCII);
                using CsvWriter csv = new(logWriter_ex, config);
                csv.WriteRecord(new SqlError { ErrorMessage = ex.Message });
                await logWriter_ex.FlushAsync();
                this.Log.LogWarning($"SQL Error logged at {DateTime.Now}");
            }
        }
    }
}
