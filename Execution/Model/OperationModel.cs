namespace GraphSimulator.Execution.Model
{
    /// <summary>
    /// Represents an automation operation to be executed
    /// </summary>
    public class OperationModel
    {
        /// <summary>
        /// Type of operation to perform
        /// Supported types: mouse_left_click, mouse_right_click, mouse_move, 
        /// scroll_up, scroll_down, key_press, key_down, key_up, type_text, wait
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Integer values for the operation (e.g., coordinates, key codes, scroll amount)
        /// </summary>
        public int[] IntValues { get; set; } = Array.Empty<int>();

        /// <summary>
        /// String values for the operation (e.g., text to type)
        /// </summary>
        public string[] StringValues { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Custom code to execute (for advanced scenarios)
        /// </summary>
        public string? CustomCode { get; set; }

        /// <summary>
        /// Priority for execution ordering (lower = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Delay in milliseconds before executing this operation
        /// </summary>
        public int DelayBefore { get; set; } = 0;

        /// <summary>
        /// Delay in milliseconds after executing this operation
        /// </summary>
        public int DelayAfter { get; set; } = 0;

        /// <summary>
        /// Optional description of what this operation does
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether this operation should be executed
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Path to graph file for graph-type operations
        /// </summary>
        public string? GraphFilePath { get; set; }

        /// <summary>
        /// Number of times to repeat this operation
        /// </summary>
        public int Frequency { get; set; } = 1;

        /// <summary>
        /// ID of the next node to execute
        /// </summary>
        public string? NextNodeId { get; set; }

        /// <summary>
        /// ID of the previous node that should execute before this one
        /// </summary>
        public string? PreviousNodeId { get; set; }
    }

    /// <summary>
    /// Builder class for creating OperationModel instances with a fluent API
    /// </summary>
    public class OperationBuilder
    {
        private readonly OperationModel _operation = new OperationModel();

        public static OperationBuilder Create() => new OperationBuilder();

        public OperationBuilder LeftClick(int x, int y)
        {
            _operation.Type = "mouse_left_click";
            _operation.IntValues = new[] { x, y };
            return this;
        }

        public OperationBuilder RightClick(int x, int y)
        {
            _operation.Type = "mouse_right_click";
            _operation.IntValues = new[] { x, y };
            return this;
        }

        public OperationBuilder MoveMouse(int x, int y)
        {
            _operation.Type = "mouse_move";
            _operation.IntValues = new[] { x, y };
            return this;
        }

        public OperationBuilder ScrollUp(int amount = 120)
        {
            _operation.Type = "scroll_up";
            _operation.IntValues = new[] { amount };
            return this;
        }

        public OperationBuilder ScrollDown(int amount = 120)
        {
            _operation.Type = "scroll_down";
            _operation.IntValues = new[] { amount };
            return this;
        }

        public OperationBuilder PressKey(byte keyCode)
        {
            _operation.Type = "key_press";
            _operation.IntValues = new[] { (int)keyCode };
            return this;
        }

        public OperationBuilder KeyDown(byte keyCode)
        {
            _operation.Type = "key_down";
            _operation.IntValues = new[] { (int)keyCode };
            return this;
        }

        public OperationBuilder KeyUp(byte keyCode)
        {
            _operation.Type = "key_up";
            _operation.IntValues = new[] { (int)keyCode };
            return this;
        }

        public OperationBuilder TypeText(string text)
        {
            _operation.Type = "type_text";
            _operation.StringValues = new[] { text };
            return this;
        }

        public OperationBuilder Wait(int milliseconds)
        {
            _operation.Type = "wait";
            _operation.IntValues = new[] { milliseconds };
            return this;
        }

        public OperationBuilder WithPriority(int priority)
        {
            _operation.Priority = priority;
            return this;
        }

        public OperationBuilder WithDelayBefore(int milliseconds)
        {
            _operation.DelayBefore = milliseconds;
            return this;
        }

        public OperationBuilder WithDelayAfter(int milliseconds)
        {
            _operation.DelayAfter = milliseconds;
            return this;
        }

        public OperationBuilder WithDescription(string description)
        {
            _operation.Description = description;
            return this;
        }

        public OperationBuilder Enabled(bool enabled = true)
        {
            _operation.Enabled = enabled;
            return this;
        }

        public OperationModel Build() => _operation;
    }
}