﻿using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus
{
    /// <remarks>
    /// Does NOT take ownership of the stream - caller remains the boss.
    /// </remarks>
    internal sealed class TextSerializer : IMetricsSerializer
    {
        private static readonly byte[] NewLine = new[] { (byte)'\n' };
        private static readonly byte[] Space = new[] { (byte)' ' };
        private static readonly byte[] ExemplarDelim = new[] { (byte)' ', (byte)'#' , (byte)' ',};

        public TextSerializer(Stream stream, bool openMetrics = false)
        {
            _stream = new Lazy<Stream>(() => stream);
            _openMetrics = openMetrics;
        }

        // Enables delay-loading of the stream, because touching stream in HTTP handler triggers some behavior.
        public TextSerializer(Func<Stream> streamFactory, bool openMetrics = false)
        {
            _stream = new Lazy<Stream>(streamFactory);
            _openMetrics = openMetrics;
        }

        public async Task FlushAsync(CancellationToken cancel)
        {
            // If we never opened the stream, we don't touch it on flush.
            if (!_stream.IsValueCreated)
                return;

            await _stream.Value.FlushAsync(cancel);
        }

        private readonly Lazy<Stream> _stream;
        private readonly bool _openMetrics;
        
        // # HELP name help
        // # TYPE name type
        public async Task WriteFamilyDeclarationAsync(byte[][] headerLines, CancellationToken cancel)
        {
            foreach (var line in headerLines)
            {
                await _stream.Value.WriteAsync(line, 0, line.Length, cancel);
                await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
            }
        }

        public Task WriteMetricAsync(byte[] identifier, double value, CancellationToken cancel)
        {
            return WriteMetricAsync(identifier, value, null, cancel);
        }

        // Reuse a buffer to do the UTF-8 encoding.
        // Maybe one day also ValueStringBuilder but that would be .NET Core only.
        // https://github.com/dotnet/corefx/issues/28379
        // Size limit guided by https://stackoverflow.com/questions/21146544/what-is-the-maximum-length-of-double-tostringd
        private readonly byte[] _stringBytesBuffer = new byte[32];

        // name{labelkey1="labelvalue1",labelkey2="labelvalue2"} 123.456
        public async Task WriteMetricAsync(byte[] identifier, double value, Exemplar? exemplar, CancellationToken cancel)
        {
            await _stream.Value.WriteAsync(identifier, 0, identifier.Length, cancel);
            await _stream.Value.WriteAsync(Space, 0, Space.Length, cancel);
            
            var valueAsString = value.ToString(CultureInfo.InvariantCulture);
            
            // TODO write all numbers in openmetrics format.
            var numBytes = PrometheusConstants.ExportEncoding
                .GetBytes(valueAsString, 0, valueAsString.Length, _stringBytesBuffer, 0);

            await _stream.Value.WriteAsync(_stringBytesBuffer, 0, numBytes, cancel);
            if (_openMetrics && exemplar is { IsValid: true })
            {
                // await _stream.Value.WriteAsync(ExemplarDelim, 0, ExemplarDelim.Length, cancel);
                // var data = PrometheusConstants.ExportEncoding.GetBytes(exemplar);
                // await _stream.Value.WriteAsync(data, 0, data.Length);
            }
            await _stream.Value.WriteAsync(NewLine, 0, NewLine.Length, cancel);
        }
    }
}
