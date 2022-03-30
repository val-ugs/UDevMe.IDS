using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface IDataRepository
    {
        public void AddRow(string fileName, double[] dataRow);
        public List<string[]> GetData(string fileName, bool hasHeaderRow);
    }
}
