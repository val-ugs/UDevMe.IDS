using IDS.BusinessLogic.Services;
using IDS.DataAccess.CSV;
using IDS.Domain.Models;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IDS.Tests
{
    public class KnnServiceTests
    {
        private DataService _dataService;
        private TrafficDataConverterService _convertService;
        private NormalizeFeaturesService _normalizeService;
        private KnnService _algorithmService;
        private AccuracyMetricService _acuraccyMetricService;
        private F1ScoreMetricService _f1ScoreMetricService;

        [SetUp]
        public void Setup()
        {
            DataRepository csvDataRepository = new DataRepository(
                new CsvSettings("..\\..\\..\\..\\IDS.DataAccess.CSV\\Data", ',')
            );
            _dataService = new DataService(csvDataRepository);
            _convertService = new TrafficDataConverterService();
            _normalizeService = new NormalizeFeaturesService();
            _algorithmService = new KnnService();
            _acuraccyMetricService = new AccuracyMetricService();
            _f1ScoreMetricService = new F1ScoreMetricService();
        }

        [Test]
        public void Predict_UNSW_ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "UNSW_NB15_training-set.csv";
            string testCsvFileName = "UNSW_NB15_training-set.csv";
            int numberOfNeighbors = 3;

            List<int> trueLabels = new List<int>();

            List<string[]> trainData = _dataService.GetData(trainCsvFileName, hasHeaderRow: true);
            TrafficData trainTrafficData = _convertService.ConvertTrainData(trainData, DataSource.Unsw, ClassificationType.Binary, true);

            List<string[]> testData = _dataService.GetData(testCsvFileName, hasHeaderRow: true);
            TrafficData testTrafficData = _convertService.ConvertTestData(testData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(1000).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(1000).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, numberOfNeighbors);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.9);
            Assert.IsTrue(f1Score >= 0.9);
        }

        [Test]
        public void Predict_KDD_ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "KDDTrain+.csv";
            string testCsvFileName = "KDDTest+.csv";
            int numberOfNeighbors = 3;

            List<int> trueLabels = new List<int>();

            List<string[]> trainData = _dataService.GetData(trainCsvFileName, hasHeaderRow: false);
            TrafficData trainTrafficData = _convertService.ConvertTrainData(trainData, DataSource.Kdd, ClassificationType.Binary, true);

            List<string[]> testData = _dataService.GetData(testCsvFileName, hasHeaderRow: false);
            TrafficData testTrafficData = _convertService.ConvertTestData(testData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(5000).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(1000).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, numberOfNeighbors);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.75);
            Assert.IsTrue(f1Score >= 0.75);
        }
    }
}