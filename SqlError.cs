using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlAzureDatabaseBasicMonitor
{
    public class SqlError
    {
        public DateTime ErrorTimestamp { get; set; }
        public string ErrorMessage { get; set; }
        public SqlError() { ErrorTimestamp = DateTime.Now; }
    }
}
