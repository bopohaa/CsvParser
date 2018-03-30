using CsvParser.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvParser.Common
{
    internal class ChunkReader
    {
        private readonly Stream _source;
        private readonly Encoding _encoder;
        private readonly byte[] _buffer;
        private readonly ObjectPool<Chunk> _chunkPool;

        private byte[] _prevByteUnk;
        private bool _eof;

        public ChunkReader(Stream source, Encoding encoding, CsvReader.Config config)
        {
            var bufferSize = config.GetReadingBufferSize();
            _source = source;
            _encoder = Encoding.GetEncoding(encoding.CodePage, encoding.EncoderFallback, new CustomDecoderFallback(bufferSize, encoding.DecoderFallback));
            _buffer = new byte[bufferSize];
            _chunkPool = Chunk.CreatePool(config.ColumnSeparator, config.Quotes, bufferSize);
            _prevByteUnk = null;
            _eof = false;
        }

        public Chunk Read()
        {
            if (_eof)
                return null;

            int offset;

            if (_prevByteUnk != null)
            {
                offset = _prevByteUnk.Length;
                Array.Copy(_prevByteUnk, _buffer, offset);
            }
            else
                offset = 0;

            var len = ReadDataIntoBuffer(offset);

            return GetChunk(len);
        }


        public Task<Chunk> ReadAsync(CancellationToken cancellation)
        {
            if (_eof)
                return Task.FromResult<Chunk>(null);

            int offset;

            if (_prevByteUnk != null)
            {
                offset = _prevByteUnk.Length;
                Array.Copy(_prevByteUnk, _buffer, offset);
            }
            else
                offset = 0;

            return _source.ReadAsync(_buffer, offset, _buffer.Length - offset)
                .ContinueWith(t => GetChunk(t.Result + offset), cancellation, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
        }

        private Chunk GetChunk(int len)
        {
            var chunk = _chunkPool.Allocate();
            _prevByteUnk = chunk.Init(_encoder, _buffer, 0, len);
            if (len != _buffer.Length)
                _eof = true;

            return chunk;
        }

        private int ReadDataIntoBuffer(int offset)
        {
            while (true)
            {
                var len = _source.Read(_buffer, offset, _buffer.Length - offset);
                offset += len;
                if (len == 0) break;
            }

            return offset;
        }
    }
}
