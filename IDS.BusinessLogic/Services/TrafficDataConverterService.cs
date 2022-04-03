using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class OneHotEncoder
    {
        Dictionary<int, string[]> _oneHotDictionary;
        public OneHotEncoder(List<string[]> trainData)
        {
            string[][] transposedData = Transpose(trainData);
            _oneHotDictionary = new Dictionary<int, string[]>();

            for (int i = 0; i < transposedData.Length; i++)
            {
                if (double.TryParse(transposedData[i][0], NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                    continue;

                _oneHotDictionary.Add(i, transposedData[i].Distinct().ToArray());
            }
        }

        private string[][] Transpose(List<string[]> data)
        {
            int numberOfRows = data.Count;
            int numberOfColumns = data[0].Length;

            string[][] transposedData = new string[numberOfColumns][];
            for (int i = 0; i < numberOfColumns; i++)
                transposedData[i] = new string[numberOfRows];

            for (int i = 0; i < numberOfRows; i++)
            {
                for (int j = 0; j < numberOfColumns; j++)
                {
                    transposedData[j][i] = data[i][j];
                }
            }

            return transposedData;
        }

        public double[] Encode(int index, string value)
        {
            string[] categories = _oneHotDictionary[index];
            int categoriesLength = categories.Length;
            double[] result = new double[categoriesLength];
            
            Array.Clear(result, 0, categoriesLength);
            
            for (int i = 0; i < categoriesLength; i++)
                if (categories[i] == value)
                    result[i] = 1;

            return result;
        }
    }

    public class TrafficDataConverterService : ITrafficDataConverterService
    {
        private OneHotEncoder _oneHotEncoder;
        private DataSource? _dataSource;
        private ClassificationType? _classificationType;
        private bool _hasOneHotEncode;

        private string _labelNameWithoutAttacks = "NORMAL";
        private List<string> _labelNamesWithAttacks = null;

        public TrafficDataConverterService(DataSource dataSource, ClassificationType classificationType, bool hasOneHotEncode)
        {
            _dataSource = dataSource;
            _classificationType = classificationType;
            _hasOneHotEncode = hasOneHotEncode;

            _labelNamesWithAttacks = new List<string>();
        }

        public TrafficData ConvertTrainData(List<string[]> data)
        {
            if (_hasOneHotEncode)
                _oneHotEncoder = new OneHotEncoder(data);

            return Convert(data, hasLabel: true);
        }

        public TrafficData ConvertTestData(List<string[]> data, bool hasLabel)
        {
            return Convert(data, hasLabel);
        }

        private TrafficData Convert(List<string[]> data, bool hasLabel)
        {
            switch (_dataSource)
            {
                case DataSource.RealTime:
                    return ConvertFromData(data, hasLabel);
                case DataSource.Kdd:
                case DataSource.Unsw:
                    data = data.Select(d => d.Take(d.Count() - 1).ToArray()).ToList(); // Remove last element in data row
                    return ConvertFromData(data, hasLabel);
            }

            return null;
        }

        private TrafficData ConvertFromData(List<string[]> data, bool hasLabel)
        {
            TrafficData trafficData = new TrafficData(data.Count);
            List<string> labelNamesForCurrentData = null;

            int endFeatureIndex = 0, labelNameIndex = 0;
            int dataRowLength = data[0].Length;
            if (hasLabel)
            {
                labelNameIndex = dataRowLength - 1;
                endFeatureIndex = dataRowLength - 2;
                labelNamesForCurrentData = data.Select(d => d[labelNameIndex].ToUpper()).Distinct().ToList();
            }
            else
                endFeatureIndex = dataRowLength - 1;

            if (labelNamesForCurrentData != null)
            {
                if (labelNamesForCurrentData.Contains(_labelNameWithoutAttacks.ToUpper()))
                    labelNamesForCurrentData.Remove(_labelNameWithoutAttacks.ToUpper());

                if (_labelNamesWithAttacks != null)
                    foreach (string labelName in labelNamesForCurrentData)
                        if (_labelNamesWithAttacks.Contains(labelName) == false)
                            _labelNamesWithAttacks.Add(labelName);
            }

            foreach (string[] dataRow in data)
            {
                List<double> features = GetFeaturesFromDataRow(dataRow, 0, endFeatureIndex);
                int label = hasLabel ? GetLabelByNames(dataRow[labelNameIndex]) : -1;

                trafficData.Samples.Add(new Sample(features, label));
            }

            return trafficData;
        }

        private List<double> GetFeaturesFromDataRow(string[] dataRow, int startIndex, int endIndex)
        {
            List<double> features = new List<double>();
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (double.TryParse(dataRow[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                    features.Add(number);
                else
                {
                    if (_hasOneHotEncode && _oneHotEncoder != null)
                        features.AddRange(_oneHotEncoder.Encode(i, dataRow[i]));
                }
            }

            return features;
        }

        private int GetLabelByNames(string labelName)
        {
            if (labelName.ToUpper() == _labelNameWithoutAttacks.ToUpper())
                return 0;

            if (_labelNamesWithAttacks != null)
                for (int i = 1; i <= _labelNamesWithAttacks.Count; i++)
                    if (labelName.ToUpper() == _labelNamesWithAttacks[i - 1].ToUpper())
                        return _classificationType switch
                        {
                            ClassificationType.Binary => 1,
                            ClassificationType.Multiclass => i,
                            _ => throw new Exception("Classification type not set")
                        };

            throw new Exception("Name of attacks not found");
        }

        public string GetNameByLabel(int label)
        {
            if (label == 0)
                return _labelNameWithoutAttacks;

            if (_labelNamesWithAttacks != null)
                if (label - 1 < _labelNamesWithAttacks.Count)
                    return _labelNamesWithAttacks[label - 1];

            throw new Exception("Label not found");
        }
    }
}
