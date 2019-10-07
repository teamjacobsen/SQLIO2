using System;
using System.Buffers;

namespace SQLIO2
{
    static class ReadOnlySequenceExtensions
    {
        public static SequencePosition? PositionOfAny(this in ReadOnlySequence<byte> sequence, ReadOnlySpan<byte> anyOf)
        {
            if (sequence.IsSingleSegment)
            {
                var index = sequence.First.Span.IndexOfAny(anyOf);

                if (index >= 0)
                {
                    return sequence.GetPosition(index);
                }

                return null;
            }
            else
            {
                // https://github.com/dotnet/corefx/blob/master/src/System.Memory/src/System/Buffers/BuffersExtensions.cs#L36

                var position = sequence.Start;
                var origin = position;

                while (sequence.TryGet(ref position, out var memory))
                {
                    var index = memory.Span.IndexOfAny(anyOf);

                    if (index >= 0)
                    {
                        return sequence.GetPosition(index, origin);
                    }
                    else if (position.GetObject() is null)
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
