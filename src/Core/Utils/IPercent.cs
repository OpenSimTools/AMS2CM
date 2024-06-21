namespace Core.Utils;

public interface IPercent
{
    /// <summary>
    /// Value guaranteed to be between 0.0 and 1.0
    /// </summary>
    public double Percent { get; }
}
