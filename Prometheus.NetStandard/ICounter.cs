namespace Prometheus
{
    public interface ICounter : ICollectorChild
    {
        void Inc(double increment = 1, params (string, string)[] exemplar);
        void IncTo(double targetValue);
        double Value { get; }
    }
}
