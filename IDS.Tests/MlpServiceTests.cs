using IDS.BusinessLogic.Services;
using IDS.DataAccess.CSV;
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
    public class MlpServiceTests
    {
        private DataService _dataService;
        private TrafficDataConverterService _convertService;
        private NormalizeFeaturesService _normalizeService;
        private MlpService _algorithmService;
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
            _algorithmService = new MlpService();
            _acuraccyMetricService = new AccuracyMetricService();
            _f1ScoreMetricService = new F1ScoreMetricService();
        }

        [Test]
        public void Predict_UNSW_ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "UNSW_NB15_training-set.csv";
            string testCsvFileName = "UNSW_NB15_training-set.csv";
            List<int> hiddenLayersWithNeurons = new List<int> { 100, 50 };
            double alpha = 0.0001;
            int batchSize = 200;
            double learningRate = 0.001;
            int maxIterations = 1000;
            double tol = 0.0001;
            double beta_1 = 0.9;
            double beta_2 = 0.999;
            double epsilon = 0.00000001;


            List<int> trueLabels = new List<int>();

            List<string[]> trainData = _dataService.GetData(trainCsvFileName, hasHeaderRow: true);
            TrafficData trainTrafficData = _convertService.ConvertTrainData(trainData, DataSource.Unsw, ClassificationType.Binary, true);

            List<string[]> testData = _dataService.GetData(testCsvFileName, hasHeaderRow: true);
            TrafficData testTrafficData = _convertService.ConvertTestData(testData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(500).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(300).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, hiddenLayersWithNeurons,
                                                   alpha, batchSize, learningRate, maxIterations, tol,
                                                   beta_1, beta_2, epsilon);
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
            List<int> hiddenLayersWithNeurons = new List<int> { 100, 50 };
            double alpha = 0.0001;
            int batchSize = 200;
            double learningRate = 0.001;
            int maxIterations = 1000;
            double tol = 0.0001;
            double beta_1 = 0.9;
            double beta_2 = 0.999;
            double epsilon = 0.00000001;


            List<int> trueLabels = new List<int>();

            List<string[]> trainData = _dataService.GetData(trainCsvFileName, hasHeaderRow: true);
            TrafficData trainTrafficData = _convertService.ConvertTrainData(trainData, DataSource.Kdd, ClassificationType.Binary, true);

            List<string[]> testData = _dataService.GetData(testCsvFileName, hasHeaderRow: true);
            TrafficData testTrafficData = _convertService.ConvertTestData(testData);

            trainTrafficData.Samples = trainTrafficData.Samples.Take(1000).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(300).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, hiddenLayersWithNeurons,
                                                   alpha, batchSize, learningRate, maxIterations, tol,
                                                   beta_1, beta_2, epsilon);
            var accuracy = _acuraccyMetricService.Calculate(trueLabels, result);
            var f1Score = _f1ScoreMetricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.75);
            Assert.IsTrue(f1Score >= 0.75);
        }
    }
}
