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
    public class XGBoostServiceTests
    {
        private ConvertToTrafficFeaturesService _convertService;
        private NormalizeFeaturesService _normalizeService;
        private XGBoostService _algorithmService;
        private AccuracyMetricService _acuraccyMetricService;
        private F1ScoreMetricService _f1ScoreMetricService;

        [SetUp]
        public void Setup()
        {
            _convertService = new ConvertToTrafficFeaturesService();
            _normalizeService = new NormalizeFeaturesService();
            _algorithmService = new XGBoostService();
            _acuraccyMetricService = new AccuracyMetricService();
            _f1ScoreMetricService = new F1ScoreMetricService();
        }

        [Test]
        public void Predict__ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "UNSW_NB15_training-set.csv";
            string testCsvFileName = "UNSW_NB15_training-set.csv";
            TrafficData trainTrafficData = new TrafficData();
            TrafficData testTrafficData = new TrafficData();
            int rounds = 5;
            int maxDepth = 10;
            int minSize = 3; // childs
            double learningRate = 0.4;
            double lambda = 1.5;
            int gamma = 1;
            double nFeatureRatio = 0.8;

            List<int> trueLabels = new List<int>();

            using (var reader = new StreamReader(trainCsvFileName))
            {
                string headerLine = reader.ReadLine();
                while (!reader.EndOfStream)
                {

                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    List<double> features = _convertService.ConvertFromUnswData(values);

                    trainTrafficData.Samples.Add(new Sample(features.Take(features.Count - 1).ToList(),
                                                             (int)features[features.Count - 1]));
                }
            }
            using (var reader = new StreamReader(testCsvFileName))
            {
                string headerLine = reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    List<double> features = _convertService.ConvertFromUnswData(values); // last feature is label

                    testTrafficData.Samples.Add(new Sample(features.Take(features.Count - 1).ToList(),
                                                             (int)features[features.Count - 1]));
                }
            }

            trainTrafficData.Samples = trainTrafficData.Samples.Take(500).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(500).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, rounds,
                                                   maxDepth, minSize, learningRate,
                                                   lambda, gamma, nFeatureRatio);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.9);
            Assert.IsTrue(f1Score >= 0.9);
        }
    }
}
