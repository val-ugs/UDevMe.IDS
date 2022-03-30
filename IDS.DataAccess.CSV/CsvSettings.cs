using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.DataAccess.CSV
{
    public class CsvSettings
    {
        public CsvSettings(string path, char delimiter)
        {
            Path = path;
            Delimiter = delimiter;
        }

        public string Path { get; }
        public char Delimiter { get; }
    }
}
