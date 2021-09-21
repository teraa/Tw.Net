using System.Buffers;
using Xunit;
using Twitch;

namespace Common.Tests
{
    public static class SequencePositionExtensions
    {
        public static string ToString(this System.SequencePosition position)
            => position.GetInteger().ToString();
    }

    public class BuffersExtensionsTests
    {
        [Fact]
        public void ValueLength2AtBeginning()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            //                                                ^--^
            var value = new byte[] { 0, 1 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(0, pos.Value.GetInteger());
        }

        [Fact]
        public void ValueLength2Inside()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            //                                                            ^--^
            var value = new byte[] { 4, 5 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(4, pos.Value.GetInteger());
        }

        [Fact]
        public void ValueLength2AtEnd()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            //                                                                        ^--^
            var value = new byte[] { 8, 9 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(8, pos.Value.GetInteger());
        }

        [Fact]
        public void ValueLength3AtBeginning()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        //                                                    ^-----^
            var value = new byte[] { 0, 1, 2 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(0, pos.Value.GetInteger());
        }

        [Fact]
        public void ValueLength3Inside()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        //                                                                ^-----^
            var value = new byte[] { 4, 5, 6 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(4, pos.Value.GetInteger());
        }

        [Fact]
        public void ValueLength3AtEnd()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            //                                                                     ^-----^
            var value = new byte[] { 7, 8, 9 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(7, pos.Value.GetInteger());
        }

        [Fact]
        public void MultipleOccurences_FindsFirst()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 3, 3, 6, 3, 3, 9 });
            //                                                         ^--^
            var value = new byte[] { 3, 3 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(3, pos.Value.GetInteger());
        }

        [Fact]
        public void MultiplePartialOccurrences_FindsComplete()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 3, 5, 3, 3, 3, 9 });
            //                                                                  ^-----^
            var value = new byte[] { 3, 3, 3 };
            var pos = seq.PositionOf(value);

            Assert.NotNull(pos);
            Assert.Equal(6, pos.Value.GetInteger());
        }

        [Fact]
        public void NonOccurring_Null()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            var value = new byte[] { 6, 5 };
            var pos = seq.PositionOf(value);

            Assert.Null(pos);
        }

        [Fact]
        public void Gap_Null()
        {
            //                                                0  1  2  3  4  5  6  7  8  9
            var seq = new ReadOnlySequence<byte>(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            //                                                   ^-   -^
            var value = new byte[] { 1, 3 };
            var pos = seq.PositionOf(value);

            Assert.Null(pos);
        }
    }
}
