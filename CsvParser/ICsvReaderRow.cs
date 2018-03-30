using System;
using System.Collections.Generic;
using System.Text;

namespace CsvParser
{
    public interface ICsvReaderRow : IDisposable, IEnumerable<string>
    {
        string this[int idx] { get; }

        ICsvReaderRow Clone();

        int Count { get; }
    }
}
