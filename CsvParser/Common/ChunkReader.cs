using CsvParser.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsvParser.Common
{
    internal class ChunkReader
    {
        private readonly Stream _source;
        private readonly Encoding _encoder;
        private readonly byte[] _buffer;
        private readonly ObjectPool<Chunk> _chunkPool;

        public ChunkReader(Stream source, Encoding encoding, int buffer_size, CsvReader.Config config)
        {
            _source = source;
            _encoder = Encoding.GetEncoding(encoding.CodePage, encoding.EncoderFallback, new CustomDecoderFallback(buffer_size, encoding.DecoderFallback));
            _buffer = new byte[buffer_size];
            _chunkPool = Chunk.CreatePool(config.ColumnSeparator, config.Quotes);
        }

        public IEnumerable<Chunk> Read()
        {
            byte[] prevByteUnk = null;
            int len;
            do
            {
                if (prevByteUnk != null)
                {
                    Array.Copy(prevByteUnk, _buffer, prevByteUnk.Length);
                    len = ReadDataIntoBuffer(prevByteUnk.Length);
                }
                else
                    len = ReadDataIntoBuffer(0);

                var chunk = _chunkPool.Allocate();
                prevByteUnk = chunk.Init(_encoder, _buffer, 0, len);

                yield return chunk;

                chunk.Dispose();
            }
            while (len == _buffer.Length);
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
