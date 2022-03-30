using IDS.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
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

        public void AddRow(string fileName,double[] dataRow)
        {
            string fullPath = _path + "\\" + fileName;

            if (!File.Exists(fullPath))
            {
                File.Create(fullPath);
            }
            using (var writer = new StreamWriter(fullPath, true))
            {
                StringBuilder dataRowInCsv = new StringBuilder();

                for (int i = 0; i < dataRow.Length; i++)
                {
                    string dataMember = Convert.ToString(dataRow[i]);
                    if (i == dataRow.Length - 1)
                        dataRowInCsv.Append($"{dataMember}\n");
                    else
                        dataRowInCsv.Append($"{dataMember}{_delimiter}");
                }

                writer.WriteLine(dataRowInCsv);
            }
            
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
