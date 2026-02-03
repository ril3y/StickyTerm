namespace StickyTerm.Exceptions;

/// <summary>
/// Exception thrown when an operation requires administrator privileges.
/// </summary>
public class ElevationRequiredException : Exception
{
    /// <summary>
    /// Gets a description of the operation that requires elevation.
    /// </summary>
    public string Operation { get; }

    public ElevationRequiredException(string operation)
        : base($"Administrator privileges required to {operation}")
    {
        Operation = operation;
    }

    public ElevationRequiredException(string operation, Exception innerException)
        : base($"Administrator privileges required to {operation}", innerException)
    {
        Operation = operation;
    }
}
