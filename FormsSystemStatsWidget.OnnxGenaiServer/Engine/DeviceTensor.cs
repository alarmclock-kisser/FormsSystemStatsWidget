namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// Device-Tensor-Metadaten (R74).
/// Repräsentiert Tensoren die zwischen Stage0 und Stage1 übergeben werden (Boundary-Tensoren).
/// Kein ORT-Code — nur Metadaten für die C#-Infrastruktur.
/// </summary>
public sealed class DeviceTensor
{
    public string Name { get; init; } = string.Empty;
    public int[] Shape { get; init; } = [];
    public string ElementType { get; init; } = "float16";
    public int DeviceId { get; init; }
    public long SizeBytes { get; init; }

    public long TotalElements => Shape.Aggregate(1L, (acc, dim) => acc * dim);

    public static DeviceTensor FromBoundarySpec(string name, int batch, int seqLen, int hiddenDim, int deviceId, string elementType = "float16")
    {
        var shape = new[] { batch, seqLen, hiddenDim };
        var bytesPerElement = elementType == "float16" ? 2 : elementType == "float32" ? 4 : 1;
        return new DeviceTensor
        {
            Name = name,
            Shape = shape,
            ElementType = elementType,
            DeviceId = deviceId,
            SizeBytes = shape.Aggregate(1L, (a, d) => a * d) * bytesPerElement
        };
    }

    public override string ToString() => $"{Name} [{string.Join(",", Shape)}] {ElementType} @GPU{DeviceId} ({SizeBytes / 1024 / 1024:F1} MB)";
}
