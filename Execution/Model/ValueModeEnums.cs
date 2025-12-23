namespace GraphSimulator.Execution.Model
{
    /// <summary>
    /// Defines how a node gets its values
    /// </summary>
    public enum NodeValueMode
    {
        /// <summary>
        /// All values are hardcoded/static
        /// </summary>
        Static,

        /// <summary>
        /// All values come from a dynamic source
        /// </summary>
        Dynamic
    }

    /// <summary>
    /// Types of dynamic value sources
    /// </summary>
    public enum DynamicSourceType
    {
        /// <summary>
        /// Get values from API endpoint
        /// </summary>
        API,

        /// <summary>
        /// Get values from array based on date progression
        /// </summary>
        DateBasedArray,

        /// <summary>
        /// Get values from array based on execution iteration
        /// </summary>
        IterationBasedArray,

        /// <summary>
        /// Calculate values based on date/time expressions
        /// </summary>
        DateExpression,

        /// <summary>
        /// Evaluate C# expression
        /// </summary>
        Expression,

        /// <summary>
        /// Read values from file
        /// </summary>
        FileContent
    }
}
