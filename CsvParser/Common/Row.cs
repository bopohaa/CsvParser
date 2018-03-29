using System;
using System.Collections;
using System.Collections.Generic;

namespace CsvParser.Common
{
    internal class Row : ICsvReaderRow
    {
        private const int COLUMNS_COUNT = 16;

        private Column[] _columns;
        private int _cnt;
        private int _lastColumnSize;
        private readonly ObjectPool<Row> _pool;
        private int _retainCnt;
        private bool _nextColumn;

        public int Count => _cnt;
        public int LastColumnSize => _lastColumnSize;

        private Row(ObjectPool<Row> pool)
        {
            _pool = pool;
            _retainCnt = 0;
            _columns = new Column[COLUMNS_COUNT];

            _cnt = 0;
            _lastColumnSize = 0;
            _nextColumn = true;
            Retain();
        }

        public string this[int idx]
        {
            get
            {
                if (idx >= _cnt)
                    throw new IndexOutOfRangeException();
                return _columns[idx].Value;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            for (var i = 0; i < _cnt; i++)
            {
                yield return _columns[i].Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void AddColumnData(Chunk chunk, int offset, int length)
        {
            TryNextColumn();
            if (length == 0) return;
            _lastColumnSize += length;

            _columns[_cnt - 1].AddPart(chunk, offset, length);
        }

        public void EndColumnData()
        {
            _nextColumn = true;
            _lastColumnSize = 0;
        }

        private void TryNextColumn()
        {
            if (!_nextColumn)
                return;

            _nextColumn = false;

            if (_cnt == _columns.Length)
                Array.Resize(ref _columns, _columns.Length + COLUMNS_COUNT);

            _cnt++;
        }

        public ICsvReaderRow Clone()
        {
            Retain();
            return this;
        }

        public void Dispose()
        {
            Release();
        }

        private void Retain()
        {
            _retainCnt++;
        }

        private void Release()
        {
            if (--_retainCnt == 0)
            {
                Clear();
                Retain();
                _pool.Free(this);
            }
        }

        private void Clear()
        {
            for (var i = 0; i < _cnt; i++)
                _columns[i].Dispose();

            _cnt = 0;
            _lastColumnSize = 0;
            _nextColumn = true;
        }

        public static ObjectPool<Row> CreatePool()
        {
            return new ObjectPool<Row>(pool => new Row(pool));
        }
    }
}
