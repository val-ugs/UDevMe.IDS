﻿using IDS.BusinessLogic.Services;
using IDS.Domain.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Tests
{
    public class RandomForestServiceTests
    {
        private TrafficDataConverterService _convertService;
        private NormalizeFeaturesService _normalizeService;
        private RandomForestService _algorithmService;
        private AccuracyMetricService _acuraccyMetricService;
        private F1ScoreMetricService _f1ScoreMetricService;

        [SetUp]
        public void Setup()
        {
            _convertService = new TrafficDataConverterService();
            _normalizeService = new NormalizeFeaturesService();
            _algorithmService = new RandomForestService();
            _acuraccyMetricService = new AccuracyMetricService();
            _f1ScoreMetricService = new F1ScoreMetricService();
        }

        [Test]
        public void Predict__ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "UNSW_NB15_training-set.csv";
            string testCsvFileName = "UNSW_NB15_training-set.csv";
            int numTrees = 3;
            int maxDepth = 5;
            int minSize = 3;
            double partOfTrafficDataRatio = 0.5;

            List<int> trueLabels = new List<int>();

            List<string[]> trainData = new List<string[]>();

            using (var reader = new StreamReader(trainCsvFileName))
            {
                string headerRow = reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    string dataRow = reader.ReadLine();
                    string[] dataMembers = dataRow.Split(',');

                    trainData.Add(dataMembers);
                }
            }

            TrafficData trainTrafficData = _convertService.ConvertTrainData(trainData, DataSource.Unsw, ClassificationType.Binary, true);

            List<string[]> testData = new List<string[]>();

            using (var reader = new StreamReader(testCsvFileName))
            {
                string headerRow = reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    string dataRow = reader.ReadLine();
                    string[] dataMembers = dataRow.Split(',');

                    testData.Add(dataMembers);
                }
            }

            TrafficData testTrafficData = _convertService.ConvertTestData(testData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(1000).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(1000).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, numTrees,
                                                   maxDepth, minSize, partOfTrafficDataRatio);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.9);
            Assert.IsTrue(f1Score >= 0.9);
        }
    }
}
