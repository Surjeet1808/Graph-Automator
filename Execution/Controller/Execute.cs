using System;
using System.Threading.Tasks;
using GraphSimulator.Execution.Model;
using GraphSimulator.Execution.Common;
using GraphSimulator.Execution.Services;
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
        
        // Track validated execution plan
        private readonly List<string> _validatedExecutionPlan = new List<string>();

        /// <summary>
        /// Validate the entire operation tree before execution
        /// </summary>
        public async Task ValidateOperationTreeAsync(List<OperationModel> operations)
        {
            _validatedExecutionPlan.Clear();
            _executionStack.Clear();
            
            foreach (var operation in operations)
            {
                await ValidateOperationAsync(operation);
            }
        }

        /// <summary>
        /// Recursively validate an operation and its nested graphs
        /// </summary>
        private async Task ValidateOperationAsync(OperationModel operation)
        {
            if (operation == null || string.IsNullOrEmpty(operation.Type))
                return;

            // Skip disabled operations
            if (!operation.Enabled)
                return;

            // If this is a graph operation, validate the nested graph
            if (operation.Type.ToLower() == "graph")
            {
                await ValidateGraphOperationAsync(operation);
            }
        }

        /// <summary>
        /// Validate a graph file and all its nested operations
        /// </summary>
        private async Task ValidateGraphOperationAsync(OperationModel operation)
        {
            // Validate graph file path
            if (string.IsNullOrEmpty(operation.GraphFilePath))
            {
                throw new ArgumentException(
                    $"❌ VALIDATION ERROR\n\n" +
                    $"Graph operation is missing file path.\n\n" +
                    $"Please select a valid graph file for this operation.");
            }

            if (!System.IO.File.Exists(operation.GraphFilePath))
            {
                throw new System.IO.FileNotFoundException(
                    $"❌ VALIDATION ERROR - FILE NOT FOUND\n\n" +
                    $"Graph file not found: {operation.GraphFilePath}\n\n" +
                    $"Please verify the file path is correct and the file exists.");
            }

            // Get the absolute path to detect circular dependencies
            string absolutePath = System.IO.Path.GetFullPath(operation.GraphFilePath);

            // Check for circular dependency
            if (_executionStack.Contains(absolutePath))
            {
                var stackList = string.Join("\n  → ", _executionStack);
                throw new InvalidOperationException(
                    $"❌ VALIDATION ERROR - CIRCULAR DEPENDENCY DETECTED!\n\n" +
                    $"The graph file is already in the execution chain:\n\n" +
                    $"Execution Chain:\n  → {stackList}\n  → {absolutePath} (CIRCULAR!)\n\n" +
                    $"This would cause an infinite loop. Please check your graph references and remove the circular dependency.\n\n" +
                    $"Common causes:\n" +
                    $"• Graph A calls Graph B, and Graph B calls Graph A\n" +
                    $"• Graph A calls itself directly\n" +
                    $"• Graph A → B → C → A (circular chain)");
            }

            // Add to execution stack for validation
            _executionStack.Add(absolutePath);
            _validatedExecutionPlan.Add($"Graph: {System.IO.Path.GetFileName(absolutePath)}");

            try
            {
                // Load the graph file
                var fileService = new GraphSimulator.Services.FileService();
                var graph = await fileService.LoadGraphAsync(operation.GraphFilePath);

                if (graph == null)
                {
                    throw new InvalidOperationException(
                        $"❌ VALIDATION ERROR - INVALID GRAPH FILE\n\n" +
                        $"Failed to load graph from file: {operation.GraphFilePath}\n\n" +
                        $"The file may be corrupted or in an invalid format.");
                }

                if (graph.Nodes == null || graph.Nodes.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"❌ VALIDATION ERROR - EMPTY GRAPH\n\n" +
                        $"Graph file contains no nodes: {operation.GraphFilePath}\n\n" +
                        $"Please ensure the graph has at least one operation node.");
                }

                // Find start node in nested graph
                var startNode = graph.Nodes.FirstOrDefault(n => n.Type?.ToLower() == "start");
                if (startNode == null)
                {
                    throw new InvalidOperationException(
                        $"❌ VALIDATION ERROR - NO START NODE\n\n" +
                        $"Graph file has no 'start' node: {operation.GraphFilePath}\n\n" +
                        $"Each graph must have exactly one start node to define the execution flow.");
                }

                // Parse operations following the link chain
                var nestedOperations = ParseGraphOperationsByLinks(graph, startNode);

                if (nestedOperations.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"❌ VALIDATION ERROR - NO VALID OPERATIONS\n\n" +
                        $"No valid operations found in graph: {operation.GraphFilePath}\n\n" +
                        $"Please ensure the graph contains properly configured operation nodes linked from the start node.");
                }

                // Recursively validate nested operations
                foreach (var nestedOp in nestedOperations)
                {
                    await ValidateOperationAsync(nestedOp);
                }
            }
            finally
            {
                // Remove from stack after validation
                _executionStack.Remove(absolutePath);
            }
        }

        /// <summary>
        /// Parse graph operations by following node links from start node
        /// </summary>
        private List<OperationModel> ParseGraphOperationsByLinks(GraphSimulator.Models.Graph graph, GraphSimulator.Models.Node startNode)
        {
            var operations = new List<OperationModel>();
            var visitedNodes = new HashSet<Guid>();
            var currentNode = startNode;

            while (currentNode != null)
            {
                // Check for cycles
                if (visitedNodes.Contains(currentNode.Id))
                {
                    throw new InvalidOperationException(
                        $"❌ VALIDATION ERROR - CIRCULAR EXECUTION PATH\n\n" +
                        $"Node '{currentNode.Name}' has already been visited in the execution chain.\n\n" +
                        $"The graph contains a loop. Please remove circular links between nodes.");
                }

                visitedNodes.Add(currentNode.Id);

                // Skip start node itself
                if (currentNode.Type?.ToLower() != "start")
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(currentNode.JsonData))
                        {
                            var operation = System.Text.Json.JsonSerializer.Deserialize<OperationModel>(
                                currentNode.JsonData,
                                new System.Text.Json.JsonSerializerOptions 
                                { 
                                    PropertyNameCaseInsensitive = true 
                                }
                            );

                            if (operation != null && !string.IsNullOrEmpty(operation.Type))
                            {
                                operations.Add(operation);
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        throw new InvalidOperationException(
                            $"❌ VALIDATION ERROR - INVALID NODE DATA\n\n" +
                            $"Invalid JSON data in node '{currentNode.Name}' (ID: {currentNode.Id})\n\n" +
                            $"Error: {jsonEx.Message}\n\n" +
                            $"Please verify the node's operation data is correctly formatted.");
                    }
                }

                // Find next node by following outgoing link
                var outgoingLink = graph.Links?.FirstOrDefault(l => l.SourceNodeId == currentNode.Id);
                
                if (outgoingLink != null)
                {
                    currentNode = graph.Nodes.FirstOrDefault(n => n.Id == outgoingLink.TargetNodeId);
                }
                else
                {
                    // No outgoing link - end of chain
                    break;
                }
            }

            return operations;
        }

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
                // Set iteration index for iteration-based dynamic sources
                operation.CurrentIterationIndex = i;
                
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
            // Resolve dynamic values if needed
            if (operation.ValueMode == NodeValueMode.Dynamic)
            {
                var resolver = new ValueResolverService();
                operation = await resolver.ResolveOperationAsync(operation);
            }

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

                    case "scroll_left":
                        int leftAmount = operation.IntValues.Length > 0 ? operation.IntValues[0] : 120;
                        WpfInputHelper.ScrollHorizontal(-leftAmount);
                        break;

                    case "scroll_right":
                        int rightAmount = operation.IntValues.Length > 0 ? operation.IntValues[0] : 120;
                        WpfInputHelper.ScrollHorizontal(rightAmount);
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

                    case "start":
                        // Start node is just a marker - do nothing
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
                // Log nested graph execution start
                System.Diagnostics.Debug.WriteLine($"[Graph Execution] Starting nested graph: {operation.GraphFilePath}");
                
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

                // Find start node in nested graph
                var startNode = graph.Nodes.FirstOrDefault(n => n.Type?.ToLower() == "start");
                if (startNode == null)
                {
                    throw new InvalidOperationException(
                        $"Graph file has no 'start' node: {operation.GraphFilePath}\n\n" +
                        $"Each graph must have exactly one start node to define the execution flow.");
                }

                // Parse operations following node links from start node
                var nestedOperations = ParseGraphOperationsByLinks(graph, startNode);

                if (nestedOperations.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No valid operations found in graph: {operation.GraphFilePath}\n\n" +
                        "Please ensure the graph contains properly configured operation nodes linked from the start node.");
                }

                // Log operation count
                System.Diagnostics.Debug.WriteLine($"[Graph Execution] Found {nestedOperations.Count} operations in nested graph");

                // Execute all operations from the nested graph synchronously
                // This ensures each operation completes before moving to the next
                await ExecuteOperationsAsync(nestedOperations);
                
                System.Diagnostics.Debug.WriteLine($"[Graph Execution] Completed nested graph: {operation.GraphFilePath}");
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
                    $"Error executing nested graph file: {operation.GraphFilePath}\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Stack trace: {ex.StackTrace}", 
                    ex
                );
            }
            finally
            {
                // Always remove from execution stack when done (even on error)
                _executionStack.Remove(absolutePath);
                System.Diagnostics.Debug.WriteLine($"[Graph Execution] Removed from execution stack: {absolutePath}");
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