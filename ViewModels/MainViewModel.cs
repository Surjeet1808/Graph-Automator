using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GraphSimulator.Models;
using GraphSimulator.Services;
using System.Windows;

namespace GraphSimulator.ViewModels
{
    /// <summary>
    /// Main view model for the application
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly FileService _fileService;
        private readonly CommandHistory _commandHistory;
        private string? _currentFilePath;
        private Node? _clipboardNode = null;

        /// <summary>
        /// Gets the current file path being edited
        /// </summary>
        public string? CurrentFilePath => _currentFilePath;

        [ObservableProperty]
        private Graph currentGraph;

        [ObservableProperty]
        private Node? selectedNode;

        [ObservableProperty]
        private NodeEditModel? selectedNodeEdit;

        [ObservableProperty]
        private Link? selectedLink;

        [ObservableProperty]
        private GraphStatistics? graphStatistics;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool isDarkMode = false;

        [ObservableProperty]
        private bool showGrid = true;

        [ObservableProperty]
        private double canvasZoom = 1.0;

        [ObservableProperty]
        private bool snapToGrid = true;

        [ObservableProperty]
        private bool canUndo = false;

        [ObservableProperty]
        private bool canRedo = false;

        [ObservableProperty]
        private string undoDescription = "Undo";

        [ObservableProperty]
        private string redoDescription = "Redo";

        [ObservableProperty]
        private bool isLinkCreationMode = false;

        [ObservableProperty]
        private Guid? linkSourceNodeId = null;

        [ObservableProperty]
        private bool canPaste = false;

        public ObservableCollection<string> NodeTypes { get; }
        public ObservableCollection<string> RecentFiles { get; }

        public MainViewModel()
        {
            _fileService = new FileService();
            _commandHistory = new CommandHistory();
            CurrentGraph = new Graph();
            NodeTypes = new ObservableCollection<string>(Graph.DefaultNodeTypes);
            RecentFiles = new ObservableCollection<string>();

            _commandHistory.HistoryChanged += OnHistoryChanged;

            LoadRecentFiles();

            // Auto-save timer (30 seconds)
            _autoSaveTimer = new System.Timers.Timer(30000);
            _autoSaveTimer.Elapsed += async (s, e) => await AutoSaveAsync();
            _autoSaveTimer.AutoReset = true;
            _autoSaveTimer.Enabled = true;
        }

        partial void OnSelectedNodeChanged(Node? value)
        {
            if (value == null)
            {
                SelectedNodeEdit = null;
                return;
            }

            SelectedNodeEdit = new NodeEditModel
            {
                Name = value.Name,
                Width = value.Width,
                Height = value.Height,
                JsonData = value.JsonData
            };
            
            // Set type which will automatically update the color
            SelectedNodeEdit.Type = value.Type;
            
            // Load operation-specific properties from JSON
            SelectedNodeEdit.LoadFromJsonData(value.JsonData);
        }

        private readonly System.Timers.Timer _autoSaveTimer;

        private async Task AutoSaveAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    await _fileService.SaveGraphAsync(CurrentGraph, _currentFilePath);
                }
                else
                {
                    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GraphSimulator_AutoSave.json");
                    await _fileService.SaveGraphAsync(CurrentGraph, tmp);
                }
            }
            catch
            {
                // swallow auto-save errors silently
            }
        }

        /// <summary>
        /// Creates a new graph
        /// </summary>
        [RelayCommand]
        public void NewGraph()
        {
            CurrentGraph = new Graph();
            _commandHistory.Clear();
            _currentFilePath = null;
            StatusMessage = "New graph created";
            UpdateStatistics();
        }

        /// <summary>
        /// Adds a new node to the graph
        /// </summary>
        [RelayCommand]
        public void AddNode(string? nodeType = null)
        {
            nodeType ??= "mouse_left_click";
            
            // Check if trying to add a start node when one already exists
            if (nodeType.ToLower() == "start")
            {
                var existingStartNode = CurrentGraph.Nodes.FirstOrDefault(n => n.Type?.ToLower() == "start");
                if (existingStartNode != null)
                {
                    StatusMessage = "Cannot add start node: A start node already exists";
                    System.Windows.MessageBox.Show(
                        "❌ Cannot Add Start Node\n\n" +
                        "A graph can have only ONE start node.\n\n" +
                        $"Existing start node: '{existingStartNode.Name}'\n\n" +
                        "Please remove the existing start node first if you want to add a new one.",
                        "Start Node Limit",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning
                    );
                    return;
                }
            }
            
            var node = new Node 
            { 
                Name = FormatOperationTypeName(nodeType),
                Type = nodeType,
                Color = Graph.GetColorForNodeType(nodeType),
                JsonData = GetDefaultJsonForType(nodeType)
            };
            
            var command = new AddNodeCommand(CurrentGraph, node);
            _commandHistory.Execute(command);
            
            StatusMessage = $"Node '{node.Name}' added";
            UpdateStatistics();
        }

        /// <summary>
        /// Adds a new node at a specific canvas location (used by double-click)
        /// </summary>
        [RelayCommand]
        public void AddNodeAt(Point position)
        {
            var nodeType = "mouse_left_click";
            var node = new Node
            {
                Name = FormatOperationTypeName(nodeType),
                Type = nodeType,
                Color = Graph.GetColorForNodeType(nodeType),
                JsonData = GetDefaultJsonForType(nodeType)
            };

            // Center the node at the clicked position
            node.X = position.X - node.Width / 2;
            node.Y = position.Y - node.Height / 2;

            if (SnapToGrid)
            {
                var grid = 10.0;
                node.X = Math.Round(node.X / grid) * grid;
                node.Y = Math.Round(node.Y / grid) * grid;
            }

            // Ensure no negative positions
            node.X = Math.Max(0, node.X);
            node.Y = Math.Max(0, node.Y);

            var command = new AddNodeCommand(CurrentGraph, node);
            _commandHistory.Execute(command);

            StatusMessage = $"Node '{node.Name}' added";
            UpdateStatistics();
        }

        /// <summary>
        /// Gets default JSON data for an operation type
        /// </summary>
        private string GetDefaultJsonForType(string type)
        {
            var operation = new
            {
                Type = type,
                IntValues = GetDefaultIntValues(type),
                StringValues = GetDefaultStringValues(type),
                Priority = 0,
                DelayBefore = 0,
                DelayAfter = 0,
                Enabled = true
            };

            return System.Text.Json.JsonSerializer.Serialize(operation, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }

        private int[] GetDefaultIntValues(string type)
        {
            return type.ToLower() switch
            {
                "mouse_left_click" or "mouse_right_click" or "mouse_move" => new[] { 100, 100 },
                "scroll_up" or "scroll_down" => new[] { 120 },
                "key_press" or "key_down" or "key_up" => new[] { 13 },
                "wait" => new[] { 1000 },
                _ => Array.Empty<int>()
            };
        }

        private string[] GetDefaultStringValues(string type)
        {
            return type.ToLower() switch
            {
                "type_text" => new[] { "Hello World" },
                _ => Array.Empty<string>()
            };
        }

        /// <summary>
        /// Formats operation type name by replacing underscores with spaces and capitalizing
        /// </summary>
        private string FormatOperationTypeName(string type)
        {
            if (string.IsNullOrEmpty(type))
                return "New Node";
            
            // Replace underscores with spaces and capitalize each word
            var words = type.Split('_');
            var formattedWords = words.Select(w => 
                string.IsNullOrEmpty(w) ? "" : char.ToUpper(w[0]) + w.Substring(1).ToLower()
            );
            return string.Join(" ", formattedWords);
        }

        /// <summary>
        /// Deletes the selected node
        /// </summary>
        [RelayCommand]
        public void DeleteNode()
        {
            if (SelectedNode == null)
                return;

            var nodeId = SelectedNode.Id;
            var command = new DeleteNodeCommand(CurrentGraph, nodeId);
            _commandHistory.Execute(command);

            SelectedNode = null;
            StatusMessage = "Node deleted";
            UpdateStatistics();
        }

        /// <summary>
        /// Duplicates the selected node
        /// </summary>
        [RelayCommand]
        public void DuplicateNode()
        {
            if (SelectedNode == null)
                return;

            var clonedNode = SelectedNode.Clone();
            var command = new AddNodeCommand(CurrentGraph, clonedNode);
            _commandHistory.Execute(command);

            StatusMessage = "Node duplicated";
            UpdateStatistics();
        }

        /// <summary>
        /// Saves the selected node's properties
        /// </summary>
        [RelayCommand]
        public void SaveNodeProperties()
        {
            if (SelectedNode == null || SelectedNodeEdit == null)
                return;

            SelectedNode.Name = SelectedNodeEdit.Name;
            SelectedNode.Type = SelectedNodeEdit.Type;
            SelectedNode.Color = Graph.GetColorForNodeType(SelectedNodeEdit.Type);
            SelectedNode.JsonData = SelectedNodeEdit.JsonData;
            SelectedNode.Width = SelectedNodeEdit.Width;
            SelectedNode.Height = SelectedNodeEdit.Height;
            SelectedNode.ModifiedAt = DateTime.UtcNow;

            StatusMessage = $"Node '{SelectedNode.Name}' properties saved";
            UpdateStatistics();
               
        }

        /// <summary>
        /// Cancel node property edits and revert staging model
        /// </summary>
        [RelayCommand]
        public void CancelNodeProperties()
        {
            if (SelectedNode == null)
                return;

            // Reset edit model to current selected node
            SelectedNodeEdit = new NodeEditModel
            {
                Name = SelectedNode.Name,
                Type = SelectedNode.Type,
                Color = SelectedNode.Color,
                JsonData = SelectedNode.JsonData,
                Width = SelectedNode.Width,
                Height = SelectedNode.Height
            };

            StatusMessage = "Edits cancelled";
        }

        /// <summary>
        /// Starts link creation mode by selecting source node
        /// </summary>
        [RelayCommand]
        public void StartLinkCreation(Node sourceNode)
        {
            if (sourceNode == null)
                return;

            IsLinkCreationMode = true;
            LinkSourceNodeId = sourceNode.Id;
            StatusMessage = $"Select target node for link from '{sourceNode.Name}'";
        }

        /// <summary>
        /// Completes link creation by selecting target node
        /// </summary>
        [RelayCommand]
        public void CompleteLinkCreation(Node targetNode)
        {
            if (targetNode == null || LinkSourceNodeId == null)
                return;

            if (targetNode.Id == LinkSourceNodeId.Value)
            {
                StatusMessage = "Cannot create self-loop: source and target must be different";
                CancelLinkCreation();
                return;
            }

            var sourceNode = CurrentGraph.Nodes.FirstOrDefault(n => n.Id == LinkSourceNodeId.Value);
            if (sourceNode == null)
            {
                StatusMessage = "Source node no longer exists";
                CancelLinkCreation();
                return;
            }

            // Check if source node already has an outgoing link (exit)
            var sourceOutgoingLinks = CurrentGraph.Links.Where(l => l.SourceNodeId == LinkSourceNodeId.Value).ToList();
            if (sourceOutgoingLinks.Count >= 1)
            {
                StatusMessage = "Source node already has an exit link. Each node can have only one exit.";
                System.Windows.MessageBox.Show(
                    "Each node can have only one exit link.\n\n" +
                    $"'{sourceNode.Name}' already has an outgoing connection.\n" +
                    "Remove the existing link first if you want to create a new one.",
                    "Link Limit Reached",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
                CancelLinkCreation();
                return;
            }

            // Check if target is a start node - start nodes cannot have incoming links
            if (targetNode.Type?.ToLower() == "start")
            {
                StatusMessage = "Cannot connect to start node. Start nodes can only have outgoing links.";
                System.Windows.MessageBox.Show(
                    "❌ Cannot Connect to Start Node\n\n" +
                    "Start nodes can only have outgoing links (exits), not incoming links (entries).\n\n" +
                    $"'{targetNode.Name}' is a start node and cannot be a link target.",
                    "Invalid Link Target",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
                CancelLinkCreation();
                return;
            }

            // Check if target node already has an incoming link (entry)
            var targetIncomingLinks = CurrentGraph.Links.Where(l => l.TargetNodeId == targetNode.Id).ToList();
            if (targetIncomingLinks.Count >= 1)
            {
                StatusMessage = "Target node already has an entry link. Each node can have only one entry.";
                System.Windows.MessageBox.Show(
                    "Each node can have only one entry link.\n\n" +
                    $"'{targetNode.Name}' already has an incoming connection.\n" +
                    "Remove the existing link first if you want to create a new one.",
                    "Link Limit Reached",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
                CancelLinkCreation();
                return;
            }

            // Check for duplicate link
            var existingLink = CurrentGraph.Links.FirstOrDefault(l =>
                l.SourceNodeId == LinkSourceNodeId.Value && l.TargetNodeId == targetNode.Id
            );
            if (existingLink != null)
            {
                StatusMessage = "Link already exists between these nodes";
                CancelLinkCreation();
                return;
            }

            try
            {
                var link = new Link(LinkSourceNodeId.Value, targetNode.Id);
                
                // Assign ports based on node positions
                // For source node: use right port if target is to the right, otherwise use bottom
                var sourceRightPort = sourceNode.Ports.FirstOrDefault(p => p.Position == PortPosition.Right);
                var sourceBottomPort = sourceNode.Ports.FirstOrDefault(p => p.Position == PortPosition.Bottom);
                
                if (targetNode.X > sourceNode.X && sourceRightPort != null)
                {
                    link.SourcePortId = sourceRightPort.Id;
                }
                else if (targetNode.Y > sourceNode.Y && sourceBottomPort != null)
                {
                    link.SourcePortId = sourceBottomPort.Id;
                }
                else if (sourceRightPort != null)
                {
                    link.SourcePortId = sourceRightPort.Id;
                }
                
                // For target node: use left port if source is to the left, otherwise use top
                var targetLeftPort = targetNode.Ports.FirstOrDefault(p => p.Position == PortPosition.Left);
                var targetTopPort = targetNode.Ports.FirstOrDefault(p => p.Position == PortPosition.Top);
                
                if (sourceNode.X < targetNode.X && targetLeftPort != null)
                {
                    link.TargetPortId = targetLeftPort.Id;
                }
                else if (sourceNode.Y < targetNode.Y && targetTopPort != null)
                {
                    link.TargetPortId = targetTopPort.Id;
                }
                else if (targetLeftPort != null)
                {
                    link.TargetPortId = targetLeftPort.Id;
                }
                
                var command = new AddLinkCommand(CurrentGraph, link);
                _commandHistory.Execute(command);

                StatusMessage = $"Link created from '{sourceNode.Name}' to '{targetNode.Name}'";
                UpdateStatistics();
                SelectLink(link);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating link: {ex.Message}";
            }
            finally
            {
                CancelLinkCreation();
            }
        }

        /// <summary>
        /// Cancels link creation mode
        /// </summary>
        [RelayCommand]
        public void CancelLinkCreation()
        {
            IsLinkCreationMode = false;
            LinkSourceNodeId = null;
            StatusMessage = "Link creation cancelled";
        }

        /// <summary>
        /// Adds a link between two nodes (invoked separately)
        /// </summary>
        [RelayCommand]
        public void PrepareAddLink(Guid nodeId)
        {
            // This would be called from UI when creating a link
            StatusMessage = "Select target node for link";
        }

        /// <summary>
        /// Deletes the selected link
        /// </summary>
        [RelayCommand]
        public void DeleteLink()
        {
            if (SelectedLink == null)
                return;

            var linkId = SelectedLink.Id;
            var command = new DeleteLinkCommand(CurrentGraph, linkId);
            _commandHistory.Execute(command);

            SelectedLink = null;
            StatusMessage = "Link deleted";
            UpdateStatistics();
        }

        /// <summary>
        /// Reverses the direction of the selected link
        /// </summary>
        [RelayCommand]
        public void ReverseLink()
        {
            if (SelectedLink == null)
                return;

            // Swap source and target
            var temp = SelectedLink.SourceNodeId;
            SelectedLink.SourceNodeId = SelectedLink.TargetNodeId;
            SelectedLink.TargetNodeId = temp;

            // Also swap port IDs
            var tempPort = SelectedLink.SourcePortId;
            SelectedLink.SourcePortId = SelectedLink.TargetPortId;
            SelectedLink.TargetPortId = tempPort;

            SelectedLink.ModifiedAt = DateTime.UtcNow;
            StatusMessage = "Link direction reversed";
            UpdateStatistics();
        }

        /// <summary>
        /// Saves changes to the selected link (updates timestamp and notifies UI)
        /// </summary>
        [RelayCommand]
        public void SaveLink()
        {
            if (SelectedLink == null)
                return;

            SelectedLink.ModifiedAt = DateTime.UtcNow;
            StatusMessage = $"Link properties saved";
            UpdateStatistics();
        }

        /// <summary>
        /// Undoes the last operation
        /// </summary>
        [RelayCommand]
        public void Undo()
        {
            _commandHistory.Undo();
            StatusMessage = "Undo completed";
        }

        /// <summary>
        /// Redoes the last undone operation
        /// </summary>
        [RelayCommand]
        public void Redo()
        {
            _commandHistory.Redo();
            StatusMessage = "Redo completed";
        }

        /// <summary>
        /// Selects a node
        /// </summary>
        [RelayCommand]
        public void SelectNode(Node node)
        {
            // Deselect previous
            if (SelectedNode != null)
                SelectedNode.IsSelected = false;

            SelectedNode = node;
            if (node != null)
                node.IsSelected = true;

            SelectedLink = null;
        }

        /// <summary>
        /// Selects a link
        /// </summary>
        [RelayCommand]
        public void SelectLink(Link link)
        {
            // Deselect previous
            if (SelectedLink != null)
                SelectedLink.IsSelected = false;

            SelectedLink = link;
            if (link != null)
                link.IsSelected = true;

            SelectedNode = null;
        }

        /// <summary>
        /// Deselects all
        /// </summary>
        [RelayCommand]
        public void DeselectAll()
        {
            if (SelectedNode != null)
                SelectedNode.IsSelected = false;
            
            if (SelectedLink != null)
                SelectedLink.IsSelected = false;

            SelectedNode = null;
            SelectedLink = null;
        }

        /// <summary>
        /// Opens a graph file asynchronously
        /// </summary>
        [RelayCommand]
        public async Task OpenGraph()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Graph Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Open Graph"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Loading graph...";
                    var graph = await _fileService.LoadGraphAsync(dialog.FileName);
                    if (graph != null)
                    {
                        // Reinitialize ports for all nodes after deserialization
                        foreach (var node in graph.Nodes)
                        {
                            ReinitializeNodePorts(node);
                        }
                        CurrentGraph = graph;
                    }
                    else
                    {
                        CurrentGraph = new Graph();
                    }
                    _currentFilePath = dialog.FileName;
                    _fileService.SaveRecentFile(dialog.FileName);
                    LoadRecentFiles();
                    _commandHistory.Clear();
                    StatusMessage = $"Graph loaded from {System.IO.Path.GetFileName(dialog.FileName)}";
                    UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading graph: {ex.Message}";
            }
        }

        /// <summary>
        /// Reinitializes ports for a node (needed after deserialization)
        /// </summary>
        private void ReinitializeNodePorts(Node node)
        {
            // If ports collection is null or empty, initialize it
            if (node.Ports == null)
            {
                node.Ports = new ObservableCollection<Port>();
            }
            
            if (node.Ports.Count == 0)
            {
                node.Ports.Add(new Port { NodeId = node.Id, Position = PortPosition.Top });
                node.Ports.Add(new Port { NodeId = node.Id, Position = PortPosition.Right });
                node.Ports.Add(new Port { NodeId = node.Id, Position = PortPosition.Bottom });
                node.Ports.Add(new Port { NodeId = node.Id, Position = PortPosition.Left });
            }
            else
            {
                // Ports exist from saved file, just ensure they have the correct NodeId
                foreach (var port in node.Ports)
                {
                    port.NodeId = node.Id;
                }
            }
        }

        /// <summary>
        /// Saves the current graph asynchronously
        /// </summary>
        [RelayCommand]
        public async Task SaveGraph()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    await SaveGraphAs();
                    return;
                }

                StatusMessage = "Saving graph...";
                await _fileService.SaveGraphAsync(CurrentGraph, _currentFilePath);
                _fileService.SaveRecentFile(_currentFilePath);
                StatusMessage = $"Graph saved to {System.IO.Path.GetFileName(_currentFilePath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving graph: {ex.Message}";
            }
        }

        /// <summary>
        /// Saves the graph with a new filename asynchronously
        /// </summary>
        [RelayCommand]
        public async Task SaveGraphAs()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Graph Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Save Graph As"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Saving graph...";
                    await _fileService.SaveGraphAsync(CurrentGraph, dialog.FileName);
                    _currentFilePath = dialog.FileName;
                    _fileService.SaveRecentFile(dialog.FileName);
                    LoadRecentFiles();
                    StatusMessage = $"Graph saved to {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving graph: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports graph to SVG format asynchronously
        /// </summary>
        [RelayCommand]
        public async Task ExportSvg()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "SVG Files (*.svg)|*.svg|All Files (*.*)|*.*",
                    Title = "Export as SVG"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting to SVG...";
                    var service = new ImageExportService();
                    var result = await service.ExportToSvgAsync(CurrentGraph, dialog.FileName);
                    StatusMessage = result ? $"Graph exported to {System.IO.Path.GetFileName(dialog.FileName)}" : "Error exporting to SVG";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports graph to CSV format asynchronously
        /// </summary>
        [RelayCommand]
        public async Task ExportCsv()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Export as CSV"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting to CSV...";
                    await _fileService.ExportAsCsvAsync(CurrentGraph, dialog.FileName);
                    StatusMessage = $"Graph exported to {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports graph to JSON format asynchronously
        /// </summary>
        [RelayCommand]
        public async Task ExportJson()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Export as JSON"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting to JSON...";
                    // Use FileService's SaveGraphAsync which exports as JSON
                    await _fileService.SaveGraphAsync(CurrentGraph, dialog.FileName);
                    StatusMessage = $"Graph exported to {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports graph to DOT format asynchronously
        /// </summary>
        [RelayCommand]
        public async Task ExportDot()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "DOT Files (*.dot)|*.dot|All Files (*.*)|*.*",
                    Title = "Export as DOT"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting to DOT...";
                    await _fileService.ExportAsDotAsync(CurrentGraph, dialog.FileName);
                    StatusMessage = $"Graph exported to {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports graph to PNG format asynchronously
        /// </summary>
        [RelayCommand]
        public async Task ExportPng()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*",
                    Title = "Export as PNG"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting to PNG...";
                    var service = new ImageExportService();
                    var result = await service.ExportToPngAsync(Application.Current.MainWindow, dialog.FileName, 1920, 1080);
                    StatusMessage = result ? $"Graph exported to {System.IO.Path.GetFileName(dialog.FileName)}" : "Error exporting to PNG";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting: {ex.Message}";
            }
        }

        /// <summary>
        /// Exports graph adjacency matrix as CSV asynchronously
        /// </summary>
        [RelayCommand]
        public async Task ExportAdjacencyMatrix()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Export Adjacency Matrix"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Exporting adjacency matrix...";
                    var csv = GenerateAdjacencyMatrix();
                    await System.IO.File.WriteAllTextAsync(dialog.FileName, csv);
                    StatusMessage = $"Adjacency matrix exported to {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting: {ex.Message}";
            }
        }

        /// <summary>
        /// Generates adjacency matrix as CSV string
        /// </summary>
        private string GenerateAdjacencyMatrix()
        {
            var nodesList = CurrentGraph.Nodes.OrderBy(n => n.Name).ToList();
            var csv = new System.Text.StringBuilder();

            // Header row with node names
            csv.Append(",");
            foreach (var node in nodesList)
            {
                csv.Append($"\"{node.Name}\",");
            }
            csv.AppendLine();

            // Data rows
            foreach (var fromNode in nodesList)
            {
                csv.Append($"\"{fromNode.Name}\",");
                foreach (var toNode in nodesList)
                {
                    var linkExists = CurrentGraph.Links.Any(l => l.SourceNodeId == fromNode.Id && l.TargetNodeId == toNode.Id);
                    csv.Append(linkExists ? "1," : "0,");
                }
                csv.AppendLine();
            }

            return csv.ToString();
        }

        /// <summary>
        /// Copies the selected node to clipboard
        /// </summary>
        [RelayCommand]
        public void CopyNode()
        {
            if (SelectedNode == null)
                return;

            _clipboardNode = SelectedNode.Clone();
            CanPaste = true;
            StatusMessage = $"Node '{SelectedNode.Name}' copied to clipboard";
        }

        /// <summary>
        /// Cuts the selected node (copies and deletes)
        /// </summary>
        [RelayCommand]
        public void CutNode()
        {
            if (SelectedNode == null)
                return;

            CopyNode();
            DeleteNode();
            StatusMessage = $"Node cut to clipboard";
        }

        /// <summary>
        /// Pastes a node from clipboard with offset
        /// </summary>
        [RelayCommand]
        public void PasteNode()
        {
            if (_clipboardNode == null)
            {
                StatusMessage = "Nothing to paste";
                return;
            }

            var newNode = _clipboardNode.Clone();
            newNode.X += 50;  // Offset by 50 pixels
            newNode.Y += 50;

            var command = new AddNodeCommand(CurrentGraph, newNode);
            _commandHistory.Execute(command);

            StatusMessage = $"Node '{newNode.Name}' pasted";
            UpdateStatistics();
        }

        /// <summary>
        /// Event that fires when execution is requested
        /// The MainWindow will handle the actual execution logic
        /// </summary>
        public event EventHandler? ExecutionRequested;

        /// <summary>
        /// Command to start execution
        /// </summary>
        [RelayCommand]
        public void StartExecution()
        {
            StatusMessage = "Starting execution...";
            ExecutionRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Updates graph statistics
        /// </summary>
        private void UpdateStatistics()
        {
            GraphStatistics = CurrentGraph.GetStatistics();
        }

        /// <summary>
        /// Handles history changes
        /// </summary>
        private void OnHistoryChanged(object? sender, CommandHistoryChangedEventArgs e)
        {
            CanUndo = e.CanUndo;
            CanRedo = e.CanRedo;
            UndoDescription = e.UndoDescription ?? "Undo";
            RedoDescription = e.RedoDescription ?? "Redo";
        }

        /// <summary>
        /// Loads recent files
        /// </summary>
        private void LoadRecentFiles()
        {
            RecentFiles.Clear();
            var recent = _fileService.GetRecentFiles();
            foreach (var file in recent)
            {
                if (System.IO.File.Exists(file))
                    RecentFiles.Add(file);
            }
        }
    }

    /// <summary>
    /// Command to add a node
    /// </summary>
    public class AddNodeCommand : IUndoableCommand
    {
        private readonly Graph _graph;
        private readonly Node _node;

        public AddNodeCommand(Graph graph, Node node)
        {
            _graph = graph;
            _node = node;
        }

        public void Execute()
        {
            _graph.AddNode(_node);
        }

        public void Undo()
        {
            _graph.RemoveNode(_node.Id);
        }

        public string GetDescription() => $"Add node '{_node.Name}'";
    }

    /// <summary>
    /// Command to delete a node
    /// </summary>
    public class DeleteNodeCommand : IUndoableCommand
    {
        private readonly Graph _graph;
        private readonly Guid _nodeId;
        private Node? _nodeBackup;
        private readonly List<Link> _linksBackup = new();

        public DeleteNodeCommand(Graph graph, Guid nodeId)
        {
            _graph = graph;
            _nodeId = nodeId;
        }

        public void Execute()
        {
            var node = _graph.Nodes.FirstOrDefault(n => n.Id == _nodeId);
            if (node == null)
                return;

            _nodeBackup = new Node
            {
                Id = node.Id,
                Name = node.Name,
                Type = node.Type,
                X = node.X,
                Y = node.Y,
                Width = node.Width,
                Height = node.Height,
                JsonData = node.JsonData,
                Color = node.Color,
                CreatedAt = node.CreatedAt,
                ModifiedAt = node.ModifiedAt
            };

            // Backup connected links
            var connectedLinkIds = node.ConnectedLinks.ToList();
            foreach (var linkId in connectedLinkIds)
            {
                var link = _graph.Links.FirstOrDefault(l => l.Id == linkId);
                if (link != null)
                    _linksBackup.Add(link.Clone());
            }

            _graph.RemoveNode(_nodeId);
        }

        public void Undo()
        {
            if (_nodeBackup == null)
                return;

            _graph.AddNode(_nodeBackup);

            foreach (var link in _linksBackup)
            {
                _graph.AddLink(link);
            }
        }

        public string GetDescription() => "Delete node";
    }

    /// <summary>
    /// Command to add a link
    /// </summary>
    public class AddLinkCommand : IUndoableCommand
    {
        private readonly Graph _graph;
        private readonly Link _link;

        public AddLinkCommand(Graph graph, Link link)
        {
            _graph = graph;
            _link = link;
        }

        public void Execute()
        {
            _graph.AddLink(_link);
        }

        public void Undo()
        {
            _graph.RemoveLink(_link.Id);
        }

        public string GetDescription() => "Add link";
    }

    /// <summary>
    /// Command to delete a link
    /// </summary>
    public class DeleteLinkCommand : IUndoableCommand
    {
        private readonly Graph _graph;
        private readonly Guid _linkId;
        private Link? _linkBackup;

        public DeleteLinkCommand(Graph graph, Guid linkId)
        {
            _graph = graph;
            _linkId = linkId;
        }

        public void Execute()
        {
            var link = _graph.Links.FirstOrDefault(l => l.Id == _linkId);
            if (link != null)
                _linkBackup = link.Clone();

            _graph.RemoveLink(_linkId);
        }

        public void Undo()
        {
            if (_linkBackup == null)
                return;

            _graph.AddLink(_linkBackup);
        }

        public string GetDescription() => "Delete link";
    }
}
