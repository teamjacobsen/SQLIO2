using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net.Sockets;

namespace SQLIO2.Protocols
{
    class VideojetProtocol : LineBasedProtocol
    {
        private static readonly byte[] StartBytes = new byte[]
        {
            (byte)'$',
            (byte)'!',
            0x06,
            0x15
        };

        public VideojetProtocol(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<VideojetProtocol> logger)
            : base(next, serviceScopeFactory, logger)
        {
        }

        protected override void ProcessLine(TcpClient client, ReadOnlySequence<byte> line)
        {
            var position = PositionOf(line, StartBytes);

            if (position != null)
            {
                var data = line.Slice(position.Value);

                if (data.Length > 0)
                {
                    RunStack(client, data.ToArray());
                }
            }
        }

        private static SequencePosition? PositionOf(in ReadOnlySequence<byte> line, ReadOnlySpan<byte> anyOf)
        {
            if (line.IsSingleSegment)
            {
                var index = line.First.Span.IndexOfAny(StartBytes);

                if (index >= 0)
                {
                    return line.GetPosition(index);
                }

                return null;
            }
            else
            {
                // https://github.com/dotnet/corefx/blob/master/src/System.Memory/src/System/Buffers/BuffersExtensions.cs#L36

                var position = line.Start;
                var origin = position;

                while (line.TryGet(ref position, out var memory))
                {
                    var index = memory.Span.IndexOfAny(anyOf);

                    if (index >= 0)
                    {
                        return line.GetPosition(index, origin);
                    }
                    else if (position.GetObject() == null)
                    {
                        break;
                    }

                    origin = position;
                }

                return null;
            }
        }
    }
}
