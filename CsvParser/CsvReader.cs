using CsvParser.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsvParser
{
    public class CsvReader : IEnumerator<ICsvReaderRow>
    {
        public struct Config
        {
            public bool WithQuotes;
            public char ColumnSeparator;
            public char Quotes;
        }

        private enum State
        {
            start,
            qdata,
            qdatanext,
            end1,
            end2
        }

        private const int BUFFER_SIZE = 1024 * 64;
        private readonly ChunkReader _reader;
        private readonly Config _config;
        private Row _current;
        private IEnumerator<Chunk> _data;
        private Chunk _currentChunk;
        private int _currentChunkStart;
        private int _rowCnt;
        private readonly ObjectPool<Row> _rowPool;

        public CsvReader(Stream source, Encoding encoding, Config config = default(Config))
        {
            _current = null;
            _data = null;
            _currentChunk = null;
            _reader = new ChunkReader(source, encoding, BUFFER_SIZE, config);
            _config = config;
            _rowCnt = 0;
            _rowPool = Row.CreatePool();
        }

        public ICsvReaderRow Current => _current;

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            if (_currentChunk != null)
            {
                _currentChunk.Dispose();
                _currentChunk = null;
            }
            if (_data != null)
            {
                _data.Dispose();
                _data = null;
            }

            if (_current != null)
            {
                _current.Dispose();
                _current = null;
            }
        }

        public bool MoveNext()
        {
            if (_current != null)
                _current.Dispose();

            _current = GetNextRow();

            return _current != null;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        private Row GetNextRow()
        {
            var row = _rowPool.Allocate();

            if (_data == null)
                _data = _reader.Read().GetEnumerator();


            var withQuotesNone = _config.WithQuotes ? 0 : 1;
            var withQuotesInc = _config.WithQuotes ? 1 : 0;
            var state = State.start;
            while (state != State.end2)
            {
                if (_currentChunk == null)
                {
                    if (!_data.MoveNext())
                        break;

                    _currentChunk = _data.Current.Clone();
                    _currentChunkStart = 0;
                }
                if (!_currentChunk.MoveNext())
                {
                    var remain = _currentChunk.Count - _currentChunkStart;
                    if (remain > 0)
                        row.AddColumnData(_currentChunk, _currentChunkStart, remain);

                    _currentChunk.Dispose();
                    _currentChunk = null;
                    continue;
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

            if (row.Count == 0)
            {
                row.Dispose();
                return null;
            }

            _rowCnt++;

            return row;
        }
    }
}
