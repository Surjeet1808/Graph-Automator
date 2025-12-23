using System;
using System.Collections.Generic;

namespace GraphSimulator.Execution.Model
{
    /// <summary>
    /// Configuration for dynamic value sources
    /// </summary>
    public class DynamicValueSource
    {
        /// <summary>
        /// Type of dynamic source
        /// </summary>
        public DynamicSourceType SourceType { get; set; }

        // ===== Date-Based Array Configuration =====
        
        /// <summary>
        /// Start date for date-based array (format: yyyy-MM-dd)
        /// </summary>
        public string? StartDate { get; set; }

        /// <summary>
        /// Array of data items (JSON strings). Each item represents values for one day.
        /// </summary>
        public string[]? DataArray { get; set; }

        // ===== Value Mapping Configuration =====
        
        /// <summary>
        /// Maps resolved data to operation values
        /// Example: { "IntValues[0]": "$.x", "IntValues[1]": "$.y", "StringValues[0]": "$.text" }
        /// </summary>
        public Dictionary<string, string>? ValueMappings { get; set; }

        // ===== API Configuration (for future use) =====
        
        /// <summary>
        /// API endpoint URL
        /// </summary>
        public string? ApiEndpoint { get; set; }

        /// <summary>
        /// HTTP method (GET, POST, etc.)
        /// </summary>
        public string? ApiMethod { get; set; }

        /// <summary>
        /// API request headers
        /// </summary>
        public Dictionary<string, string>? ApiHeaders { get; set; }

        /// <summary>
        /// API request body
        /// </summary>
        public string? ApiBody { get; set; }

        // ===== Expression Configuration (for future use) =====
        
        /// <summary>
        /// C# expression to evaluate
        /// </summary>
        public string? Expression { get; set; }

        // ===== File Configuration (for future use) =====
        
        /// <summary>
        /// File path to read data from
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// File format (json, csv, txt)
        /// </summary>
        public string? FileFormat { get; set; }

        /// <summary>
        /// Validates the configuration based on source type
        /// </summary>
        public bool Validate(out string? errorMessage)
        {
            errorMessage = null;

            switch (SourceType)
            {
                case DynamicSourceType.DateBasedArray:
                    if (string.IsNullOrEmpty(StartDate))
                    {
                        errorMessage = "Start date is required for date-based array";
                        return false;
                    }

                    if (!DateTime.TryParse(StartDate, out _))
                    {
                        errorMessage = $"Invalid start date format: {StartDate}. Use yyyy-MM-dd format.";
                        return false;
                    }

                    if (DataArray == null || DataArray.Length == 0)
                    {
                        errorMessage = "Data array cannot be empty for date-based array";
                        return false;
                    }

                    // Validate each JSON item in the array
                    for (int i = 0; i < DataArray.Length; i++)
                    {
                        try
                        {
                            System.Text.Json.JsonDocument.Parse(DataArray[i]);
                        }
                        catch (Exception ex)
                        {
                            errorMessage = $"Invalid JSON at array index {i}: {ex.Message}";
                            return false;
                        }
                    }
                    break;

                case DynamicSourceType.API:
                    if (string.IsNullOrEmpty(ApiEndpoint))
                    {
                        errorMessage = "API endpoint is required for API source type";
                        return false;
                    }
                    break;

                case DynamicSourceType.FileContent:
                    if (string.IsNullOrEmpty(FilePath))
                    {
                        errorMessage = "File path is required for file content source type";
                        return false;
                    }
                    break;
            }

            return true;
        }
    }
}
