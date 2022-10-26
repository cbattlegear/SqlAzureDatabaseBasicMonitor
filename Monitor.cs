using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.Storage.Blobs;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Specialized;
using System.IO;
using System.Collections.Generic;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Azure.Storage.Blobs.Models;

namespace SqlAzureDatabaseBasicMonitor
{
    public class Monitor
    {
        internal BlobServiceClient blobServiceClient { get; set; }
        internal BlobContainerClient blobContainerClient { get; set; }
        internal string containerName { get; set; }
        internal string sql_azure_connection_string { get; set; }
        internal string csv_separator { get; set; }
        internal AppendBlobClient performanceLog { get; set; }
        internal AppendBlobClient errorLog { get; set; }

        internal ILogger log { get; set; }

        [FunctionName("Monitor")]
        public void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            this.log = log;
            this.log.LogInformation($"SQL Azure Basic monitor function executed at: {DateTime.Now}");
            string blob_storage_connection_string = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING", EnvironmentVariableTarget.Process);
            this.blobServiceClient = new BlobServiceClient(blob_storage_connection_string);
            this.containerName = Environment.GetEnvironmentVariable("LOGGING_CONTAINER_NAME", EnvironmentVariableTarget.Process);
            this.sql_azure_connection_string = Environment.GetEnvironmentVariable("SQL_AZURE_CONNECTION_STRING", EnvironmentVariableTarget.Process);
            this.csv_separator = Environment.GetEnvironmentVariable("LOG_FILE_CSV_SEPARATOR", EnvironmentVariableTarget.Process);
            PrepareLogFiles().Wait();
            MonitorViaQuery().Wait();
        }

        public async Task PrepareLogFiles()
        {
            try
            {
                this.blobContainerClient = blobServiceClient.GetBlobContainerClient(this.containerName);
                await blobContainerClient.CreateAsync();
            }
            catch (RequestFailedException e)
            {
                this.log.LogDebug(e.Message);
            }

            this.performanceLog = this.blobContainerClient.GetAppendBlobClient(string.Format("performance-{0:yyyy'-'MM'-'dd}.csv", DateTime.Now));
            await this.performanceLog.CreateIfNotExistsAsync();
            this.errorLog = this.blobContainerClient.GetAppendBlobClient(string.Format("error-{0:yyyy'-'MM'-'dd}.csv", DateTime.Now));
            await this.errorLog.CreateIfNotExistsAsync();
        }

        public async Task MonitorViaQuery()
        {
            using MemoryStream stream = new();
            using (StreamWriter logWriter = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    // No headers to prevent headaches reading elsewhere
                    HasHeaderRecord = false,
                    Delimiter = this.csv_separator
                };
                try
                {
                    using SqlConnection cn = new SqlConnection(this.sql_azure_connection_string);
                    await cn.OpenAsync();
                    SqlCommand cmd = new SqlCommand("SELECT * FROM sys.dm_db_resource_stats WHERE end_time > DATEADD(mi, -1, current_timestamp)", cn);
                    SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    List<SqlPerformance> perf = new();
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
                    using (CsvWriter csv = new(logWriter, config))
                    {
                        csv.WriteRecords<SqlPerformance>(perf);
                        await logWriter.FlushAsync();
                        stream.Position = 0;
                        await this.performanceLog.AppendBlockAsync(stream);
                    }
                }
                catch (Exception ex)
                {
                    using (MemoryStream stream_ex = new())
                    using (StreamWriter logWriter_ex = new StreamWriter(stream_ex, System.Text.Encoding.UTF8))
                    using (CsvWriter csv = new(logWriter_ex, config))
                    {
                        csv.WriteRecord<SqlError>(new SqlError { ErrorMessage = ex.Message });
                        await logWriter_ex.FlushAsync();
                        stream_ex.Position = 0;
                        await this.errorLog.AppendBlockAsync(stream_ex);
                    }
                }
            }
        }
    }
}
