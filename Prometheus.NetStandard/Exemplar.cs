using System;

namespace Prometheus;

internal struct Exemplar
{
    private static int MaxRunes = 128;

    private (string, string)[]? _labels;
    public (string, string)[]? Labels => _labels;

    private double _val;
    public double Val => _val;

    private double _timestamp;
    public double Timestamp => _timestamp;

    public Exemplar()
    {
        _labels = null;
        _val = 0;
        _timestamp = 0;
    }

    public void Update((string, string)[] labels, double val)
    {
        int tally = 0;
        foreach (var (key, value) in labels)
            tally += key.Length + value.Length;

        if (tally > MaxRunes)
            throw new ArgumentException("exemplar exceeds total rune limit", nameof(labels));

        _labels = labels;
        _val = val;
        _timestamp = ((double)DateTimeOffset.Now.ToUnixTimeMilliseconds()) / 1000;
    }

    public bool IsValid => _labels == null;
}