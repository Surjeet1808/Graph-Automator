using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GraphSimulator.ViewModels
{
    // Lightweight staging model for node property editing
    public class NodeEditModel : ObservableObject
    {
        private string name = "";
        public string Name { get => name; set => SetProperty(ref name, value); }

        private string type = "mouse_left_click";
        public string Type 
        { 
            get => type; 
            set 
            { 
                if (SetProperty(ref type, value))
                {
                    // Update color based on the new type
                    Color = Models.Graph.GetColorForNodeType(value);
                    
                    // Update name based on the new type
                    Name = FormatOperationTypeName(value);
                    
                    // Update JSON data when type changes
                    UpdateJsonDataForType();
                }
            } 
        }

        private string color = Models.Graph.GetColorForNodeType("mouse_left_click");
        public string Color { get => color; set => SetProperty(ref color, value); }

        private string jsonData = "{}";
        public string JsonData { get => jsonData; set => SetProperty(ref jsonData, value); }

        private double width = 120;
        public double Width { get => width; set => SetProperty(ref width, value); }

        private double height = 80;
        public double Height { get => height; set => SetProperty(ref height, value); }

        // Operation-specific properties
        private int xCoordinate = 0;
        public int XCoordinate { get => xCoordinate; set { SetProperty(ref xCoordinate, value); UpdateJsonData(); } }

        private int yCoordinate = 0;
        public int YCoordinate { get => yCoordinate; set { SetProperty(ref yCoordinate, value); UpdateJsonData(); } }

        private int scrollAmount = 120;
        public int ScrollAmount { get => scrollAmount; set { SetProperty(ref scrollAmount, value); UpdateJsonData(); } }

        private int keyCode = 0;
        public int KeyCode { get => keyCode; set { SetProperty(ref keyCode, value); UpdateJsonData(); } }

        private string textToType = "";
        public string TextToType { get => textToType; set { SetProperty(ref textToType, value); UpdateJsonData(); } }

        private int waitDuration = 1000;
        public int WaitDuration { get => waitDuration; set { SetProperty(ref waitDuration, value); UpdateJsonData(); } }

        private string customCode = "";
        public string CustomCode { get => customCode; set { SetProperty(ref customCode, value); UpdateJsonData(); } }

        private int delayBefore = 0;
        public int DelayBefore { get => delayBefore; set { SetProperty(ref delayBefore, value); UpdateJsonData(); } }

        private int delayAfter = 0;
        public int DelayAfter { get => delayAfter; set { SetProperty(ref delayAfter, value); UpdateJsonData(); } }

        private int priority = 0;
        public int Priority { get => priority; set { SetProperty(ref priority, value); UpdateJsonData(); } }

        private bool enabled = true;
        public bool Enabled { get => enabled; set { SetProperty(ref enabled, value); UpdateJsonData(); } }

        private string description = "";
        public string Description { get => description; set { SetProperty(ref description, value); UpdateJsonData(); } }

        private string graphFilePath = "";
        public string GraphFilePath { get => graphFilePath; set { SetProperty(ref graphFilePath, value); UpdateJsonData(); } }

        private int frequency = 1;
        public int Frequency { get => frequency; set { SetProperty(ref frequency, value < 1 ? 1 : value); UpdateJsonData(); } }

        private string nextNodeId = "";
        public string NextNodeId { get => nextNodeId; set { SetProperty(ref nextNodeId, value); UpdateJsonData(); } }

        private string previousNodeId = "";
        public string PreviousNodeId { get => previousNodeId; set { SetProperty(ref previousNodeId, value); UpdateJsonData(); } }

        private bool isUpdatingFromJson = false;

        private void UpdateJsonDataForType()
        {
            if (isUpdatingFromJson) return;

            // Set default values based on type
            switch (type.ToLower())
            {
                case "start":
                    // Start node doesn't need operation data
                    break;
                case "mouse_left_click":
                case "mouse_right_click":
                case "mouse_move":
                    XCoordinate = 100;
                    YCoordinate = 100;
                    break;
                case "scroll_up":
                case "scroll_down":
                case "scroll_left":
                case "scroll_right":
                    ScrollAmount = 120;
                    break;
                case "key_press":
                case "key_down":
                case "key_up":
                    KeyCode = 13; // Enter key
                    break;
                case "type_text":
                    TextToType = "Hello World";
                    break;
                case "wait":
                    WaitDuration = 1000;
                    break;
                case "custom_code":
                    CustomCode = "// Your custom code here";
                    break;
                case "graph":
                    GraphFilePath = "";
                    break;
            }

            UpdateJsonData();
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

        private void UpdateJsonData()
        {
            if (isUpdatingFromJson) return;

            try
            {
                var operation = new
                {
                    Type = this.type,
                    IntValues = GetIntValuesForType(),
                    StringValues = GetStringValuesForType(),
                    CustomCode = string.IsNullOrEmpty(customCode) ? null : customCode,
                    GraphFilePath = string.IsNullOrEmpty(graphFilePath) ? null : graphFilePath,
                    Priority = priority,
                    DelayBefore = delayBefore,
                    DelayAfter = delayAfter,
                    Frequency = frequency,
                    NextNodeId = string.IsNullOrEmpty(nextNodeId) ? null : nextNodeId,
                    PreviousNodeId = string.IsNullOrEmpty(previousNodeId) ? null : previousNodeId,
                    Description = string.IsNullOrEmpty(description) ? null : description,
                    Enabled = enabled
                };

                jsonData = System.Text.Json.JsonSerializer.Serialize(operation, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                OnPropertyChanged(nameof(JsonData));
            }
            catch
            {
                // Ignore serialization errors
            }
        }

        private int[] GetIntValuesForType()
        {
            switch (type.ToLower())
            {
                case "mouse_left_click":
                case "mouse_right_click":
                case "mouse_move":
                    return new[] { xCoordinate, yCoordinate };
                case "scroll_up":
                case "scroll_down":
                    return new[] { scrollAmount };
                case "key_press":
                case "key_down":
                case "key_up":
                    return new[] { keyCode };
                case "wait":
                    return new[] { waitDuration };
                default:
                    return Array.Empty<int>();
            }
        }

        private string[] GetStringValuesForType()
        {
            switch (type.ToLower())
            {
                case "type_text":
                    return new[] { textToType };
                default:
                    return Array.Empty<string>();
            }
        }

        public void LoadFromJsonData(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                isUpdatingFromJson = true;
                
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Type", out var typeElement))
                {
                    type = typeElement.GetString() ?? "mouse_left_click";
                }

                if (root.TryGetProperty("IntValues", out var intValuesElement) && intValuesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var intValues = intValuesElement.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    
                    switch (type.ToLower())
                    {
                        case "mouse_left_click":
                        case "mouse_right_click":
                        case "mouse_move":
                            if (intValues.Length >= 2)
                            {
                                xCoordinate = intValues[0];
                                yCoordinate = intValues[1];
                            }
                            break;
                        case "scroll_up":
                        case "scroll_down":
                            if (intValues.Length >= 1) scrollAmount = intValues[0];
                            break;
                        case "key_press":
                        case "key_down":
                        case "key_up":
                            if (intValues.Length >= 1) keyCode = intValues[0];
                            break;
                        case "wait":
                            if (intValues.Length >= 1) waitDuration = intValues[0];
                            break;
                    }
                }

                if (root.TryGetProperty("StringValues", out var stringValuesElement) && stringValuesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var stringValues = stringValuesElement.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
                    if (type.ToLower() == "type_text" && stringValues.Length >= 1)
                    {
                        textToType = stringValues[0];
                    }
                }

                if (root.TryGetProperty("CustomCode", out var customCodeElement))
                {
                    customCode = customCodeElement.GetString() ?? "";
                }

                if (root.TryGetProperty("GraphFilePath", out var graphFilePathElement))
                {
                    graphFilePath = graphFilePathElement.GetString() ?? "";
                }

                if (root.TryGetProperty("Priority", out var priorityElement))
                {
                    priority = priorityElement.GetInt32();
                }

                if (root.TryGetProperty("DelayBefore", out var delayBeforeElement))
                {
                    delayBefore = delayBeforeElement.GetInt32();
                }

                if (root.TryGetProperty("DelayAfter", out var delayAfterElement))
                {
                    delayAfter = delayAfterElement.GetInt32();
                }

                if (root.TryGetProperty("Frequency", out var frequencyElement))
                {
                    frequency = frequencyElement.GetInt32();
                    if (frequency < 1) frequency = 1;
                }

                if (root.TryGetProperty("NextNodeId", out var nextNodeIdElement))
                {
                    nextNodeId = nextNodeIdElement.GetString() ?? "";
                }

                if (root.TryGetProperty("PreviousNodeId", out var previousNodeIdElement))
                {
                    previousNodeId = previousNodeIdElement.GetString() ?? "";
                }

                if (root.TryGetProperty("Description", out var descriptionElement))
                {
                    description = descriptionElement.GetString() ?? "";
                }

                if (root.TryGetProperty("Enabled", out var enabledElement))
                {
                    enabled = enabledElement.GetBoolean();
                }

                OnPropertyChanged(nameof(XCoordinate));
                OnPropertyChanged(nameof(YCoordinate));
                OnPropertyChanged(nameof(ScrollAmount));
                OnPropertyChanged(nameof(KeyCode));
                OnPropertyChanged(nameof(TextToType));
                OnPropertyChanged(nameof(WaitDuration));
                OnPropertyChanged(nameof(CustomCode));
                OnPropertyChanged(nameof(GraphFilePath));
                OnPropertyChanged(nameof(DelayBefore));
                OnPropertyChanged(nameof(DelayAfter));
                OnPropertyChanged(nameof(Frequency));
                OnPropertyChanged(nameof(NextNodeId));
                OnPropertyChanged(nameof(PreviousNodeId));
                OnPropertyChanged(nameof(Priority));
                OnPropertyChanged(nameof(Enabled));
                OnPropertyChanged(nameof(Description));
            }
            catch
            {
                // Ignore parse errors
            }
            finally
            {
                isUpdatingFromJson = false;
            }
        }
    }
}
