using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Twitch
{
    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Memory/src/System/Buffers/BuffersExtensions.cs
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ReadOnlySequenceExtensions
    {
        /// <summary>
        /// Returns position of first occurrence of item in the <see cref="ReadOnlySequence{T}"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequencePosition? PositionOf<T>(in this ReadOnlySequence<T> source, ReadOnlySpan<T> value) where T : IEquatable<T>
        {
            if (value.Length == 0) return null;

            var remaining = source;

            while (true)
            {
                SequencePosition? firstPos = remaining.PositionOf(value[0]);
                if (firstPos is null) return null;

                remaining = remaining.Slice(remaining.GetPosition(1, firstPos.Value));

                bool success = true;

                for (int i = 1; i < value.Length; i++)
                {
                    var pos = remaining.PositionOf(value[i]);
                    if (pos is null || pos.Value.GetInteger() > firstPos.Value.GetInteger() + i)
                    {
                        success = false;
                        break;
                    }

                    remaining = remaining.Slice(remaining.GetPosition(1, pos.Value));
                }

                if (success) return firstPos;
            }
        }

        public static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message, Span<byte> delimiter)
        {
            SequencePosition? currentEndPos = buffer.PositionOf(delimiter);

            if (!currentEndPos.HasValue)
            {
                message = default;
                return false;
            }

            message = buffer.Slice(0, currentEndPos.Value);

            // Skip the message + delimiter
            SequencePosition nextStartPos = buffer.GetPosition(delimiter.Length, currentEndPos.Value);
            buffer = buffer.Slice(nextStartPos);

            return true;
        }
    }
}
