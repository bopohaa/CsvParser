using System;
using System.Collections.Generic;
using System.Text;

namespace CsvParser.Common
{
    internal struct Column : IDisposable
    {
        private const int PARTS_COUNT = 16;

        public struct Part
        {
            public readonly Chunk Chunk;
            public readonly int Offset;
            public readonly int Size;

            public Part(Chunk chunk, int offset, int size)
            {
                Chunk = chunk;
                Offset = offset;
                Size = size;
            }
        }

        private Part[] _parts;
        private int _cnt;
        private string _value;

        public void AddPart(Chunk chunk, int offset, int size)
        {
            _value = null;
            var idx = _cnt++;
            if (_parts == null)
                _parts = new Part[PARTS_COUNT];
            else if (idx == _parts.Length)
                Array.Resize(ref _parts, _parts.Length + PARTS_COUNT);
            _parts[idx] = new Part(chunk.Clone(), offset, size);
        }

        public void Dispose()
        {
            for (var i = 0; i < _cnt; i++)
                _parts[i].Chunk.Dispose();
            _cnt = 0;
            _value = null;
        }

        public string Value
        {
            get
            {
                if (_value == null)
                    _value = GetValue();
                return _value;
            }
        }

        private string GetValue()
        {
            if (_cnt == 1)
                return GetValue(0);

            var sb = new StringBuilder();
            for (var i = 0; i < _cnt; i++)
                sb.Append(GetValue(i));

            return sb.ToString();
        }

        private string GetValue(int idx)
        {
            return new string(_parts[idx].Chunk.Data, _parts[idx].Offset, _parts[idx].Size);
        }
    }

}
