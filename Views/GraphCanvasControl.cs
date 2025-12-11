using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GraphSimulator.Models;
using GraphSimulator.Services;
using System.Linq;
using System.Diagnostics;

namespace GraphSimulator.Views
{
    /// <summary>
    /// Canvas control for rendering and interacting with the graph
    /// </summary>
    public class GraphCanvasControl : Canvas
    {
        public event Action<Node>? NodeSelected;
        public event Action<Node>? NodeRightClicked;
        public event Action<Link>? LinkSelected;
        public event Action<Link>? LinkRightClicked;
        public event Action? DeselectRequested;
        public event Action<double>? ZoomChanged;
        public event Action<Port>? PortClicked;
        public event Action<Node>? NodeDragFinished;
        public event Action<Point, Node?>? CanvasRightClicked;

        private Dictionary<Guid, FrameworkElement> _nodeVisuals = new();
        private Dictionary<Guid, Node> _nodeMap = new();
        private Dictionary<Guid, FrameworkElement> _linkVisuals = new();
        private Dictionary<Guid, Canvas> _linkCanvases = new();
        private Dictionary<Guid, Port> _portMap = new(); // Map port IDs to port objects
        private Point _lastMousePosition;
        private double _zoomLevel = 1.0;
        private Point _panOffset = new();
        private bool _isPanning = false;
        private Node? _draggingNode = null;
        private Stopwatch? _dragStartTimer = null;
        private const int DragDelayMs = 200;
        private int _mouseMoveThrottleMs = 10;
        private Path? _tempPreviewPath;
        private Port? _linkCreationSourcePort = null;
        private OrthogonalRoutingService _routingService = new();

        private Guid? _linkCreationSourceNodeId;
        public Guid? LinkCreationSourceNodeId
        {
            get => _linkCreationSourceNodeId;
            set
            {
                _linkCreationSourceNodeId = value;
                if (!_linkCreationSourceNodeId.HasValue)
                    RemoveTemporaryLinkPreview();
            }
        }

        public event Action<Point>? DoubleClickedOnCanvas;

        private bool _showGrid = true;
        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                InvalidateVisual();
            }
        }

        public object? SelectedNodeEditModel { get; set; }
        public Guid? SelectedNodeId { get; set; }
        public Node? LastSelectedNode { get; set; }
        public bool IsInLinkCreationMode { get; set; } // Track link creation mode for port highlighting

        public GraphCanvasControl()
        {
            this.Background = Brushes.White;
            this.ClipToBounds = true;
            this.MouseWheel += Canvas_MouseWheel;
            this.MouseMove += Canvas_MouseMove;
            this.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            this.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            this.MouseRightButtonDown += Canvas_MouseRightButtonDown;
            this.KeyDown += Canvas_KeyDown;
        }

        private void Canvas_KeyDown(object sender, KeyEventArgs e)
        {
            // Cancel link creation on Escape key
            if (e.Key == Key.Escape && IsInLinkCreationMode)
            {
                LinkCreationSourceNodeId = null;
                IsInLinkCreationMode = false;
                RemoveTemporaryLinkPreview();
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_showGrid)
            {
                var gridSize = 50;
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(230, 230, 230)), 1);

                // Draw vertical lines
                for (double x = 0; x < this.ActualWidth; x += gridSize)
                {
                    drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, this.ActualHeight));
                }

                // Draw horizontal lines
                for (double y = 0; y < this.ActualHeight; y += gridSize)
                {
                    drawingContext.DrawLine(pen, new Point(0, y), new Point(this.ActualWidth, y));
                }
            }
        }

        /// <summary>
        /// Renders the graph on the canvas
        /// </summary>
        public void RenderGraph(Graph graph)
        {
            if (graph == null) return;

            this.Children.Clear();
            _nodeVisuals.Clear();
            _nodeMap.Clear();
            _linkVisuals.Clear();
            _portMap.Clear();

            // Draw all links first (so they appear behind nodes)
            foreach (var link in graph.Links)
            {
                var linkVisual = CreateLinkVisual(link, graph);
                if (linkVisual != null)
                {
                    _linkVisuals[link.Id] = linkVisual;
                    this.Children.Add(linkVisual);
                }
            }

            // Draw all nodes
            foreach (var node in graph.Nodes)
            {
                var nodeVisual = CreateNodeVisual(node, SelectedNodeEditModel);
                _nodeVisuals[node.Id] = nodeVisual;
                _nodeMap[node.Id] = node;
                Canvas.SetLeft(nodeVisual, node.X);
                Canvas.SetTop(nodeVisual, node.Y);
                this.Children.Add(nodeVisual);
                
                // Register ports
                foreach (var port in node.Ports)
                {
                    port.CanvasPosition = _routingService.CalculatePortPosition(node, port.Position);
                    _portMap[port.Id] = port;
                }
                
                // Draw ports if:
                // 1. In link creation mode -> show all ports on all nodes
                // 2. Node is selected
                // 3. Node is the last selected node
                if (IsInLinkCreationMode || node.IsSelected || node == LastSelectedNode)
                {
                    RenderNodePorts(node, nodeVisual);
                }
            }
        }

        /// <summary>
        /// Creates a visual for a node with enhanced styling
        /// </summary>
        private FrameworkElement CreateNodeVisual(Node node, object? editModel = null)
        {
            // Check if this node is being edited; if so, apply staged values for preview
            var displayName = node.Name;
            var displayColor = node.Color;
            var displayWidth = node.Width;
            var displayHeight = node.Height;

            // Apply staged preview only when editModel is provided AND this node is the selected node being edited
            if (editModel != null && SelectedNodeId.HasValue && SelectedNodeId.Value == node.Id)
            {
                var editProps = editModel.GetType().GetProperties();
                foreach (var prop in editProps)
                {
                    if (prop.Name == "Name" && prop.GetValue(editModel) is string name)
                        displayName = name;
                    if (prop.Name == "Color" && prop.GetValue(editModel) is string color)
                        displayColor = color;
                    if (prop.Name == "Width" && prop.GetValue(editModel) is double width)
                        displayWidth = width;
                    if (prop.Name == "Height" && prop.GetValue(editModel) is double height)
                        displayHeight = height;
                }
            }

            var grid = new Grid
            {
                Width = displayWidth,
                Height = displayHeight,
                Tag = node.Id,
                Cursor = Cursors.Hand
            };
            
            // Add tooltip to show operation type
            var tooltip = new ToolTip
            {
                Content = $"Type: {node.Type}\nName: {displayName}"
            };
            grid.ToolTip = tooltip;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(displayColor);
                var brush = new SolidColorBrush(color);
                var isLinkSource = LinkCreationSourceNodeId.HasValue && LinkCreationSourceNodeId.Value == node.Id;
                // Use neutral stroke; highlight selection with a colored glow effect instead of a blue border
                var strokeColor = isLinkSource ? Colors.DodgerBlue : (node.IsLocked ? Colors.Red : Colors.DarkGray);
                var strokeBrush = new SolidColorBrush(strokeColor);
                var strokeThickness = isLinkSource ? 4 : 2;

                // Create drop shadow for better visual depth
                var shadowEffect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 6,
                    ShadowDepth = 3,
                    Opacity = 0.3
                };

                var rect = new Rectangle
                {
                    Width = displayWidth,
                    Height = displayHeight,
                    RadiusX = 8,
                    RadiusY = 8,
                    Fill = brush,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThickness,
                    Effect = shadowEffect
                };

                // If node is selected or is the last selected node, highlight with a black thin border
                if (node.IsSelected || node == LastSelectedNode)
                {
                    rect.Stroke = new SolidColorBrush(Colors.Black);
                    rect.StrokeThickness = 2;
                    rect.Effect = shadowEffect;
                }

                var textBlock = new TextBlock
                {
                    Text = displayName,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4)
                };

                grid.Children.Add(rect);
                grid.Children.Add(textBlock);

                // Mouse hover effect
                grid.MouseEnter += (s, e) =>
                {
                    rect.StrokeThickness = Math.Max(strokeThickness + 1, 3);
                    rect.Opacity = 0.95;
                };

                grid.MouseLeave += (s, e) =>
                {
                    rect.StrokeThickness = strokeThickness;
                    rect.Opacity = 1.0;
                };

                grid.MouseLeftButtonDown += (s, e) =>
                {
                    if (LinkCreationSourceNodeId.HasValue)
                    {
                        // In link creation mode, click on node body still completes link
                        // But ports are the proper way to complete links
                        // This is just a fallback
                    }
                    else
                    {
                        NodeSelected?.Invoke(node);
                        _dragStartTimer = Stopwatch.StartNew();
                    }
                    _lastMousePosition = e.GetPosition(this);
                    e.Handled = true;
                };

                grid.MouseRightButtonDown += (s, e) =>
                {
                    // select on right-click and notify right-click (for context menu)
                    NodeSelected?.Invoke(node);
                    NodeRightClicked?.Invoke(node);
                    e.Handled = true;
                };

                return grid;
            }
            catch
            {
                return new Grid { Width = node.Width, Height = node.Height };
            }
        }

        /// <summary>
        /// Creates a visual for a link using orthogonal L-shaped routing
        /// </summary>
        private FrameworkElement? CreateLinkVisual(Link link, Graph graph)
        {
            var sourceNode = graph.Nodes.FirstOrDefault(n => n.Id == link.SourceNodeId);
            var targetNode = graph.Nodes.FirstOrDefault(n => n.Id == link.TargetNodeId);

            if (sourceNode == null || targetNode == null)
                return null;

            var canvas = new Canvas 
            { 
                Width = 2000, 
                Height = 1500,
                IsHitTestVisible = true,
                Tag = link.Id,
                Cursor = Cursors.Hand
            };

            _linkCanvases[link.Id] = canvas;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(link.Color);
                // If link is selected, use blue highlight color
                if (link.IsSelected)
                {
                    color = Colors.Blue;
                }
                var lineBrush = new SolidColorBrush(color);
                var thickness = link.Thickness;
                
                // Increase thickness for selected links
                if (link.IsSelected)
                {
                    thickness += 2;
                }

                // Calculate start and end points using ports if available
                Point x1Y1, x2Y2;
                
                // Get source port position or use node center
                if (link.SourcePortId != Guid.Empty && _portMap.TryGetValue(link.SourcePortId, out var sourcePort))
                {
                    x1Y1 = sourcePort.CanvasPosition;
                }
                else
                {
                    // Use best port or node center
                    var srcX = sourceNode.X + sourceNode.Width / 2;
                    var srcY = sourceNode.Y + sourceNode.Height / 2;
                    x1Y1 = new Point(srcX, srcY);
                }
                
                // Get target port position or use node center
                if (link.TargetPortId != Guid.Empty && _portMap.TryGetValue(link.TargetPortId, out var targetPort))
                {
                    x2Y2 = targetPort.CanvasPosition;
                }
                else
                {
                    // Use best port or node center
                    var tgtX = targetNode.X + targetNode.Width / 2;
                    var tgtY = targetNode.Y + targetNode.Height / 2;
                    x2Y2 = new Point(tgtX, tgtY);
                }

                var x1 = x1Y1.X;
                var y1 = x1Y1.Y;
                var x2 = x2Y2.X;
                var y2 = x2Y2.Y;

                // Create orthogonal L-shaped path
                var midX = (x1 + x2) / 2;
                var pathGeometry = new PathGeometry();
                var pathFigure = new PathFigure { StartPoint = new Point(x1, y1) };

                // L-shape: horizontal first, then vertical
                pathFigure.Segments.Add(new LineSegment { Point = new Point(midX, y1) });
                pathFigure.Segments.Add(new LineSegment { Point = new Point(midX, y2) });
                pathFigure.Segments.Add(new LineSegment { Point = new Point(x2, y2) });

                pathGeometry.Figures.Add(pathFigure);

                var path = new Path
                {
                    Data = pathGeometry,
                    Stroke = lineBrush,
                    StrokeThickness = thickness,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = true,
                    Cursor = Cursors.Arrow,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                canvas.Children.Add(path);

                // Add arrowhead at center of the path (midpoint)
                // The L-shaped path goes: (x1, y1) -> (midX, y1) -> (midX, y2) -> (x2, y2)
                // The center point is approximately at (midX, (y1+y2)/2) but we need the actual path center
                var centerX = midX;
                var centerY = (y1 + y2) / 2;
                
                // Determine arrow direction based on the path direction at the center
                // At center, we're on the vertical segment going from y1 to y2
                var dx = 0; // vertical segment has no horizontal change
                var dy = y2 > y1 ? 1 : -1; // direction of vertical movement
                var angle = Math.Atan2(dy, dx);
                var arrowSize = 12;

                var arrowPath = new Path
                {
                    Fill = lineBrush,
                    Data = new PathGeometry(
                        new[] {
                            new PathFigure {
                                StartPoint = new Point(centerX, centerY),
                                Segments = new PathSegmentCollection {
                                    new LineSegment { Point = new Point(centerX - arrowSize * Math.Cos(angle - Math.PI/6), centerY - arrowSize * Math.Sin(angle - Math.PI/6)) },
                                    new LineSegment { Point = new Point(centerX - arrowSize * Math.Cos(angle + Math.PI/6), centerY - arrowSize * Math.Sin(angle + Math.PI/6)) },
                                    new LineSegment { Point = new Point(centerX, centerY) }
                                },
                                IsClosed = true
                            }
                        },
                        FillRule.EvenOdd,
                        null
                    ),
                    IsHitTestVisible = false
                };

                canvas.Children.Add(arrowPath);

                // Link label
                if (!string.IsNullOrEmpty(link.Label))
                {
                    var labelBlock = new TextBlock
                    {
                        Text = link.Label,
                        Foreground = Brushes.Black,
                        FontSize = 10,
                        Background = new SolidColorBrush(Colors.White),
                        Padding = new Thickness(2),
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(labelBlock, midX + 5);
                    Canvas.SetTop(labelBlock, Math.Min(y1, y2) - 15);
                    canvas.Children.Add(labelBlock);
                }

                canvas.MouseLeftButtonDown += (s, e) =>
                {
                    LinkSelected?.Invoke(link);
                    e.Handled = true;
                };

                canvas.MouseRightButtonDown += (s, e) =>
                {
                    LinkRightClicked?.Invoke(link);
                    e.Handled = true;
                };

                canvas.MouseEnter += (s, e) =>
                {
                    path.StrokeThickness = thickness + 1;
                    path.Opacity = 0.9;
                };

                canvas.MouseLeave += (s, e) =>
                {
                    path.StrokeThickness = thickness;
                    path.Opacity = 1.0;
                };

                return canvas;
            }
            catch
            {
                return new Canvas { Width = 2000, Height = 1500 };
            }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            const double zoomFactor = 1.1;
            const double minZoom = 0.1;
            const double maxZoom = 4.0;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                var newZoom = e.Delta > 0 ? _zoomLevel * zoomFactor : _zoomLevel / zoomFactor;
                _zoomLevel = Math.Clamp(newZoom, minZoom, maxZoom);

                var transform = new ScaleTransform(_zoomLevel, _zoomLevel);
                this.RenderTransform = transform;

                ZoomChanged?.Invoke(_zoomLevel);
                e.Handled = true;
            }
        }

        private Stopwatch? _lastMouseMoveTime = null;

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Throttle mouse move events to improve performance
            if (_lastMouseMoveTime != null && _lastMouseMoveTime.ElapsedMilliseconds < _mouseMoveThrottleMs)
                return;

            _lastMouseMoveTime = Stopwatch.StartNew();

            var currentPos = e.GetPosition(this);

            // If we're in link creation mode show temporary preview from source node to cursor
            if (LinkCreationSourceNodeId.HasValue && !_isPanning)
            {
                UpdateTemporaryLinkPreview(currentPos);
            }

            // Handle drag with hold-before-drag gesture
            if (_dragStartTimer != null && _dragStartTimer.IsRunning)
            {
                if (_dragStartTimer.ElapsedMilliseconds >= DragDelayMs)
                {
                    // Delay exceeded - start drag
                    _draggingNode = _nodeMap.Values.FirstOrDefault(n => 
                        Math.Abs(Canvas.GetLeft(_nodeVisuals[n.Id]) - (currentPos.X - _nodeVisuals[n.Id].ActualWidth / 2)) < 60 &&
                        Math.Abs(Canvas.GetTop(_nodeVisuals[n.Id]) - (currentPos.Y - _nodeVisuals[n.Id].ActualHeight / 2)) < 40
                    );
                    
                    if (_draggingNode == null)
                    {
                        _dragStartTimer = null;
                    }
                }
                else
                {
                    // Still within delay window
                    _lastMousePosition = currentPos;
                    return;
                }
            }

            // Drag node
            if (_draggingNode != null && e.LeftButton == MouseButtonState.Pressed && !_isPanning)
            {
                var deltaX = (currentPos.X - _lastMousePosition.X) / _zoomLevel;
                var deltaY = (currentPos.Y - _lastMousePosition.Y) / _zoomLevel;
                
                _draggingNode.X += deltaX;
                _draggingNode.Y += deltaY;

                if (_nodeVisuals.TryGetValue(_draggingNode.Id, out var nodeVisual))
                {
                    Canvas.SetLeft(nodeVisual, _draggingNode.X);
                    Canvas.SetTop(nodeVisual, _draggingNode.Y);
                }

                // Update connected links
                foreach (var linkId in _draggingNode.ConnectedLinks)
                {
                    if (_linkCanvases.TryGetValue(linkId, out var linkCanvas))
                    {
                        this.Children.Remove(linkCanvas);
                        this.Children.Add(linkCanvas);
                    }
                }

                _lastMousePosition = currentPos;
                e.Handled = true;
            }
            // Pan canvas
            else if (Keyboard.IsKeyDown(Key.Space) && e.LeftButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _panOffset.X += (currentPos.X - _lastMousePosition.X);
                _panOffset.Y += (currentPos.Y - _lastMousePosition.Y);

                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(_zoomLevel, _zoomLevel));
                transform.Children.Add(new TranslateTransform(_panOffset.X, _panOffset.Y));
                this.RenderTransform = transform;

                _lastMousePosition = currentPos;
                e.Handled = true;
            }
            else
            {
                _isPanning = false;
                _lastMousePosition = currentPos;
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            var hitTest = VisualTreeHelper.HitTest(this, pos);
            if (hitTest?.VisualHit == this)
            {
                // Double-click on empty canvas -> create node
                if (e.ClickCount == 2)
                {
                    DoubleClickedOnCanvas?.Invoke(pos);
                    e.Handled = true;
                    return;
                }

                DeselectRequested?.Invoke();
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingNode != null)
            {
                // Update port positions for this node
                foreach (var port in _draggingNode.Ports)
                {
                    port.CanvasPosition = _routingService.CalculatePortPosition(_draggingNode, port.Position);
                }
                // Notify that node drag finished so links can be rerouted
                NodeDragFinished?.Invoke(_draggingNode);
            }

            _draggingNode = null;
            _isPanning = false;
            _dragStartTimer?.Stop();
            _dragStartTimer = null;
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hitTest = VisualTreeHelper.HitTest(this, e.GetPosition(this));
            if (hitTest?.VisualHit == this)
            {
                CanvasRightClicked?.Invoke(e.GetPosition(this), null);
                e.Handled = true;
            }
        }

        private void UpdateTemporaryLinkPreview(Point? currentPos)
        {
            if (!LinkCreationSourceNodeId.HasValue)
                return;

            if (!_nodeMap.TryGetValue(LinkCreationSourceNodeId.Value, out var sourceNode))
            {
                RemoveTemporaryLinkPreview();
                return;
            }

            // Get best source port position
            var sourcePort = sourceNode.Ports.FirstOrDefault();
            var x1 = _routingService.CalculatePortPosition(sourceNode, sourcePort?.Position ?? PortPosition.Right).X;
            var y1 = _routingService.CalculatePortPosition(sourceNode, sourcePort?.Position ?? PortPosition.Right).Y;
            var x2 = currentPos?.X ?? x1;
            var y2 = currentPos?.Y ?? y1;

            // Use orthogonal L-shaped routing for preview
            var midX = (x1 + x2) / 2;
            var pg = new PathGeometry();
            var pf = new PathFigure { StartPoint = new Point(x1, y1) };
            pf.Segments.Add(new LineSegment { Point = new Point(midX, y1) });
            pf.Segments.Add(new LineSegment { Point = new Point(midX, y2) });
            pf.Segments.Add(new LineSegment { Point = new Point(x2, y2) });
            pg.Figures.Add(pf);

            if (_tempPreviewPath == null)
            {
                _tempPreviewPath = new Path
                {
                    Stroke = new SolidColorBrush(Colors.DodgerBlue),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    IsHitTestVisible = false
                };
                this.Children.Add(_tempPreviewPath);
            }

            _tempPreviewPath.Data = pg;
            
            // Auto-reveal ports on nearby nodes
            RevealNearbyPorts(new Point(x2, y2), sourceNode.Id);
        }

        /// <summary>
        /// Reveals ports on nearby nodes during link creation
        /// </summary>
        private void RevealNearbyPorts(Point cursorPos, Guid excludeNodeId)
        {
            const double revealRadius = 150;
            
            foreach (var node in _nodeMap.Values)
            {
                if (node.Id == excludeNodeId)
                    continue;

                var nodeCenter = new Point(node.X + node.Width / 2, node.Y + node.Height / 2);
                var distance = Math.Sqrt(
                    Math.Pow(cursorPos.X - nodeCenter.X, 2) +
                    Math.Pow(cursorPos.Y - nodeCenter.Y, 2)
                );

                // If nearby node is not selected, visually indicate ports are available
                // by temporarily rendering subtle port hints
                if (distance < revealRadius && !node.IsSelected && _nodeVisuals.TryGetValue(node.Id, out var visual))
                {
                    // Ports will be shown with a visual hint if needed
                    // For now, proximity-based logic is in place for future enhancement
                }
            }
        }

        private void RemoveTemporaryLinkPreview()
        {
            if (_tempPreviewPath != null)
            {
                this.Children.Remove(_tempPreviewPath);
                _tempPreviewPath = null;
            }
        }

        /// <summary>
        /// Renders the four ports on a selected node
        /// </summary>
        private void RenderNodePorts(Node node, FrameworkElement nodeVisual)
        {
            if (node.Ports == null || node.Ports.Count == 0)
                return;

            foreach (var port in node.Ports)
            {
                var portPos = _routingService.CalculatePortPosition(node, port.Position);
                
                // When in link creation mode, highlight target ports in blue
                var isSourceNode = LinkCreationSourceNodeId.HasValue && LinkCreationSourceNodeId.Value == node.Id;
                var isTargetNode = IsInLinkCreationMode && !isSourceNode;
                var portColor = isTargetNode ? Colors.DeepSkyBlue : Colors.Gray;
                
                var portVisual = new Ellipse
                {
                    Width = port.Radius * 2,
                    Height = port.Radius * 2,
                    Fill = new SolidColorBrush(portColor),
                    Stroke = new SolidColorBrush(Colors.DarkGray),
                    StrokeThickness = 1,
                    IsHitTestVisible = true,
                    Cursor = Cursors.Hand,
                    Tag = port
                };

                Canvas.SetLeft(portVisual, portPos.X - port.Radius);
                Canvas.SetTop(portVisual, portPos.Y - port.Radius);

                portVisual.MouseLeftButtonDown += (s, e) =>
                {
                    PortClicked?.Invoke(port);
                    e.Handled = true;
                };

                portVisual.MouseEnter += (s, e) =>
                {
                    portVisual.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                    portVisual.StrokeThickness = 2;
                };

                portVisual.MouseLeave += (s, e) =>
                {
                    portVisual.Fill = new SolidColorBrush(isTargetNode ? Colors.DeepSkyBlue : Colors.Gray);
                    portVisual.StrokeThickness = 1;
                };

                this.Children.Add(portVisual);
            }
        }
    }
}

