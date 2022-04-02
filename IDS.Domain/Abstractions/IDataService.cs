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
        public string[] GetFilenameList();
        public List<string[]> GetData(string fileName, bool hasHeaderRow);
    }
}
