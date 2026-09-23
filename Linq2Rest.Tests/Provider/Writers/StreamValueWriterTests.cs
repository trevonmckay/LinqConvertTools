namespace LinqConvertTools.Tests.Provider.Writers
{
    using System.IO;
    using LinqConvertTools.Provider.Writers;
    using NUnit.Framework;

    [TestFixture]
    public class StreamValueWriterTests
    {
        private static readonly byte[] Content = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        private readonly StreamValueWriter _writer = new();

        [Test]
        public void WhenWritingStreamThenWritesBase64BinaryLiteral()
        {
            using var stream = new MemoryStream(Content);

            var result = _writer.Write(stream);

            Assert.AreEqual("X'AQIDBAUGBwgJCg=='", result);
        }

        [Test]
        public void WhenWritingStreamPositionedAfterStartThenWritesWholeStream()
        {
            using var stream = new MemoryStream(Content);
            stream.Position = 4;

            var result = _writer.Write(stream);

            Assert.AreEqual("X'AQIDBAUGBwgJCg=='", result);
        }

        [Test]
        public void WhenStreamReturnsFewerBytesPerReadThenWritesWholeStream()
        {
            using var stream = new ChunkedStream(Content, maxBytesPerRead: 3, canSeek: true);

            var result = _writer.Write(stream);

            Assert.AreEqual("X'AQIDBAUGBwgJCg=='", result);
        }

        [Test]
        public void WhenWritingNonSeekableStreamThenWritesRemainingContent()
        {
            using var stream = new ChunkedStream(Content, maxBytesPerRead: 3, canSeek: false);

            var result = _writer.Write(stream);

            Assert.AreEqual("X'AQIDBAUGBwgJCg=='", result);
        }

        private sealed class ChunkedStream : Stream
        {
            private readonly MemoryStream _inner;
            private readonly int _maxBytesPerRead;
            private readonly bool _canSeek;

            public ChunkedStream(byte[] content, int maxBytesPerRead, bool canSeek)
            {
                _inner = new MemoryStream(content);
                _maxBytesPerRead = maxBytesPerRead;
                _canSeek = canSeek;
            }

            public override bool CanRead => true;

            public override bool CanSeek => _canSeek;

            public override bool CanWrite => false;

            public override long Length => _canSeek ? _inner.Length : throw new NotSupportedException();

            public override long Position
            {
                get => _canSeek ? _inner.Position : throw new NotSupportedException();
                set
                {
                    if (!_canSeek)
                    {
                        throw new NotSupportedException();
                    }

                    _inner.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, Math.Min(count, _maxBytesPerRead));
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _canSeek ? _inner.Seek(offset, origin) : throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
