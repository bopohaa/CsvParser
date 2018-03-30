using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CsvParser.Common
{
    internal class Chunk : IDisposable, IEnumerator<KeyValuePair<Chunk.TypeEnum, int>>
    {
        public enum TypeEnum : byte
        {
            data = 0,
            comma = 1,
            quotes = 2,
            cr = 3,
            lf = 4
        }

        private Encoding _encoding;
        private readonly ObjectPool<Chunk> _pool;
        private char[] _buffer;
        private int _counts;
        private int _retainCnt;

        private int _scanOffset;
        private TypeEnum _scanCurrent;
        private readonly char _columnSeparator;
        private readonly char _quotes;

        public char[] Data => _buffer;
        public int Count => _counts;

        public KeyValuePair<TypeEnum, int> Current => new KeyValuePair<TypeEnum, int>(_scanCurrent, _scanOffset);

        object IEnumerator.Current => Current;

        private Chunk(char column_separator, char quotes, int buffer_size, ObjectPool<Chunk> pool)
        {
            _pool = pool;
            _buffer = new char[buffer_size];
            _retainCnt = 0;

            Clean();
            Retain();

            _columnSeparator = column_separator == default(char) ? ',' : column_separator;
            _quotes = quotes == default(char) ? '"' : quotes;

        }

        public byte[] Init(Encoding encoding, byte[] data, int offset, int size)
        {
            _encoding = encoding;
            Clean();

            if (_buffer.Length < size)
                Array.Resize(ref _buffer, size);

            _counts = _encoding.GetChars(data, offset, size, _buffer, 0);
            return ((CustomDecoderFallback)_encoding.DecoderFallback).ResetBytesUnknown();
        }
        public void Dispose()
        {
            Release();
        }

        public Chunk Clone()
        {
            Retain();
            return this;
        }

        public bool MoveNext()
        {
            while (++_scanOffset < _counts)
            {
                var c = _buffer[_scanOffset];
                TypeEnum t;
                switch (c)
                {
                    case '\r':
                        t = TypeEnum.cr;
                        break;
                    case '\n':
                        t = TypeEnum.lf;
                        break;
                    default:
                        if (c == _columnSeparator)
                            t = TypeEnum.comma;
                        else if (c == _quotes)
                            t = TypeEnum.quotes;
                        else
                            t = TypeEnum.data;
                        break;

                }
                if (t != TypeEnum.data)
                {
                    _scanCurrent = t;
                    return true;
                }
            }

            return false;
        }

        public void Reset()
        {
            _scanOffset = -1;
        }


        private void Clean()
        {
            _counts = 0;
            Reset();
        }

        private void Retain()
        {
            _retainCnt++;
        }

        private void Release()
        {
            if (--_retainCnt == 0)
            {
                Retain();
                _pool.Free(this);
            }
        }

        public static ObjectPool<Chunk> CreatePool(char column_separator, char quotes, int buffer_size)
        {
            return new ObjectPool<Chunk>(pool => new Chunk(column_separator, quotes, buffer_size, pool));
        }
    }
}
