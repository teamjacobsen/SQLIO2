using System;
using System.Buffers;

namespace SQLIO2
{
    static class ReadOnlySequenceExtensions
    {
        public static SequencePosition? PositionOfAny(this in ReadOnlySequence<byte> line, ReadOnlySpan<byte> anyOf)
        {
            if (line.IsSingleSegment)
            {
                var index = line.First.Span.IndexOfAny(anyOf);

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
