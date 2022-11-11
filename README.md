# Basic SQL Azure Performance and Connectivity Monitor
This is a small Azure Function to monitor for connectivity errors
to your SQL Azure Database while also monitoring overall performance
metrics of the database. 

## Prerequisites 
 - [Visual Studio 2022 with the Azure Development Tools installed](https://visualstudio.microsoft.com/vs/community/)
 - [An Azure Subscription]()
 - SQL Azure Database to monitor
 - Azure Storage Account

## Setup
1. Clone the repository and open the solution in Visual Studio.
2. [Deploy the Azure Function to your Azure Subscription](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-vs?tabs=in-process#publish-to-azure)
3. Adjust Application settings to match your needs the relevant settings
```
"SQL_AZURE_CONNECTION_STRING": "The Connection string to the database you want to monitor",
"BLOB_STORAGE_CONNECTION_STRING": "The Connection string to the storage account where you want to put your logging data",
"LOGGING_CONTAINER_NAME": "The container name for the logging data",
"LOG_FILE_CSV_SEPARATOR": "The separator for the logging information",
"ENABLE_EXTENDED_SQL_PERFORMANCE_MONITORING": "true or 1 to enable, anything else to disable"
```
4. Verify the data is being stored in your Azure storage account, the file names will be performance-year-month-day.csv and errors-year-month-day.csv. If you enable Extended SQL Performance monitoring you will also extended-performance-year-month-day.csv and wait_stats-year-month-day.csv.

## Data Analysis
You can run through some quick analysis queries and results within the [SQL Performance Serverless Queries Notebook](serverless_queries/SQL_Performance_Serverless_Queries.ipynb). 
This has some prebuilt views ready for the files created and some examples of how to analyze the data.

## To Do Items
 - Monitor Multiple Database
 - Add Query Data Store Queries
 - Clean up Jobs
 - Custom Queries
 - Azure Monitor Output