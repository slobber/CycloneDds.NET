using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the Quality of Service (QoS) settings for a DDS topic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class DdsQosAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reliability QoS policy.
    /// </summary>
    public DdsReliability Reliability { get; set; } = DdsReliability.Reliable;

    /// <summary>
    /// Gets or sets the durability QoS policy.
    /// </summary>
    public DdsDurability Durability { get; set; } = DdsDurability.Volatile;

    /// <summary>
    /// Gets or sets the history kind QoS policy.
    /// </summary>
    public DdsHistoryKind HistoryKind { get; set; } = DdsHistoryKind.KeepLast;

    /// <summary>
    /// Gets or sets the history depth. Only used when HistoryKind is KeepLast.
    /// </summary>
    public int HistoryDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether writer batching is enabled.
    /// When enabled, multiple writes are aggregated into larger RTPS messages
    /// for network efficiency. Samples are buffered until BatchMaxBytes or
    /// BatchMaxSamples is reached, or Flush() is called explicitly.
    /// </summary>
    public bool BatchEnable { get; set; }

    /// <summary>
    /// Gets or sets the maximum total serialized data bytes per batch.
    /// When the accumulated size exceeds this value, the batch is flushed
    /// automatically. 0 means unlimited (batch grows until BatchMaxSamples
    /// or explicit Flush()).
    /// </summary>
    public int BatchMaxBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of samples per batch.
    /// When the sample count reaches this value, the batch is flushed
    /// automatically. 0 means unlimited (batch grows until BatchMaxBytes
    /// or explicit Flush()).
    /// </summary>
    public int BatchMaxSamples { get; set; }
}
