using IDS.BusinessLogic.Services;
using IDS.DataAccess.CSV;
using IDS.Domain.Abstractions;
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
        private DataService _dataService;
        private AccuracyMetricService _acuraccyMetricService;
        private F1ScoreMetricService _f1ScoreMetricService;

        [SetUp]
        public void Setup()
        {
            DataRepository csvDataRepository = new DataRepository(
                new CsvSettings("..\\..\\..\\..\\IDS.DataAccess.CSV\\CsvData", ',')
            );
            _dataService = new DataService(csvDataRepository);
            _acuraccyMetricService = new AccuracyMetricService();
            _f1ScoreMetricService = new F1ScoreMetricService();
        }

        [Test]
        public void Predict_UNSW_ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "UNSW_NB15_training-set.csv";
            string testCsvFileName = "UNSW_NB15_training-set.csv";
            int rounds = 5;
            int maxDepth = 10;
            int minSize = 3;
            double learningRate = 0.4;
            double lambda = 1.5;
            int gamma = 1;
            double nFeatureRatio = 0.8;

            TrafficDataConverterService converterService = new TrafficDataConverterService(DataSource.Unsw, ClassificationType.Binary, true);
            NormalizeFeaturesService normalizeService = new NormalizeFeaturesService(0, 1);
            IClassifierService classifierService = new XGBoostService(rounds, maxDepth, minSize, learningRate,
                                                                      lambda, gamma, nFeatureRatio);

            List<int> trueLabels = new List<int>();

            List<string[]> trainData = _dataService.GetData(trainCsvFileName, hasHeaderRow: true);
            TrafficData trainTrafficData = converterService.ConvertTrainData(trainData);

            List<string[]> testData = _dataService.GetData(testCsvFileName, hasHeaderRow: true);
            TrafficData testTrafficData = converterService.ConvertTestData(testData, hasLabel: true);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(1200).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(400).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = normalizeService.NormalizeTrainSamples(trainTrafficData.Samples);
            testTrafficData.Samples = normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            classifierService.Train(trainTrafficData);

            // act
            var result = classifierService.Predict(testTrafficData);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.75);
            Assert.IsTrue(f1Score >= 0.75);
        }

        [Test]
        public void Predict_KDD_ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "KDDTrain+.csv";
            string testCsvFileName = "KDDTest+.csv";
            int rounds = 5;
            int maxDepth = 10;
            int minSize = 3;
            double learningRate = 0.4;
            double lambda = 1.5;
            int gamma = 1;
            double nFeatureRatio = 0.8;

            TrafficDataConverterService converterService = new TrafficDataConverterService(DataSource.Kdd, ClassificationType.Binary, true);
            NormalizeFeaturesService normalizeService = new NormalizeFeaturesService(0, 1);
            IClassifierService classifierService = new XGBoostService(rounds, maxDepth, minSize, learningRate,
                                                                      lambda, gamma, nFeatureRatio);

            List<int> trueLabels = new List<int>();

            List<string[]> trainData = _dataService.GetData(trainCsvFileName, hasHeaderRow: true);
            TrafficData trainTrafficData = converterService.ConvertTrainData(trainData);

            List<string[]> testData = _dataService.GetData(testCsvFileName, hasHeaderRow: true);
            TrafficData testTrafficData = converterService.ConvertTestData(testData, hasLabel: true);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(1200).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(400).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = normalizeService.NormalizeTrainSamples(trainTrafficData.Samples);
            testTrafficData.Samples = normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            classifierService.Train(trainTrafficData);

            // act
            var result = classifierService.Predict(testTrafficData);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.75);
            Assert.IsTrue(f1Score >= 0.75);
        }
    }
}
