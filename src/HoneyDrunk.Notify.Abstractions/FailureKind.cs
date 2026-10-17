namespace HoneyDrunk.Notify.Abstractions;

/// <summary>
/// Classifies the nature of a delivery failure to guide retry strategy.
/// </summary>
public enum FailureKind
{
    /// <summary>
    /// No failure occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// A transient failure that may succeed on retry (network timeout, throttling, etc.).
    /// </summary>
    Transient = 1,

    /// <summary>
    /// A permanent failure that will not succeed on retry (invalid address, hard bounce, etc.).
    /// </summary>
    Permanent = 2,

    /// <summary>
    /// A policy-enforced failure (suppression list, opt-out, compliance block, etc.).
    /// </summary>
    Policy = 3,
}
