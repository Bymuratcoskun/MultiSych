using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSych.Services.Implementations
{
    public class ThrottledStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly int _maxBytesPerSecond;
        private int _bytesProcessed;
        private DateTime _windowStart;

        public ThrottledStream(Stream baseStream, int maxBytesPerSecond)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _maxBytesPerSecond = maxBytesPerSecond;
            _windowStart = DateTime.UtcNow;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            Throttle(count);
            return _baseStream.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await ThrottleAsync(count, cancellationToken);
            return await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Throttle(count);
            _baseStream.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await ThrottleAsync(count, cancellationToken);
            await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);

        private void Throttle(int bytes)
        {
            if (_maxBytesPerSecond <= 0 || bytes <= 0) return;

            _bytesProcessed += bytes;
            var elapsed = DateTime.UtcNow - _windowStart;

            if (elapsed.TotalSeconds >= 1.0)
            {
                _windowStart = DateTime.UtcNow;
                _bytesProcessed = 0;
                return;
            }

            double expectedTime = (double)_bytesProcessed / _maxBytesPerSecond;
            double remainingTime = expectedTime - elapsed.TotalSeconds;

            if (remainingTime > 0)
            {
                Thread.Sleep((int)(remainingTime * 1000));
            }
        }

        private async Task ThrottleAsync(int bytes, CancellationToken cancellationToken)
        {
            if (_maxBytesPerSecond <= 0 || bytes <= 0) return;

            _bytesProcessed += bytes;
            var elapsed = DateTime.UtcNow - _windowStart;

            if (elapsed.TotalSeconds >= 1.0)
            {
                _windowStart = DateTime.UtcNow;
                _bytesProcessed = 0;
                return;
            }

            double expectedTime = (double)_bytesProcessed / _maxBytesPerSecond;
            double remainingTime = expectedTime - elapsed.TotalSeconds;

            if (remainingTime > 0)
            {
                await Task.Delay((int)(remainingTime * 1000), cancellationToken);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
