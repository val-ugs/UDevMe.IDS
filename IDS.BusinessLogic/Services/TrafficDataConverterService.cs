﻿using IDS.Domain.Abstractions;
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

        public TrafficData ConvertTrainData(List<string[]> data, DataSource dataSource,
                                            ClassificationType classificationType, bool hasOneHotEncode)
        {
            if (hasOneHotEncode)
                _oneHotEncoder = new OneHotEncoder(data);
            _dataSource = dataSource;
            _classificationType = classificationType;
            return Convert(data);
        }

        public TrafficData ConvertTestData(List<string[]> data)
        {
            if (_dataSource == null)
                throw new Exception("Data type not set");
            if (_classificationType == null)
                throw new Exception("Classification type not set");
            return Convert(data);
        }

        private TrafficData Convert(List<string[]> data)
        {
            switch (_dataSource)
            {
                case DataSource.RealTime:
                    break;
                case DataSource.Unsw:
                    return ConvertFromUnswData(data);
            }

            return null;
        }

        private TrafficData ConvertFromUnswData(List<string[]> data)
        {
            TrafficData trafficData = new TrafficData(data.Count);

            string nameWithoutAttacks = "Normal";
            List<string> nameOfAttacks = new List<string>
            {
                "Fuzzers", "Analysis", "Backdoor", "DoS", "Exploits",
                "Generic", "Reconnaissance", "Shellcode", "Worms"
            };

            foreach (string[] dataRow in data)
            {
                List<double> features = GetFeaturesFromDataRow(dataRow, 0, dataRow.Length - 2);
                int label = GetLabelFromDataRow(dataRow, dataRow.Length - 2, nameWithoutAttacks, nameOfAttacks);

                trafficData.Samples.Add(new Sample(features, label));
            }

            return trafficData;
        }

        private List<double> GetFeaturesFromDataRow(string[] dataRow, int startIndex, int endIndex)
        {
            List<double> features = new List<double>();
            for (int i = startIndex; i < endIndex; i++)
            {
                if (double.TryParse(dataRow[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                    features.Add(number);
                else
                {
                    if (_oneHotEncoder != null)
                        features.AddRange(_oneHotEncoder.Encode(i, dataRow[i]));
                }
            }

            return features;
        }

        private double[] OneHotEncoder(string value)
        {
            //install 
            //return CategoricalCatalog.OneHotEncoding()
            return new double[] { 0, 0 };
        }

        private int GetLabelFromDataRow(string[] dataRow, int labelIndex,
                                        string nameWithoutAttacks = null, List<string> nameOfAttacks = null)
        {
            int label;

            if (int.TryParse(dataRow[labelIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out int number))
                label = _classificationType switch
                {
                    ClassificationType.Binary => number > 1 ? 1 : number,
                    ClassificationType.Multiclass => number,
                    _ => throw new Exception("Classification type not set")
                };
            else
            {
                if (nameWithoutAttacks == null)
                    throw new Exception("Name without attacks not set");
                if (nameOfAttacks == null)
                    throw new Exception("Name of attacks not set");

                label = GetLabelByNames(dataRow[labelIndex], nameWithoutAttacks, nameOfAttacks);
            }

            return label;
        }

        private int GetLabelByNames(string labelName, string nameWithoutAttacks, List<string> nameOfAttacks)
        {
            if (labelName == nameWithoutAttacks)
                return 0;

            for (int i = 1; i <= nameOfAttacks.Count; i++)
                if (labelName == nameOfAttacks[i - 1])
                    return _classificationType switch
                    {
                        ClassificationType.Binary => 1,
                        ClassificationType.Multiclass => i,
                        _ => throw new Exception("Classification type not set")
                    };

            throw new Exception("Name of attacks not found");
        }
    }
}
