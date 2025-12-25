using System;
using System.Collections.Generic;
using GraphSimulator.Execution.Model;

namespace GraphSimulator.Execution.Services
{
    /// <summary>
    /// Service to resolve operation values based on ValueSource type
    /// </summary>
    public static class ValueSourceService
    {
        /// <summary>
        /// Resolves the values for an operation based on its ValueSource
        /// </summary>
        /// <param name="operation">The operation model</param>
        /// <returns>Resolved values containing IntValues and StringValues</returns>
        public static ResolvedValues ResolveValues(OperationModel operation)
        {
            switch (operation.ValueSource?.ToLower())
            {
                case "node":
                    return ResolveFromNode(operation);
                
                case "date-map":
                    return ResolveFromDateMap(operation);
                
                default:
                    // Default to node behavior
                    return ResolveFromNode(operation);
            }
        }

        /// <summary>
        /// Get values directly from node data (current behavior)
        /// </summary>
        private static ResolvedValues ResolveFromNode(OperationModel operation)
        {
            return new ResolvedValues
            {
                IntValues = operation.IntValues ?? Array.Empty<int>(),
                StringValues = operation.StringValues ?? Array.Empty<string>()
            };
        }

        /// <summary>
        /// Get values from date map based on current date
        /// </summary>
        private static ResolvedValues ResolveFromDateMap(OperationModel operation)
        {
            if (operation.DateMap == null || operation.DateMap.Count == 0)
            {
                // Fall back to default values from node
                return ResolveFromNode(operation);
            }

            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

            // Try to find exact date match
            if (operation.DateMap.TryGetValue(currentDate, out var dateValues))
            {
                return new ResolvedValues
                {
                    IntValues = dateValues.IntValues ?? Array.Empty<int>(),
                    StringValues = dateValues.StringValues ?? Array.Empty<string>()
                };
            }

            // No match found, fall back to default values from node
            return ResolveFromNode(operation);
        }
    }

    /// <summary>
    /// Represents resolved values from a value source
    /// </summary>
    public class ResolvedValues
    {
        public int[] IntValues { get; set; } = Array.Empty<int>();
        public string[] StringValues { get; set; } = Array.Empty<string>();
    }
}
