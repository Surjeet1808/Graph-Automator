using System;
using System.Threading.Tasks;
using GraphSimulator.Execution.Model;
using GraphSimulator.Execution.Common;
using System.Linq;
using System.Collections.Generic;

namespace GraphSimulator.Execution.Controller
{
    /// <summary>
    /// Executes automation operations defined in OperationModel
    /// </summary>
    public class Execute
    {
        // Track graph execution stack to detect circular dependencies
        private readonly HashSet<string> _executionStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Execute a single operation
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        public async Task ExecuteOperationAsync(OperationModel operation)
        {
            // Validate operation
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (string.IsNullOrEmpty(operation.Type))
            {
                throw new ArgumentException("Operation type cannot be null or empty", nameof(operation));
            }

            // Skip if disabled
            if (!operation.Enabled)
            {
                return;
            }

            // Delay before execution
            if (operation.DelayBefore > 0)
            {
                await Task.Delay(operation.DelayBefore);
            }

            // Execute operation based on frequency (repeat count)
            int frequency = operation.Frequency > 0 ? operation.Frequency : 1;
            
            for (int i = 0; i < frequency; i++)
            {
                await ExecuteSingleOperationAsync(operation);
                
                // Add small delay between repetitions if frequency > 1
                if (i < frequency - 1 && frequency > 1)
                {
                    await Task.Delay(50); // 50ms delay between repetitions
                }
            }

            // Delay after execution
            if (operation.DelayAfter > 0)
            {
                await Task.Delay(operation.DelayAfter);
            }
        }

        /// <summary>
        /// Execute a single operation once (without frequency handling)
        /// </summary>
        private async Task ExecuteSingleOperationAsync(OperationModel operation)
        {
            // Execute based on type
            try
            {
                switch (operation.Type.ToLower())
                {
                    case "mouse_left_click":
                        ValidateIntValues(operation, 2, "mouse_left_click requires x and y coordinates");
                        WpfInputHelper.ClickAt(operation.IntValues[0], operation.IntValues[1]);
                        break;

                    case "mouse_right_click":
                        ValidateIntValues(operation, 2, "mouse_right_click requires x and y coordinates");
                        WpfInputHelper.RightClickAt(operation.IntValues[0], operation.IntValues[1]);
                        break;

                    case "mouse_move":
                        ValidateIntValues(operation, 2, "mouse_move requires x and y coordinates");
                        WpfInputHelper.MoveTo(operation.IntValues[0], operation.IntValues[1]);
                        break;

                    case "scroll_up":
                        int upAmount = operation.IntValues.Length > 0 ? operation.IntValues[0] : 120;
                        WpfInputHelper.Scroll(upAmount);
                        break;

                    case "scroll_down":
                        int downAmount = operation.IntValues.Length > 0 ? operation.IntValues[0] : 120;
                        WpfInputHelper.Scroll(-downAmount);
                        break;

                    case "key_press":
                        ValidateIntValues(operation, 1, "key_press requires a key code");
                        WpfInputHelper.PressKey((byte)operation.IntValues[0]);
                        break;

                    case "key_down":
                        ValidateIntValues(operation, 1, "key_down requires a key code");
                        WpfInputHelper.KeyDown((byte)operation.IntValues[0]);
                        break;

                    case "key_up":
                        ValidateIntValues(operation, 1, "key_up requires a key code");
                        WpfInputHelper.KeyUp((byte)operation.IntValues[0]);
                        break;

                    case "type_text":
                        ValidateStringValues(operation, 1, "type_text requires text to type");
                        WpfInputHelper.TypeText(operation.StringValues[0]);
                        break;

                    case "wait":
                        ValidateIntValues(operation, 1, "wait requires duration in milliseconds");
                        await Task.Delay(operation.IntValues[0]);
                        break;

                    case "graph":
                        await ExecuteGraphOperationAsync(operation);
                        break;

                    case "custom_code":
                        if (string.IsNullOrEmpty(operation.CustomCode))
                        {
                            throw new ArgumentException("CustomCode cannot be empty for custom_code operation");
                        }
                        // Execute custom code (implement as needed)
                        throw new NotImplementedException("Custom code execution not yet implemented");

                    default:
                        throw new NotSupportedException($"Operation type '{operation.Type}' is not supported.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to execute operation '{operation.Type}': {ex.Message}", 
                    ex
                );
            }
        }

        /// <summary>
        /// Execute a graph file as a nested operation
        /// </summary>
        private async Task ExecuteGraphOperationAsync(OperationModel operation)
        {
            // Validate graph file path
            if (string.IsNullOrEmpty(operation.GraphFilePath))
            {
                throw new ArgumentException("GraphFilePath is required for graph operation. Please select a valid graph file.");
            }

            if (!System.IO.File.Exists(operation.GraphFilePath))
            {
                throw new System.IO.FileNotFoundException(
                    $"Graph file not found: {operation.GraphFilePath}\n\nPlease verify the file path is correct and the file exists.");
            }

            // Get the absolute path to detect circular dependencies
            string absolutePath = System.IO.Path.GetFullPath(operation.GraphFilePath);

            // Check for circular dependency
            if (_executionStack.Contains(absolutePath))
            {
                var stackList = string.Join("\n  → ", _executionStack);
                throw new InvalidOperationException(
                    $"⚠️ CIRCULAR DEPENDENCY DETECTED!\n\n" +
                    $"The graph file is already being executed in the current execution chain:\n\n" +
                    $"Execution Chain:\n  → {stackList}\n  → {absolutePath} (CIRCULAR!)\n\n" +
                    $"This would cause an infinite loop. Please check your graph references and remove the circular dependency.\n\n" +
                    $"Common causes:\n" +
                    $"• Graph A calls Graph B, and Graph B calls Graph A\n" +
                    $"• Graph A calls itself directly\n" +
                    $"• Graph A → B → C → A (circular chain)");
            }

            // Add to execution stack
            _executionStack.Add(absolutePath);

            try
            {
                // Load the graph file
                var fileService = new GraphSimulator.Services.FileService();
                var graph = await fileService.LoadGraphAsync(operation.GraphFilePath);

                if (graph == null)
                {
                    throw new InvalidOperationException($"Failed to load graph from file: {operation.GraphFilePath}\n\nThe file may be corrupted or in an invalid format.");
                }

                if (graph.Nodes == null || graph.Nodes.Count == 0)
                {
                    throw new InvalidOperationException($"Graph file contains no nodes: {operation.GraphFilePath}\n\nPlease ensure the graph has at least one operation node.");
                }

                // Parse operations from the loaded graph
                var nestedOperations = new List<OperationModel>();
                
                foreach (var node in graph.Nodes.OrderBy(n => n.Y).ThenBy(n => n.X))
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(node.JsonData))
                            continue;

                        var nestedOp = System.Text.Json.JsonSerializer.Deserialize<OperationModel>(
                            node.JsonData,
                            new System.Text.Json.JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            }
                        );

                        if (nestedOp != null && !string.IsNullOrEmpty(nestedOp.Type))
                        {
                            nestedOperations.Add(nestedOp);
                        }
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        throw new InvalidOperationException(
                            $"Invalid JSON data in node '{node.Name}' (ID: {node.Id})\n\n" +
                            $"Error: {jsonEx.Message}\n\n" +
                            "Please verify the node's operation data is correctly formatted.");
                    }
                }

                if (nestedOperations.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No valid operations found in graph: {operation.GraphFilePath}\n\n" +
                        "Please ensure the graph contains at least one properly configured operation node.");
                }

                // Execute all operations from the nested graph
                await ExecuteOperationsAsync(nestedOperations);
            }
            catch (InvalidOperationException)
            {
                // Re-throw our custom exceptions as-is
                throw;
            }
            catch (System.IO.FileNotFoundException)
            {
                // Re-throw file not found as-is
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error executing graph file: {operation.GraphFilePath}\n\n" +
                    $"Error: {ex.Message}", 
                    ex
                );
            }
            finally
            {
                // Always remove from execution stack when done (even on error)
                _executionStack.Remove(absolutePath);
            }
        }

        /// <summary>
        /// Execute multiple operations in sequence
        /// </summary>
        /// <param name="operations">Array of operations to execute</param>
        public async Task ExecuteOperationsAsync(params OperationModel[] operations)
        {
            if (operations == null || operations.Length == 0)
            {
                return;
            }

            // Sort by priority (lower priority value = execute first)
            var sortedOps = operations.OrderBy(op => op.Priority).ToArray();

            foreach (var operation in sortedOps)
            {
                await ExecuteOperationAsync(operation);
            }
        }

        /// <summary>
        /// Execute operations from a list
        /// </summary>
        public async Task ExecuteOperationsAsync(List<OperationModel> operations)
        {
            if (operations == null || operations.Count == 0)
            {
                return;
            }

            await ExecuteOperationsAsync(operations.ToArray());
        }

        private void ValidateIntValues(OperationModel operation, int minCount, string errorMessage)
        {
            if (operation.IntValues == null || operation.IntValues.Length < minCount)
            {
                throw new ArgumentException(errorMessage);
            }
        }

        private void ValidateStringValues(OperationModel operation, int minCount, string errorMessage)
        {
            if (operation.StringValues == null || operation.StringValues.Length < minCount)
            {
                throw new ArgumentException(errorMessage);
            }
        }
    }
}