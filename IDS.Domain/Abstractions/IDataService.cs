using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface IDataService
    {
        public bool AddRow(string fileName, double[] dataRow);
        public List<string[]> GetData(string fileName, bool hasHeaderRow);
    }
}
