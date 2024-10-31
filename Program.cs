using OfficeOpenXml;
using System;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.Serialization;

class Program
{
    static void Main(string[] args)
    {
        ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

        string connectionString = "Data Source=NANDKISHOR;Initial Catalog=PPMS;User ID=sa;Password=root;";
        string excelFilePath = @"C:\D Drive\All Projects\LumaxBawal\ExcelUploadFiles\Config_SparePart.xlsx";
        string tableName = "[PPMS].[dbo].[Config_SparePart]";

        // Load data from Excel file
        DataTable excelDataTable = LoadDataFromExcel(excelFilePath);

        // Initialize error log
        string errorLogFilePath = @"C:\Users\nandk\source\repos\ExcelBulkUpload\ErrorLog.txt";
        StreamWriter errorLogWriter = new StreamWriter(errorLogFilePath);

        try
        {
            // Validate Excel data
            ValidateExcelData(excelDataTable, errorLogWriter);

            // Upload data to SQL Server table
            BulkInsertToSqlTable(connectionString, tableName, excelDataTable, errorLogWriter);

            // Log successful upload message and row count
            LogError(errorLogWriter, $"Spare Part data uploaded successfully! {excelDataTable.Rows.Count} rows updated or inserted");
        }
        catch (Exception ex)
        {
            // Log runtime errors to the error log file
            LogError(errorLogWriter, $"Error: {ex.Message}");
        }
        finally
        {
            // Close the error log writer
            errorLogWriter.Close();
        }
    }

    static DataTable LoadDataFromExcel(string filePath)
    {
        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
            // Check if there is at least one worksheet in the Excel file
            if (package.Workbook.Worksheets.Count > 0)
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0]; // Access the first worksheet

                DataTable dt = new DataTable();

                foreach (var firstRowCell in worksheet.Cells[1, 1, 1, worksheet.Dimension.End.Column])
                {
                    dt.Columns.Add(firstRowCell.Text);
                }

                for (var rowNumber = 2; rowNumber <= worksheet.Dimension.End.Row; rowNumber++)
                {
                    var worksheetRow = worksheet.Cells[rowNumber, 1, rowNumber, worksheet.Dimension.End.Column];
                    var row = dt.Rows.Add();
                    foreach (var cell in worksheetRow)
                    {
                        row[cell.Start.Column - 1] = cell.Text;
                    }
                }

                return dt;
            }
            else
            {
                throw new Exception("No worksheets found in the Excel file.");
            }
        }
    }

    static void ValidateExcelData(DataTable excelDataTable, StreamWriter errorLogWriter)
    {
        // Define expected column names and data types
        Dictionary<string, Type> expectedColumns = new Dictionary<string, Type>
    {
        { "SparePartID", typeof(string) },
        { "MouldID", typeof(string) },
        { "MouldCategory", typeof(string) },
        { "SparePartName", typeof(string) },
        { "SparePartSize", typeof(string) },
        { "SparePartLoc", typeof(string) },
        { "MinQuantity", typeof(string) },
        { "MaxQuantity", typeof(string) },
        { "ReorderLevel", typeof(string) },
        { "SparePartMake", typeof(string) },
        { "LastUpdatedTime", typeof(string) },
        { "LastUpdatedBy", typeof(string) },
    };

        // Check if all expected columns are present
        foreach (var expectedColumn in expectedColumns)
        {
            if (!excelDataTable.Columns.Contains(expectedColumn.Key))
            {
                LogError(errorLogWriter, $"Missing column: {expectedColumn.Key}");
            }
        }

        //Check data types for each column
        foreach (DataColumn column in excelDataTable.Columns)
        {
            if (expectedColumns.ContainsKey(column.ColumnName))
            {
                Type expectedType = expectedColumns[column.ColumnName];
                if (column.DataType != expectedType)
                {
                    LogError(errorLogWriter, $"Invalid data type for column {column.ColumnName}. Expected {expectedType}, found {column.DataType}");
                }
            }
            else
            {
                LogError(errorLogWriter, $"Unexpected column: {column.ColumnName}");
            }
        }

        // Add more specific data validation logic based on your requirements
    }

    static void LogError(StreamWriter errorLogWriter, string errorMessage)
    {
        // Log the error to the error log file
        errorLogWriter.WriteLine($"Note: {errorMessage}");
        Console.WriteLine($"Note: {errorMessage}");
    }

    static void BulkInsertToSqlTable(string connectionString, string tableName, DataTable dataTable, StreamWriter errorLogWriter)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // Truncate the SQL table
            using (SqlCommand truncateCommand = new SqlCommand($"TRUNCATE TABLE {tableName}", connection))
            {
                truncateCommand.ExecuteNonQuery();
            }

            // Perform the SQL Bulk Copy to insert the Excel data
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tableName;

                foreach (DataColumn column in dataTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

                try
                {
                    // WriteToServer with the KeepIdentity option to retain identity column values
                    bulkCopy.WriteToServer(dataTable, DataRowState.Added);

                    // Get the count of rows inserted
                    int totalRowsInserted = GetRowCount(connection, tableName);

                    // Log the total number of rows inserted
                    LogError(errorLogWriter, $"{totalRowsInserted} rows inserted");
                }
                catch (Exception ex)
                {
                    // Log bulk insert errors to the error log file
                    LogError(errorLogWriter, $"Error during bulk insert: {ex.Message}");
                    throw new Exception("Error during bulk insert: " + ex.Message);
                }
            }
        }
    }

    static int GetRowCount(SqlConnection connection, string tableName)
    {
        using (SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {tableName}", connection))
        {
            return (int)countCommand.ExecuteScalar();
        }
    }


}