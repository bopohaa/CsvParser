using CsvParser.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvParser
{
    public class CsvReader : IEnumerator<ICsvReaderRow>
    {
        private const int DEFAULT_BUFFER_SIZE = 64 * 1024;

        public struct Config
        {
#pragma warning disable 0649
            public bool WithQuotes;
            public char ColumnSeparator;
            public char Quotes;
            public int ReadinBufferSize;
#pragma warning restore 0649

            internal int GetReadingBufferSize()
            {
                return ReadinBufferSize == default(int) ? DEFAULT_BUFFER_SIZE : ReadinBufferSize;
            }
        }

        private enum State
        {
            start,
            qdata,
            qdatanext,
            end1,
            end2
        }

        private readonly ChunkReader _reader;
        private readonly Config _config;
        private Chunk _currentChunk;
        private int _currentChunkStart;
        private int _rowCnt;
        private readonly ObjectPool<Row> _rowPool;
        private ICsvReaderRow _currentRow;

        public ICsvReaderRow Current => _currentRow;

        object IEnumerator.Current => _currentRow;

        public CsvReader(Stream source, Encoding encoding, Config config = default(Config))
        {
            _currentChunk = null;
            _currentRow = null;
            _currentChunkStart = 0;
            _reader = new ChunkReader(source, encoding, config);
            _config = config;
            _rowCnt = 0;
            _rowPool = Row.CreatePool();
        }


        public void Dispose()
        {
            if (_currentChunk != null)
            {
                _currentChunk.Dispose();
                _currentChunk = null;
            }
            if (_currentRow != null)
            {
                _currentRow.Dispose();
                _currentRow = null;
            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            var row = _rowPool.Allocate();

            var state = State.start;
            while (state != State.end2)
            {
                if (_currentChunk == null)
                {
                    _currentChunk = _reader.Read();
                    if (_currentChunk == null)
                        break;
                }

                state = Parse(state, row);
            }

            return CompleteRead(row);
        }

        public Task<bool> MoveNextAsync()
        {
            return MoveNextAsync(CancellationToken.None);
        }

        public async Task<bool> MoveNextAsync(CancellationToken cancellation)
        {
            var row = _rowPool.Allocate();

            var state = State.start;
            while (state != State.end2)
            {
                if (_currentChunk == null)
                {
                    _currentChunk = await _reader.ReadAsync(cancellation);
                    if (_currentChunk == null)
                        break;
                }

                state = Parse(state, row);
            }

            return CompleteRead(row);
        }

        private bool CompleteRead(ICsvReaderRow row)
        {
            if (row.Count == 0)
            {
                row.Dispose();
                row = null;
            }
            else
                _rowCnt++;

            var prev = _currentRow;
            _currentRow = row;
            if (prev != null)
                prev.Dispose();

            return row != null;
        }

        private State Parse(State state, Row row)
        {
            var withQuotesNone = _config.WithQuotes ? 0 : 1;
            var withQuotesInc = _config.WithQuotes ? 1 : 0;

            while (state != State.end2)
            {
                if (!_currentChunk.MoveNext())
                {
                    var remain = _currentChunk.Count - _currentChunkStart;
                    if (remain > 0)
                        row.AddColumnData(_currentChunk, _currentChunkStart, remain);

                    _currentChunk.Dispose();
                    _currentChunk = null;
                    _currentChunkStart = 0;
                    return state;
                }
                var part = _currentChunk.Current;

                switch (state)
                {
                    case State.start:
                        switch (part.Key)
                        {
                            case Chunk.TypeEnum.quotes:
                                if (row.LastColumnSize > 0)
                                    throw new InvalidDataException($"Data found before quotation marks, at: {_rowCnt}");
                                _currentChunkStart = part.Value + withQuotesNone;
                                state = State.qdata;
                                break;
                            case Chunk.TypeEnum.comma:
                                row.AddColumnData(_currentChunk, _currentChunkStart, part.Value - _currentChunkStart);
                                row.EndColumnData();
                                _currentChunkStart = part.Value + 1;
                                break;
                            case Chunk.TypeEnum.cr:
                                row.AddColumnData(_currentChunk, _currentChunkStart, part.Value - _currentChunkStart);
                                _currentChunkStart = part.Value + 1;
                                state = State.end1;
                                break;
                            case Chunk.TypeEnum.lf:
                                row.AddColumnData(_currentChunk, _currentChunkStart, part.Value - _currentChunkStart);
                                _currentChunkStart = part.Value + 1;
                                state = State.end2;
                                break;
                            default:
                                throw new InvalidOperationException($"Unexpected operand, at: {_rowCnt}");
                        }
                        break;
                    case State.qdata:
                        if (part.Key == Chunk.TypeEnum.quotes)
                        {
                            row.AddColumnData(_currentChunk, _currentChunkStart, part.Value - _currentChunkStart + withQuotesInc);
                            _currentChunkStart = part.Value + 1;
                            state = State.qdatanext;
                        }
                        break;
                    case State.qdatanext:
                        if (part.Value - _currentChunkStart != 0)
                            throw new InvalidDataException($"Unexpected data between quotes, at: {_rowCnt}");
                        switch (part.Key)
                        {
                            case Chunk.TypeEnum.comma:
                                row.EndColumnData();
                                _currentChunkStart = part.Value + 1;
                                state = State.start;
                                break;
                            case Chunk.TypeEnum.quotes:
                                _currentChunkStart = part.Value;
                                state = State.qdata;
                                break;
                            case Chunk.TypeEnum.cr:
                                _currentChunkStart = part.Value + 1;
                                state = State.end1;
                                break;
                            case Chunk.TypeEnum.lf:
                                _currentChunkStart = part.Value + 1;
                                state = State.end2;
                                break;
                            default:
                                throw new InvalidOperationException($"Unexpected operand, at: {_rowCnt}");
                        }
                        break;
                    case State.end1:
                        if (part.Key != Chunk.TypeEnum.lf || (part.Value != 0 && part.Value - _currentChunkStart != 0))
                            throw new InvalidDataException($"Unexpected data between CRLF, at: {_rowCnt}");
                        row.EndColumnData();
                        _currentChunkStart = part.Value + 1;
                        state = State.end2;
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected state, at: {_rowCnt}");
                }
            }

            return state;
        }
    }
}
