using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    public sealed class Counter : Collector<Counter.Child>, ICounter
    {
        public sealed class Child : ChildBase, ICounter
        {
            internal Child(Collector parent, Labels labels, Labels flattenedLabels, bool publish)
                : base(parent, labels, flattenedLabels, publish)
            {
                _identifier = CreateIdentifier();
            }

            private readonly byte[] _identifier;

            private ThreadSafeDouble _value;
            private Exemplar _exemplar = new();

            private protected override Task CollectAndSerializeImplAsync(IMetricsSerializer serializer,
                CancellationToken cancel)
            {
                Exemplar exemplar;
                lock (this)
                    exemplar = _exemplar;
                return serializer.WriteMetricAsync(_identifier, Value, exemplar, cancel);
            }

            public void Inc(double increment = 1.0, params (string, string)[] exemplar)
            {
                if (increment < 0.0)
                    throw new ArgumentOutOfRangeException(nameof(increment), "Counter value cannot decrease.");

                _value.Add(increment);

                if (exemplar.Length > 0)
                {
                    lock (this)
                        _exemplar.Update(exemplar, increment);
                }

                Publish();
            }

            public void IncTo(double targetValue)
            {
                _value.IncrementTo(targetValue);
                Publish();
            }

            public double Value => _value.Value;
        }

        private protected override Child NewChild(Labels labels, Labels flattenedLabels, bool publish)
        {
            return new Child(this, labels, flattenedLabels, publish);
        }

        internal Counter(string name, string help, string[]? labelNames, Labels staticLabels, bool suppressInitialValue)
            : base(name, help, labelNames, staticLabels, suppressInitialValue)
        {
        }

        public void Inc(double increment = 1, params (string, string)[] exemplar) =>
            Unlabelled.Inc(increment, exemplar);

        public void IncTo(double targetValue) => Unlabelled.IncTo(targetValue);
        public double Value => Unlabelled.Value;

        public void Publish() => Unlabelled.Publish();
        public void Unpublish() => Unlabelled.Unpublish();

        private protected override MetricType Type => MetricType.Counter;
    }
}