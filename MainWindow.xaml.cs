using GraphSimulator.ViewModels;
using GraphSimulator.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GraphSimulator.Execution.Controller;
using GraphSimulator.Execution.Model;
using GraphSimulator.Execution.Common;
using System.Runtime.InteropServices;



namespace GraphSimulator
{
    public partial class MainWindow : Window
    {
        // Windows API for getting cursor position
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        
        private MainViewModel? _viewModel;
        private Execute _executor = new Execute();
        private System.Windows.Threading.DispatcherTimer? _mousePositionTimer;
        private bool _isTrackingMousePosition = false;
    private int _scrollMeasureAmount = 0;
    private int _scrollMeasureHorizontalAmount = 0;
    private bool _isScrollMeasureActive = false;
        
        public MainWindow()
        {
            InitializeComponent();
            // Create and assign the MainViewModel
            _viewModel = new GraphSimulator.ViewModels.MainViewModel();
            DataContext = _viewModel;

            // ...existing code...
            // Subscribe to execution event
            _viewModel.ExecutionRequested += OnExecutionRequested;
            
            // Initialize mouse position timer
            InitializeMousePositionTracking();

            // Handle command-line file opening and auto-execution
            this.Loaded += async (s, e) =>
            {
                if (!string.IsNullOrEmpty(App.FileToOpen))
                {
                    await _viewModel.LoadGraphFromFileAsync(App.FileToOpen);
                    
                    if (App.AutoExecute)
                    {
                        // Auto-execute the graph
                        _viewModel.StartExecutionCommand.Execute(null);
                    }
                }
            };

            // Render the initial graph on the canvas control
            try
            {
                GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);

                // Re-render when nodes or links change
                _viewModel.CurrentGraph.Nodes.CollectionChanged += (s, e) =>
                {
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                };

                _viewModel.CurrentGraph.Links.CollectionChanged += (s, e) =>
                {
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                };
                
                // Wire canvas events to ViewModel commands
                // Left-click selects node only; do NOT open context menu here
                GraphCanvasControl.NodeSelected += (node) => 
                {
                    _viewModel.SelectNodeCommand.Execute(node);
                    // Track last selected node for outline persistence
                    GraphCanvasControl.LastSelectedNode = node;
                    // Ensure canvas preview and selection visuals update immediately
                    GraphCanvasControl.SelectedNodeEditModel = _viewModel.SelectedNodeEdit;
                    GraphCanvasControl.SelectedNodeId = _viewModel.SelectedNode?.Id;
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                };

                // Right-click on a node should open the node context menu
                GraphCanvasControl.NodeRightClicked += (node) =>
                {
                    _viewModel.SelectNodeCommand.Execute(node);
                    // Ensure visuals update immediately for context operations
                    GraphCanvasControl.SelectedNodeEditModel = _viewModel.SelectedNodeEdit;
                    GraphCanvasControl.SelectedNodeId = _viewModel.SelectedNode?.Id;
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    ShowNodeContextMenu(node);
                };

                GraphCanvasControl.LinkSelected += (link) => 
                {
                    _viewModel.SelectLinkCommand.Execute(link);
                    // Force immediate visual update for link selection
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                };

                GraphCanvasControl.LinkRightClicked += (link) =>
                {
                    _viewModel.SelectLinkCommand.Execute(link);
                    // Update visuals and then show toolbar
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    ShowLinkToolbar(link);
                };

                GraphCanvasControl.DeselectRequested += () => 
                {
                    // If in link creation mode, cancel it
                    if (_viewModel.IsLinkCreationMode)
                    {
                        _viewModel.CancelLinkCreationCommand.Execute(null);
                        GraphCanvasControl.LinkCreationSourceNodeId = null;
                        GraphCanvasControl.IsInLinkCreationMode = false;
                        GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    }
                    else
                    {
                        // Otherwise, deselect all nodes
                        _viewModel.DeselectAllCommand.Execute(null);
                        GraphCanvasControl.LastSelectedNode = null;
                        LinkToolbarPopup.IsOpen = false;
                        // Clear canvas preview state
                        GraphCanvasControl.SelectedNodeEditModel = null;
                        GraphCanvasControl.SelectedNodeId = null;
                        // Re-render to hide ports
                        GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    }
                };
                GraphCanvasControl.ZoomChanged += (z) => _viewModel.CanvasZoom = z;
                GraphCanvasControl.DoubleClickedOnCanvas += (pt) =>
                {
                    // Forward double-click to ViewModel to add node
                    _viewModel.AddNodeAtCommand.Execute(pt);
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                };
                
                // Wire port click events for link creation
                GraphCanvasControl.PortClicked += (port) =>
                {
                    if (_viewModel.IsLinkCreationMode)
                    {
                        // Complete link to this port
                        var targetNode = _viewModel.CurrentGraph.Nodes.FirstOrDefault(n => n.Id == port.NodeId);
                        if (targetNode != null)
                        {
                            _viewModel.CompleteLinkCreationCommand.Execute(targetNode);
                            GraphCanvasControl.LinkCreationSourceNodeId = null;
                            GraphCanvasControl.IsInLinkCreationMode = false;
                            
                            // Clear node selection visuals since we now have only link selected
                            foreach (var node in _viewModel.CurrentGraph.Nodes)
                            {
                                node.IsSelected = false;
                            }
                            
                            // Re-render to clear port highlighting and update link visuals
                            GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                        }
                    }
                    else
                    {
                        // Start link from this port
                        var sourceNode = _viewModel.CurrentGraph.Nodes.FirstOrDefault(n => n.Id == port.NodeId);
                        if (sourceNode != null)
                        {
                            _viewModel.StartLinkCreationCommand.Execute(sourceNode);
                            GraphCanvasControl.LinkCreationSourceNodeId = sourceNode.Id;
                            GraphCanvasControl.IsInLinkCreationMode = true;
                            // Re-render to show port highlighting
                            GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                        }
                    }
                };

                // Wire node drag finished event for link rerouting
                var linkRoutingVM = new ViewModels.LinkRoutingViewModel();
                GraphCanvasControl.NodeDragFinished += (movedNode) =>
                {
                    // Reroute all links connected to the moved node
                    linkRoutingVM.RerouteConnectedLinks(movedNode, _viewModel.CurrentGraph);
                    // Re-render to show rerouted links
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                };

                // Update link creation source when mode changes
                _viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(_viewModel.CurrentGraph))
                    {
                        // Re-wire collection change events when graph is replaced
                        _viewModel.CurrentGraph.Nodes.CollectionChanged += (s2, e2) =>
                        {
                            GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                        };
                        _viewModel.CurrentGraph.Links.CollectionChanged += (s2, e2) =>
                        {
                            GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                        };
                        // Render the new graph
                        GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                        GraphCanvasControl.LastSelectedNode = null;
                        GraphCanvasControl.LinkCreationSourceNodeId = null;
                        GraphCanvasControl.IsInLinkCreationMode = false;
                    }
                    if (e.PropertyName == nameof(_viewModel.IsLinkCreationMode))
                    {
                        GraphCanvasControl.LinkCreationSourceNodeId = _viewModel.IsLinkCreationMode ? _viewModel.LinkSourceNodeId : null;
                        GraphCanvasControl.IsInLinkCreationMode = _viewModel.IsLinkCreationMode;
                        GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    }
                    if (e.PropertyName == nameof(_viewModel.ShowGrid))
                    {
                        GraphCanvasControl.ShowGrid = _viewModel.ShowGrid;
                    }
                    if (e.PropertyName == nameof(_viewModel.SelectedNode) || e.PropertyName == nameof(_viewModel.SelectedNodeEdit))
                    {
                        // Update JSON editor to use the staging model when selection or staging model changes
                        if (JsonEditor != null)
                        {
                            JsonEditor.Text = _viewModel.SelectedNodeEdit?.JsonData ?? string.Empty;
                        }
                        // Subscribe to SelectedNodeEdit changes for live preview
                        SubscribeToNodeEditPreview(_viewModel.SelectedNodeEdit);
                        // Pass staging model and selected node id to canvas for live preview rendering
                        GraphCanvasControl.SelectedNodeEditModel = _viewModel.SelectedNodeEdit;
                        GraphCanvasControl.SelectedNodeId = _viewModel.SelectedNode?.Id;
                    }
                };

                // Wire JSON editor text changes back to the model
                if (JsonEditor != null)
                {
                    // Route JSON editor changes to the staging model instead of directly mutating the active node.
                    JsonEditor.TextChanged += (s, e) =>
                    {
                        if (_viewModel.SelectedNodeEdit != null)
                        {
                            _viewModel.SelectedNodeEdit.JsonData = JsonEditor.Text;
                        }
                    };
                };

                // Right-click on canvas for context menu
                GraphCanvasControl.MouseRightButtonDown += (s, e) =>
                {
                    if (_viewModel.SelectedNode == null && _viewModel.SelectedLink == null)
                    {
                        ShowCanvasContextMenu(e);
                        LinkToolbarPopup.IsOpen = false;
                    }
                };
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error wiring canvas: {ex.Message}");
            }
        }

        private void ShowNodeContextMenu(Node node)
        {
            var menu = new ContextMenu();

            var editItem = new MenuItem { Header = "Edit Properties" };
            editItem.Click += (s, e) => 
            {
                // Properties already shown in the properties panel
                _viewModel.StatusMessage = $"Editing properties for '{node.Name}'";
            };
            menu.Items.Add(editItem);

            menu.Items.Add(new Separator());

            var linkItem = new MenuItem { Header = "Start Link" };
            linkItem.Click += (s, e) => _viewModel?.StartLinkCreationCommand.Execute(node);
            menu.Items.Add(linkItem);

            menu.Items.Add(new Separator());

            var duplicateItem = new MenuItem { Header = "Duplicate" };
            duplicateItem.Click += (s, e) => _viewModel?.DuplicateNodeCommand.Execute(null);
            menu.Items.Add(duplicateItem);

            var colorItem = new MenuItem { Header = "Change Color" };
            colorItem.Click += (s, e) => ShowColorPicker(node);
            menu.Items.Add(colorItem);

            var resizeItem = new MenuItem { Header = "Resize" };
            resizeItem.Click += (s, e) => ShowResizeDialog(node);
            menu.Items.Add(resizeItem);

            menu.Items.Add(new Separator());

            var lockItem = new MenuItem 
            { 
                Header = node.IsLocked ? "Unlock" : "Lock",
                IsCheckable = false
            };
            lockItem.Click += (s, e) => node.IsLocked = !node.IsLocked;
            menu.Items.Add(lockItem);

            menu.Items.Add(new Separator());

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += (s, e) => _viewModel?.DeleteNodeCommand.Execute(null);
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
        }

        private void SidebarColor_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedNodeEdit == null)
                return;

            if (sender is Button btn && btn.Tag is string color)
            {
                _viewModel.SelectedNodeEdit.Color = color;
                // reflect preview on canvas immediately
                GraphCanvasControl.SelectedNodeEditModel = _viewModel.SelectedNodeEdit;
                GraphCanvasControl.SelectedNodeId = _viewModel.SelectedNode?.Id;
                GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                // Close floating palette if open
                try { ColorPalettePopup.IsOpen = false; } catch { }
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the floating color palette near the mouse/click
            try
            {
                if (ColorPalettePopup != null)
                {
                    // Place popup near mouse for convenience
                    ColorPalettePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    ColorPalettePopup.IsOpen = true;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening color palette: {ex.Message}");
            }
        }

        private void ShowLinkContextMenu(Link link)
        {
            var menu = new ContextMenu();

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += (s, e) => _viewModel?.DeleteLinkCommand.Execute(null);
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
            // Ensure toolbar closed when context menu used
            LinkToolbarPopup.IsOpen = false;
        }

        private void ShowLinkToolbar(Link link)
        {
            if (_viewModel == null || link == null)
                return;

            // Find source and target nodes
            var src = _viewModel.CurrentGraph.Nodes.FirstOrDefault(n => n.Id == link.SourceNodeId);
            var tgt = _viewModel.CurrentGraph.Nodes.FirstOrDefault(n => n.Id == link.TargetNodeId);
            if (src == null || tgt == null)
            {
                LinkToolbarPopup.IsOpen = false;
                return;
            }

            // Compute mid point (canvas coordinates)
            var x1 = src.X + src.Width / 2;
            var y1 = src.Y + src.Height / 2;
            var x2 = tgt.X + tgt.Width / 2;
            var y2 = tgt.Y + tgt.Height / 2;
            var midX = (x1 + x2) / 2;
            var midY = (y1 + y2) / 2;

            // Position the popup relative to the canvas
            LinkToolbarPopup.HorizontalOffset = midX;
            LinkToolbarPopup.VerticalOffset = midY - 24; // slightly above the link
            LinkToolbarPopup.IsOpen = true;
            LinkToolbarPopup.Focus();
        }

        private void LinkToolbar_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedLink == null)
                return;

            if (_viewModel.DeleteLinkCommand.CanExecute(null))
                _viewModel.DeleteLinkCommand.Execute(null);

            LinkToolbarPopup.IsOpen = false;
            GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
        }

        private void LinkToolbar_Color_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedLink == null)
                return;

            LinkToolbarPopup.IsOpen = false; // Close toolbar first
            var link = _viewModel.SelectedLink;
            var colors = new[] { "#000000", "#FF0000", "#00AA00", "#0000FF", "#FFA500", "#800080" };
            var menu = new ContextMenu();
            foreach (var color in colors)
            {
                var item = new MenuItem { Header = color };
                item.Click += (s, ev) =>
                {
                    link.Color = color;
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    _viewModel.StatusMessage = $"Link color changed to {color}";
                };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        private void LinkToolbar_EditLabel_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedLink == null)
                return;

            LinkToolbarPopup.IsOpen = false; // Close toolbar first
            var link = _viewModel.SelectedLink;
            // Simple inline prompt
            var input = Microsoft.VisualBasic.Interaction.InputBox("Enter link label:", "Edit Link Label", link.Label ?? "");
            if (input != null)
            {
                link.Label = input;
                GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                _viewModel.StatusMessage = "Link label updated";
            }
        }

        private void ShowCanvasContextMenu(MouseButtonEventArgs e)
        {
            var menu = new ContextMenu();

            var addNodeItem = new MenuItem { Header = "Add Node" };
            addNodeItem.Click += (s, ee) => _viewModel?.AddNodeCommand.Execute(null);
            menu.Items.Add(addNodeItem);

            menu.IsOpen = true;
        }
// Application.Current.Shutdown();
     private async void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            // Example 1: Simple direct calls
            WpfInputHelper.ClickAt(500, 300);
            WpfInputHelper.TypeText("Hello World");
            WpfInputHelper.PressKey(Constants.VK_RETURN);
            
            // Example 2: Using OperationModel with builder pattern
            var operations = new[]
            {
                OperationBuilder.Create()
                    .LeftClick(500, 300)
                    .WithDelayAfter(500)
                    .WithDescription("Click at center")
                    .Build(),
                    
                OperationBuilder.Create()
                    .TypeText("Hello World")
                    .WithDelayAfter(200)
                    .Build(),
                    
                OperationBuilder.Create()
                    .PressKey(Constants.VK_RETURN)
                    .WithDescription("Press Enter")
                    .Build(),
                    
                OperationBuilder.Create()
                    .Wait(1000)
                    .Build(),
                    
                OperationBuilder.Create()
                    .ScrollDown(120)
                    .Build()
            };
            
            await _executor.ExecuteOperationsAsync(operations);
            
            // Example 3: Manual creation with priority
            var manualOps = new[]
            {
                new OperationModel
                {
                    Type = "mouse_left_click",
                    IntValues = new[] { 100, 200 },
                    Priority = 1,
                    DelayAfter = 500,
                    Description = "First click"
                },
                new OperationModel
                {
                    Type = "type_text",
                    StringValues = new[] { "Test" },
                    Priority = 2,
                    DelayAfter = 300
                },
                new OperationModel
                {
                    Type = "key_press",
                    IntValues = new[] { (int)Constants.VK_RETURN },
                    Priority = 3
                }
            };
            
            await _executor.ExecuteOperationsAsync(manualOps);
        }

        /// <summary>
        /// Handles execution request from the ViewModel
        /// </summary>
        private async void OnExecutionRequested(object? sender, EventArgs e)
        {
            try
            {
                _viewModel!.StatusMessage = "Executing graph operations...";

                // Parse operations from the graph nodes
                var operations = ParseGraphOperations();

                if (operations.Count == 0)
                {
                    MessageBox.Show(
                        "No executable operations found in the graph.\n\n" +
                        "Please add nodes with JSON data containing operation definitions.",
                        "No Operations",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    _viewModel.StatusMessage = "No operations to execute";
                    return;
                }

                // Execute the operations
                await _executor.ExecuteOperationsAsync(operations);

                _viewModel.StatusMessage = $"Execution completed. {operations.Count} operation(s) executed successfully.";

                // Only show message box if not auto-executed from command line
                if (!App.AutoExecute)
                {
                    MessageBox.Show(
                        $"Execution completed successfully!\n\n{operations.Count} operation(s) executed.",
                        "Execution Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    // Auto-close application after execution
                    await Task.Delay(500); // Brief delay to ensure execution completes
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                _viewModel!.StatusMessage = $"Execution failed: {ex.Message}";
                
                // Only show error message box if not auto-executed
                if (!App.AutoExecute)
                {
                    // Determine failure type and show appropriate message
                    string failureType = "Unknown Error";
                    string failureDetails = ex.Message;
                    MessageBoxImage icon = MessageBoxImage.Error;

                    if (ex is System.IO.FileNotFoundException)
                    {
                        failureType = "File Not Found";
                        icon = MessageBoxImage.Warning;
                    }
                    else if (ex.Message.Contains("CIRCULAR DEPENDENCY"))
                    {
                        failureType = "Circular Dependency Detected";
                        icon = MessageBoxImage.Warning;
                    }
                    else if (ex.Message.Contains("GraphFilePath is required"))
                    {
                        failureType = "Missing Graph File Path";
                        icon = MessageBoxImage.Warning;
                    }
                    else if (ex.Message.Contains("Failed to load graph"))
                    {
                        failureType = "Graph Loading Failed";
                        icon = MessageBoxImage.Error;
                    }
                    else if (ex.Message.Contains("Invalid JSON data"))
                    {
                        failureType = "Invalid Node Data";
                        icon = MessageBoxImage.Error;
                    }
                    else if (ex.Message.Contains("No valid operations"))
                    {
                        failureType = "Empty or Invalid Graph";
                        icon = MessageBoxImage.Warning;
                    }
                    else if (ex is InvalidOperationException)
                    {
                        failureType = "Operation Failed";
                        icon = MessageBoxImage.Error;
                    }
                    else if (ex is ArgumentException)
                    {
                        failureType = "Invalid Configuration";
                        icon = MessageBoxImage.Warning;
                    }

                    MessageBox.Show(
                        $"❌ Execution Failed\n\n" +
                        $"Failure Type: {failureType}\n\n" +
                        $"Details:\n{failureDetails}\n\n" +
                        $"Please fix the issue and try again.",
                        $"Execution Error - {failureType}",
                        MessageBoxButton.OK,
                        icon
                    );
                }
                else
                {
                    // Auto-close even on error when run from command line
                    await Task.Delay(500);
                    Application.Current.Shutdown(1); // Exit with error code 1
                }
            }
        }

        /// <summary>
        /// Parses graph nodes to extract operation definitions following link flow from start node
        /// </summary>
        private List<OperationModel> ParseGraphOperations()
        {
            var operations = new List<OperationModel>();

            if (_viewModel?.CurrentGraph?.Nodes == null)
                return operations;

            // Find the start node
            var startNode = _viewModel.CurrentGraph.Nodes.FirstOrDefault(n => n.Type?.ToLower() == "start");
            
            if (startNode == null)
            {
                throw new InvalidOperationException(
                    "⚠️ NO START NODE FOUND\n\n" +
                    "The graph must contain exactly one 'start' node.\n\n" +
                    "Please add a start node to define where execution should begin.");
            }

            // Traverse from start node following outgoing links
            var visitedNodes = new HashSet<Guid>();
            var currentNode = startNode;
            int priority = 1;

            while (currentNode != null)
            {
                // Check for cycles
                if (visitedNodes.Contains(currentNode.Id))
                {
                    throw new InvalidOperationException(
                        $"⚠️ CIRCULAR EXECUTION PATH DETECTED\n\n" +
                        $"Node '{currentNode.Name}' has already been visited.\n\n" +
                        "The graph contains a loop. Please remove circular links.");
                }

                visitedNodes.Add(currentNode.Id);

                // Skip start node itself - it's just a marker
                if (currentNode.Type?.ToLower() != "start")
                {
                    try
                    {
                        // Parse JSON data from node
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
                                // Set priority based on traversal order
                                operation.Priority = priority++;
                                operations.Add(operation);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"❌ Failed to parse node '{currentNode.Name}'\n\n" +
                            $"Error: {ex.Message}");
                    }
                }

                // Find the next node by following outgoing link
                var outgoingLink = _viewModel.CurrentGraph.Links.FirstOrDefault(l => l.SourceNodeId == currentNode.Id);
                
                if (outgoingLink != null)
                {
                    currentNode = _viewModel.CurrentGraph.Nodes.FirstOrDefault(n => n.Id == outgoingLink.TargetNodeId);
                }
                else
                {
                    // No outgoing link - end of chain
                    break;
                }
            }

            return operations;
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Graph Simulator v1.0\n\n" +
                "A comprehensive graph visualization and editing application.\n\n" +
                "Features:\n" +
                "• Create and edit nodes and links\n" +
                "• Save/load graphs in JSON format\n" +
                "• Export to multiple formats (SVG, PNG, JPEG, CSV, DOT)\n" +
                "• Undo/redo operations\n" +
                "• Advanced graph analysis\n" +
                "• Customizable node properties\n",
                "About Graph Simulator",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void MenuItem_ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel!.CanvasZoom = System.Math.Min(_viewModel.CanvasZoom * 1.2, 4.0);
        }

        private void MenuItem_ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _viewModel!.CanvasZoom = System.Math.Max(_viewModel.CanvasZoom / 1.2, 0.1);
        }

        private void MenuItem_DarkMode_Click(object sender, RoutedEventArgs e)
        {
            _viewModel!.IsDarkMode = !_viewModel.IsDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var app = Application.Current;
            var themeDictionaries = app.Resources.MergedDictionaries;

            // Remove existing theme
            var existingTheme = themeDictionaries.FirstOrDefault(d => d.Source?.ToString().Contains("MaterialDesignTheme") == true);
            if (existingTheme != null)
                themeDictionaries.Remove(existingTheme);

            // Apply new theme based on IsDarkMode
            if (_viewModel?.IsDarkMode == true)
            {
                // Load dark theme (if available, otherwise use light)
                // For now, just darken the window background
                this.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                GraphCanvasControl.Background = new SolidColorBrush(Color.FromRgb(30, 30, 33));
            }
            else
            {
                this.Background = new SolidColorBrush(Colors.White);
                GraphCanvasControl.Background = new SolidColorBrush(Colors.White);
            }
        }

        private void JsonEditor_Format_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || JsonEditor == null)
                return;

            try
            {
                var json = JsonEditor.Text;
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                var formatted = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
                JsonEditor.Text = formatted;
                if (_viewModel.SelectedNodeEdit != null)
                    _viewModel.SelectedNodeEdit.JsonData = formatted;
                _viewModel.StatusMessage = "JSON formatted successfully";
            }
            catch (Exception ex)
            {
                _viewModel!.StatusMessage = $"Error formatting JSON: {ex.Message}";
            }
        }

        private void JsonEditor_Validate_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || JsonEditor == null)
                return;

            try
            {
                var json = JsonEditor.Text;
                if (string.IsNullOrWhiteSpace(json))
                {
                    _viewModel.StatusMessage = "JSON is empty";
                    return;
                }

                Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                _viewModel.StatusMessage = "JSON is valid";
            }
            catch (Exception ex)
            {
                _viewModel!.StatusMessage = $"JSON validation failed: {ex.Message}";
            }
        }

        private void ShowColorPicker(Node node)
        {
            var colors = new[] { "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF", "#FFA500", "#A020F0", "#007ACC", "#666666" };
            var menu = new ContextMenu();

            foreach (var color in colors)
            {
                var item = new MenuItem { Header = color };
                item.Click += (s, e) => 
                {
                    node.Color = color;
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    _viewModel.StatusMessage = $"Node color changed to {color}";
                };
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }

        private void ShowResizeDialog(Node node)
        {
            var dialog = new Window
            {
                Title = "Resize Node",
                Height = 180,
                Width = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var panel = new StackPanel { Margin = new Thickness(16) };

            panel.Children.Add(new Label { Content = "Width:", FontWeight = FontWeights.Bold });
            var widthBox = new TextBox { Text = node.Width.ToString(), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(widthBox);

            panel.Children.Add(new Label { Content = "Height:", FontWeight = FontWeights.Bold });
            var heightBox = new TextBox { Text = node.Height.ToString(), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 16) };
            panel.Children.Add(heightBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(16, 6, 16, 6), IsCancel = true };

            okBtn.Click += (s, e) =>
            {
                if (double.TryParse(widthBox.Text, out var width) && double.TryParse(heightBox.Text, out var height))
                {
                    node.Width = Math.Max(50, width);
                    node.Height = Math.Max(50, height);
                    GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
                    _viewModel.StatusMessage = "Node resized";
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("Please enter valid numbers for width and height", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void NodeProperties_Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Revert staged edits rather than clearing selection
            if (_viewModel != null)
            {
                if (_viewModel.CancelNodePropertiesCommand.CanExecute(null))
                    _viewModel.CancelNodePropertiesCommand.Execute(null);
                // Re-render to show reverted state
                GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
            }
        }

        private void SubscribeToNodeEditPreview(NodeEditModel? editModel)
        {
            if (editModel == null)
                return;

            // Subscribe to property changes on the staging model to show live preview
            editModel.PropertyChanged -= NodeEditModel_PropertyChanged; // Remove old subscription if any
            editModel.PropertyChanged += NodeEditModel_PropertyChanged;
            
            // Update operation-specific field visibility
            UpdateOperationFieldsVisibility(editModel.Type);
        }

        private void UpdateOperationFieldsVisibility(string operationType)
        {
            // Hide all operation-specific fields first
            MouseOperationFields.Visibility = Visibility.Collapsed;
            ScrollOperationFields.Visibility = Visibility.Collapsed;
            KeyOperationFields.Visibility = Visibility.Collapsed;
            TypeTextFields.Visibility = Visibility.Collapsed;
            WaitOperationFields.Visibility = Visibility.Collapsed;
            CustomCodeFields.Visibility = Visibility.Collapsed;
            GraphFields.Visibility = Visibility.Collapsed;

            // Show relevant fields based on operation type
            switch (operationType?.ToLower())
            {
                case "start":
                    // Start node has no operation-specific fields
                    break;
                case "mouse_left_click":
                case "mouse_right_click":
                case "mouse_move":
                    MouseOperationFields.Visibility = Visibility.Visible;
                    break;
                case "scroll_up":
                case "scroll_down":
                case "scroll_left":
                case "scroll_right":
                    ScrollOperationFields.Visibility = Visibility.Visible;
                    break;
                case "key_press":
                case "key_down":
                case "key_up":
                    KeyOperationFields.Visibility = Visibility.Visible;
                    break;
                case "type_text":
                    TypeTextFields.Visibility = Visibility.Visible;
                    break;
                case "wait":
                    WaitOperationFields.Visibility = Visibility.Visible;
                    break;
                case "custom_code":
                    CustomCodeFields.Visibility = Visibility.Visible;
                    break;
                case "graph":
                    GraphFields.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void NodeEditModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_viewModel?.SelectedNode == null)
                return;

            // Update operation field visibility when type changes
            if (e.PropertyName == nameof(NodeEditModel.Type) && sender is NodeEditModel editModel)
            {
                UpdateOperationFieldsVisibility(editModel.Type);
                
                // Also update the canvas since type change affects color
                GraphCanvasControl.SelectedNodeEditModel = _viewModel.SelectedNodeEdit;
                GraphCanvasControl.SelectedNodeId = _viewModel.SelectedNode?.Id;
                GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
            }

            // Update JSON editor when JsonData changes
            if (e.PropertyName == nameof(NodeEditModel.JsonData) && sender is NodeEditModel model)
            {
                if (JsonEditor != null && JsonEditor.Text != model.JsonData)
                {
                    JsonEditor.Text = model.JsonData;
                }
            }

            // Update the selected node's visual properties in real-time for preview
            if (e.PropertyName == nameof(NodeEditModel.Color) ||                 e.PropertyName == nameof(NodeEditModel.Name) ||
                e.PropertyName == nameof(NodeEditModel.Width) ||
                e.PropertyName == nameof(NodeEditModel.Height))
            {
                // Ensure canvas has the current staging model + selected node id
                GraphCanvasControl.SelectedNodeEditModel = _viewModel.SelectedNodeEdit;
                GraphCanvasControl.SelectedNodeId = _viewModel.SelectedNode?.Id;
                // Re-render to show preview
                GraphCanvasControl.RenderGraph(_viewModel.CurrentGraph);
            }
        }

        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            // Toggle sidebar column width
            if (SidebarColumn.Width.Value > 0)
            {
                // Hide sidebar
                SidebarColumn.Width = new GridLength(0);
                SidebarToggleButton.Content = "−";
                SidebarToggleButton.ToolTip = "Toggle sidebar";
                // Show the show sidebar button
                ShowSidebarButton.Visibility = Visibility.Visible;
            }
            else
            {
                // Show sidebar
                SidebarColumn.Width = new GridLength(300);
                SidebarToggleButton.Content = "−";
                SidebarToggleButton.ToolTip = "Hide sidebar";
                // Hide the show sidebar button
                ShowSidebarButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            // Show sidebar
            SidebarColumn.Width = new GridLength(300);
            SidebarToggleButton.Content = "−";
            SidebarToggleButton.ToolTip = "Hide sidebar";
            // Hide the show sidebar button
            ShowSidebarButton.Visibility = Visibility.Collapsed;
        }

        #region Mouse Position Tracking

        /// <summary>
        /// Initializes the mouse position tracking timer
        /// </summary>
        private void InitializeMousePositionTracking()
        {
            _mousePositionTimer = new System.Windows.Threading.DispatcherTimer();
            _mousePositionTimer.Interval = TimeSpan.FromMilliseconds(50); // Update 20 times per second
            _mousePositionTimer.Tick += MousePositionTimer_Tick;
        }

        /// <summary>
        /// Event handler for when the Show Mouse Position checkbox is checked
        /// </summary>
        private void ShowMousePosition_Checked(object sender, RoutedEventArgs e)
        {
            _isTrackingMousePosition = true;
            MousePositionText.Visibility = Visibility.Visible;
            
            if (_mousePositionTimer != null)
            {
                _mousePositionTimer.Start();
            }
            
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Mouse position tracking enabled";
            }
        }

        /// <summary>
        /// Event handler for when the Show Mouse Position checkbox is unchecked
        /// </summary>
        private void ShowMousePosition_Unchecked(object sender, RoutedEventArgs e)
        {
            _isTrackingMousePosition = false;
            MousePositionText.Visibility = Visibility.Collapsed;
            
            if (_mousePositionTimer != null)
            {
                _mousePositionTimer.Stop();
            }
            
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Mouse position tracking disabled";
            }
        }

        /// <summary>
        /// Timer tick event to update mouse position display
        /// </summary>
        private void MousePositionTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isTrackingMousePosition)
                return;

            try
            {
                // Get global cursor position using Windows API
                if (GetCursorPos(out POINT cursorPos))
                {
                    // Update the display with actual screen coordinates
                    MousePositionText.Text = $"Screen X: {cursorPos.X}, Y: {cursorPos.Y}";
                }
            }
            catch
            {
                // Ignore any errors in getting mouse position
            }
        }

        #endregion

        #region Scroll Measurement

        /// <summary>
        /// Event handler for when the Scroll Measure checkbox is checked
        /// </summary>
        private void ScrollMeasure_Checked(object sender, RoutedEventArgs e)
        {
            _isScrollMeasureActive = true;
            _scrollMeasureAmount = 0;
            _scrollMeasureHorizontalAmount = 0;
            ScrollMeasureText.Visibility = Visibility.Visible;
            ScrollMeasureHorizontalText.Visibility = Visibility.Visible;
            ResetScrollButton.Visibility = Visibility.Visible;
            ResetHScrollButton.Visibility = Visibility.Visible;
            UpdateScrollMeasureDisplay();
            // Attach scroll event to the main window
            this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            this.PreviewMouseWheel += MainWindow_PreviewMouseHWheel;
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Scroll measurement enabled - Use two-finger swipe or scroll wheel (vertical/horizontal)";
            }
        }

        /// <summary>
        /// Event handler for when the Scroll Measure checkbox is unchecked
        /// </summary>
        private void ScrollMeasure_Unchecked(object sender, RoutedEventArgs e)
        {
            _isScrollMeasureActive = false;
            ScrollMeasureText.Visibility = Visibility.Collapsed;
            ScrollMeasureHorizontalText.Visibility = Visibility.Collapsed;
            ResetScrollButton.Visibility = Visibility.Collapsed;
            ResetHScrollButton.Visibility = Visibility.Collapsed;
            // Detach scroll event
            this.PreviewMouseWheel -= MainWindow_PreviewMouseWheel;
            this.PreviewMouseWheel -= MainWindow_PreviewMouseHWheel;
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Scroll measurement disabled";
            }
        }

        /// <summary>
        /// Handle mouse wheel events for scroll measurement
        /// </summary>
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isScrollMeasureActive)
            {
                // Accumulate scroll delta (vertical)
                _scrollMeasureAmount += e.Delta;
                UpdateScrollMeasureDisplay();
            }
        }

        // Handle horizontal mouse wheel events (if supported)
        private void MainWindow_PreviewMouseHWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isScrollMeasureActive)
            {
                // Accumulate horizontal scroll delta
                _scrollMeasureHorizontalAmount += e.Delta;
                UpdateScrollMeasureDisplay();
            }
        }

        /// <summary>
        /// Reset scroll measurement counter
        /// </summary>
        private void ResetScroll_Click(object sender, RoutedEventArgs e)
        {
            _scrollMeasureAmount = 0;
            UpdateScrollMeasureDisplay();
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Scroll counter reset to 0";
            }
        }

        private void ResetHScroll_Click(object sender, RoutedEventArgs e)
        {
            _scrollMeasureHorizontalAmount = 0;
            UpdateScrollMeasureDisplay();
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Horizontal scroll counter reset to 0";
            }
        }

        /// <summary>
        /// Update the scroll measurement display
        /// </summary>
        private void UpdateScrollMeasureDisplay()
        {
            ScrollMeasureText.Text = $"Scroll: {_scrollMeasureAmount} (↑positive / ↓negative)";
            ScrollMeasureHorizontalText.Text = $"HScroll: {_scrollMeasureHorizontalAmount} (→positive / ←negative)";
        }

        #endregion

        /// <summary>
        /// Handles Browse button click to select a graph file
        /// </summary>
        private void BrowseGraphFile_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.SelectedNodeEdit == null)
                return;

            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Graph Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Select Graph File to Execute"
                };

                if (dialog.ShowDialog() == true)
                {
                    // Store the path immediately without validation
                    _viewModel.SelectedNodeEdit.GraphFilePath = dialog.FileName;
                    
                    // Set the node name to the selected graph filename (without extension)
                    string graphFileName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                    _viewModel.SelectedNodeEdit.Name = graphFileName;
                    
                    GraphValidationMessage.Text = $"📄 Selected: {System.IO.Path.GetFileName(dialog.FileName)}";
                    GraphValidationMessage.Foreground = new SolidColorBrush(Colors.Blue);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting graph file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Saves the graph as an executable file with a .bat launcher
        /// </summary>
        private void SaveAsExecutable_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
                return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Graph Files (*.json)|*.json",
                    Title = "Save Graph as Executable",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() == true)
                {
                    // Save the graph file
                    _viewModel.SaveGraphToFileAsync(dialog.FileName);

                    // Get the directory and file name
                    string directory = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
                    string graphFileName = System.IO.Path.GetFileName(dialog.FileName);
                    string batFileName = System.IO.Path.ChangeExtension(graphFileName, ".bat");
                    string batFilePath = System.IO.Path.Combine(directory, batFileName);

                    // Get the application executable path
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    appPath = appPath.Replace(".dll", ".exe"); // Handle .NET Core/5+ case

                    // Create a batch file to launch the application with the graph file
                    string batContent = $"@echo off\r\n\"{appPath}\" \"%~dp0{graphFileName}\" --execute\r\n";
                    System.IO.File.WriteAllText(batFilePath, batContent);

                    MessageBox.Show(
                        $"Executable graph saved successfully!\n\n" +
                        $"Graph File: {graphFileName}\n" +
                        $"Launcher: {batFileName}\n\n" +
                        $"Double-click '{batFileName}' to execute the graph.\n" +
                        $"Open '{graphFileName}' in the application to edit.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    _viewModel.StatusMessage = $"Executable graph saved to {batFileName}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving executable graph: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
       
    }
}
