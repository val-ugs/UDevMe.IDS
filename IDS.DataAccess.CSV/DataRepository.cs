using IDS.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IDS.DataAccess.CSV
{
    public class DataRepository : IDataRepository
    {
        private readonly string _path;
        private readonly char _delimiter;

        public DataRepository(CsvSettings csvSettings)
        {
            _path = csvSettings.Path;
            _delimiter = csvSettings.Delimiter;
        }

        public string[] GetFilenameList()
        {
            return Directory.GetFiles(_path).Select(file => Path.GetFileName(file)).ToArray();
        }

        public List<string[]> GetData(string fileName, bool hasHeaderRow)
        {
            string fullPath = _path + "\\" + fileName;
            List<string[]> outputData = new List<string[]>();

            using (var reader = new StreamReader(fullPath))
            {
                if (hasHeaderRow)
                {
                    string headerRow = reader.ReadLine();
                }
                while (!reader.EndOfStream)
                {
                    string dataRow = reader.ReadLine();
                    string[] dataMembers = dataRow.Split(_delimiter);

                    outputData.Add(dataMembers);
                }
            }

            return outputData;
        }
    }
}
