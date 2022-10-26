using Microsoft.Data.SqlClient;
using System.Data;
namespace SqlAzureDatabaseBasicMonitor
{
    // Directly referencing https://makolyte.com/csharp-mapping-nullable-columns-with-sqldatareader/
    // Added overload for column index
    public static class SqlDataReaderExtensions
    {
        public static T Get<T>(this SqlDataReader reader, string columnName)
        {
            if (reader.IsDBNull(columnName))
                return default;
            return reader.GetFieldValue<T>(columnName);
        }

        public static T Get<T>(this SqlDataReader reader, int columnIndex)
        {
            if (reader.IsDBNull(columnIndex))
                return default;
            return reader.GetFieldValue<T>(columnIndex);
        }
    }
}

