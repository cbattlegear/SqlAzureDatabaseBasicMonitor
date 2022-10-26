using System;

namespace SqlAzureDatabaseBasicMonitor
{
    public class SqlError
    {
        public DateTime ErrorTimestamp { get; set; }
        public string ErrorMessage { get; set; }
        public SqlError() { ErrorTimestamp = DateTime.Now; }
    }
}
