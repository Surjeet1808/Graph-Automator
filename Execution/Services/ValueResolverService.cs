using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphSimulator.Execution.Model;

namespace GraphSimulator.Execution.Services
{
    /// <summary>
    /// Service for resolving dynamic values from various sources
    /// </summary>
    public class ValueResolverService
    {
        /// <summary>
        /// Resolves all values for an operation based on its dynamic source
        /// </summary>
        public async Task<OperationModel> ResolveOperationAsync(OperationModel operation)
        {
            if (operation.ValueMode == NodeValueMode.Static)
            {
                return operation; // No resolution needed
            }

            if (operation.DynamicSource == null)
            {
                throw new InvalidOperationException(
                    $"❌ DYNAMIC VALUE ERROR\n\n" +
                    $"Operation '{operation.Type}' is set to Dynamic mode but has no DynamicSource configured.\n\n" +
                    $"Please configure a dynamic source or switch to Static mode.");
            }

            // Validate dynamic source configuration
            if (!operation.DynamicSource.Validate(out string? validationError))
            {
                throw new InvalidOperationException(
                    $"❌ DYNAMIC SOURCE CONFIGURATION ERROR\n\n" +
                    $"Operation '{operation.Type}' has invalid dynamic source configuration:\n\n" +
                    $"{validationError}");
            }

            // Resolve based on source type
            Dictionary<string, object> resolvedData;

            try
            {
                resolvedData = operation.DynamicSource.SourceType switch
                {
                    DynamicSourceType.DateBasedArray => ResolveDateBasedArray(operation.DynamicSource),
                    DynamicSourceType.IterationBasedArray => ResolveIterationBasedArray(operation.DynamicSource, operation.CurrentIterationIndex ?? 0),
                    DynamicSourceType.API => await ResolveFromApiAsync(operation.DynamicSource),
                    DynamicSourceType.DateExpression => ResolveDateExpression(operation.DynamicSource),
                    DynamicSourceType.Expression => ResolveExpression(operation.DynamicSource),
                    DynamicSourceType.FileContent => await ResolveFromFileAsync(operation.DynamicSource),
                    _ => throw new NotSupportedException(
                        $"❌ UNSUPPORTED DYNAMIC SOURCE\n\n" +
                        $"Dynamic source type '{operation.DynamicSource.SourceType}' is not yet implemented.")
                };
            }
            catch (DateBasedArrayOutOfRangeException ex)
            {
                // Re-throw to stop execution with alert
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"❌ VALUE RESOLUTION ERROR\n\n" +
                    $"Failed to resolve dynamic values for operation '{operation.Type}':\n\n" +
                    $"{ex.Message}", ex);
            }

            // Map resolved data to operation values
            MapValuesToOperation(operation, resolvedData);

            return operation;
        }

        /// <summary>
        /// Resolves value from date-based array based on days elapsed since start date
        /// </summary>
        private Dictionary<string, object> ResolveDateBasedArray(DynamicValueSource source)
        {
            if (source.DataArray == null || source.DataArray.Length == 0)
            {
                throw new InvalidOperationException("Data array is empty");
            }

            if (string.IsNullOrEmpty(source.StartDate))
            {
                throw new InvalidOperationException("Start date is not configured");
            }

            // Parse start date
            if (!DateTime.TryParse(source.StartDate, out DateTime startDate))
            {
                throw new InvalidOperationException($"Invalid start date format: {source.StartDate}");
            }

            // Calculate days elapsed since start date
            DateTime today = DateTime.Now.Date;
            TimeSpan elapsed = today - startDate.Date;
            int dayIndex = (int)elapsed.TotalDays;

            // Check if index is within array bounds
            if (dayIndex < 0)
            {
                throw new DateBasedArrayOutOfRangeException(
                    $"❌ DATE-BASED ARRAY ERROR\n\n" +
                    $"Current date ({today:yyyy-MM-dd}) is BEFORE the start date ({startDate:yyyy-MM-dd}).\n\n" +
                    $"Days difference: {dayIndex}\n\n" +
                    $"Execution stopped. Please update the start date or wait until the start date arrives.");
            }

            if (dayIndex >= source.DataArray.Length)
            {
                throw new DateBasedArrayOutOfRangeException(
                    $"❌ DATE-BASED ARRAY EXCEEDED\n\n" +
                    $"Current date ({today:yyyy-MM-dd}) is {dayIndex} days after start date ({startDate:yyyy-MM-dd}).\n" +
                    $"Array only has {source.DataArray.Length} items (indices 0-{source.DataArray.Length - 1}).\n\n" +
                    $"Attempted to access index: {dayIndex}\n" +
                    $"Last valid date would be: {startDate.AddDays(source.DataArray.Length - 1):yyyy-MM-dd}\n\n" +
                    $"⚠️ Execution stopped to prevent errors.\n\n" +
                    $"Solutions:\n" +
                    $"• Add more items to the data array\n" +
                    $"• Update the start date to a more recent date\n" +
                    $"• Check if the graph should still be running");
            }

            // Get the item at the calculated index
            string jsonItem = source.DataArray[dayIndex];

            try
            {
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonItem);
                return new Dictionary<string, object> 
                { 
                    { "root", jsonDoc.RootElement.Clone() },
                    { "dayIndex", dayIndex },
                    { "startDate", startDate },
                    { "currentDate", today }
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse JSON at day index {dayIndex} (date: {today:yyyy-MM-dd}): {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves value from array based on iteration index
        /// </summary>
        private Dictionary<string, object> ResolveIterationBasedArray(DynamicValueSource source, int iterationIndex)
        {
            if (source.DataArray == null || source.DataArray.Length == 0)
            {
                throw new InvalidOperationException("Data array is empty");
            }

            // Use modulo to wrap around if iterations exceed array length
            int index = iterationIndex % source.DataArray.Length;
            string jsonItem = source.DataArray[index];

            try
            {
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonItem);
                return new Dictionary<string, object> 
                { 
                    { "root", jsonDoc.RootElement.Clone() },
                    { "iterationIndex", iterationIndex },
                    { "arrayIndex", index }
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse JSON at iteration index {iterationIndex} (array index {index}): {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves values from API endpoint (future implementation)
        /// </summary>
        private async Task<Dictionary<string, object>> ResolveFromApiAsync(DynamicValueSource source)
        {
            throw new NotImplementedException("API source type is not yet implemented. Please use DateBasedArray for now.");
        }

        /// <summary>
        /// Resolves values from date expression (future implementation)
        /// </summary>
        private Dictionary<string, object> ResolveDateExpression(DynamicValueSource source)
        {
            throw new NotImplementedException("DateExpression source type is not yet implemented. Please use DateBasedArray for now.");
        }

        /// <summary>
        /// Resolves values from C# expression (future implementation)
        /// </summary>
        private Dictionary<string, object> ResolveExpression(DynamicValueSource source)
        {
            throw new NotImplementedException("Expression source type is not yet implemented. Please use DateBasedArray for now.");
        }

        /// <summary>
        /// Resolves values from file content (future implementation)
        /// </summary>
        private async Task<Dictionary<string, object>> ResolveFromFileAsync(DynamicValueSource source)
        {
            throw new NotImplementedException("FileContent source type is not yet implemented. Please use DateBasedArray for now.");
        }

        /// <summary>
        /// Maps resolved data to operation IntValues and StringValues using ValueMappings
        /// </summary>
        private void MapValuesToOperation(OperationModel operation, Dictionary<string, object> resolvedData)
        {
            if (operation.DynamicSource?.ValueMappings == null || operation.DynamicSource.ValueMappings.Count == 0)
            {
                throw new InvalidOperationException(
                    "Value mappings are required to map resolved data to operation values");
            }

            var intValues = new List<int>();
            var stringValues = new List<string>();

            foreach (var mapping in operation.DynamicSource.ValueMappings)
            {
                var target = mapping.Key;    // e.g., "IntValues[0]"
                var jsonPath = mapping.Value; // e.g., "$.x"

                var value = ExtractValueFromJsonPath(resolvedData, jsonPath);

                if (value == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to extract value from path '{jsonPath}'. The path may not exist in the resolved data.");
                }

                // Parse target and assign value
                if (target.StartsWith("IntValues["))
                {
                    int intIndex = ExtractArrayIndex(target);
                    while (intValues.Count <= intIndex)
                    {
                        intValues.Add(0);
                    }
                    intValues[intIndex] = Convert.ToInt32(value);
                }
                else if (target.StartsWith("StringValues["))
                {
                    int strIndex = ExtractArrayIndex(target);
                    while (stringValues.Count <= strIndex)
                    {
                        stringValues.Add(string.Empty);
                    }
                    stringValues[strIndex] = Convert.ToString(value) ?? string.Empty;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Invalid mapping target: {target}. Must be 'IntValues[index]' or 'StringValues[index]'");
                }
            }

            operation.IntValues = intValues.ToArray();
            operation.StringValues = stringValues.ToArray();
        }

        /// <summary>
        /// Extracts value from resolved data using JSONPath-like syntax
        /// </summary>
        private object? ExtractValueFromJsonPath(Dictionary<string, object> data, string jsonPath)
        {
            if (!data.ContainsKey("root"))
            {
                return null;
            }

            var element = (System.Text.Json.JsonElement)data["root"];

            // Remove leading $. if present
            var path = jsonPath.TrimStart('$', '.');
            
            if (string.IsNullOrEmpty(path))
            {
                return element;
            }

            var parts = path.Split('.');

            foreach (var part in parts)
            {
                if (element.ValueKind == System.Text.Json.JsonValueKind.Object && 
                    element.TryGetProperty(part, out var nextElement))
                {
                    element = nextElement;
                }
                else
                {
                    return null;
                }
            }

            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out int intVal) ? intVal : element.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Extracts array index from target string like "IntValues[0]"
        /// </summary>
        private int ExtractArrayIndex(string target)
        {
            var start = target.IndexOf('[') + 1;
            var end = target.IndexOf(']');
            
            if (start <= 0 || end <= start)
            {
                throw new InvalidOperationException($"Invalid array index format in target: {target}");
            }

            string indexStr = target.Substring(start, end - start);
            
            if (!int.TryParse(indexStr, out int index))
            {
                throw new InvalidOperationException($"Invalid array index in target: {target}");
            }

            return index;
        }
    }

    /// <summary>
    /// Custom exception for date-based array out of range errors
    /// This should stop execution with an alert
    /// </summary>
    public class DateBasedArrayOutOfRangeException : Exception
    {
        public DateBasedArrayOutOfRangeException(string message) : base(message)
        {
        }
    }
}
