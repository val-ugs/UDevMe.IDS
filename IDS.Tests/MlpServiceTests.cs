using IDS.BusinessLogic.Services;
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
        private ConvertToTrafficFeaturesService _convertService;
        private NormalizeFeaturesService _normalizeService;
        private MlpService _algorithmService;
        private AccuracyMetricService _metricService;

        [SetUp]
        public void Setup()
        {
            _convertService = new ConvertToTrafficFeaturesService();
            _normalizeService = new NormalizeFeaturesService();
            _algorithmService = new MlpService();
            _metricService = new AccuracyMetricService();
        }

        [Test]
        public void Predict__ShouldReturnTrue()
        {
            // arrange
            string trainCsvFileName = "UNSW_NB15_training-set.csv";
            string testCsvFileName = "UNSW_NB15_training-set.csv";
            TrafficData trainTrafficData = new TrafficData();
            TrafficData testTrafficData = new TrafficData();
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
            testTrafficData.Samples = testTrafficData.Samples.Take(300).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, hiddenLayersWithNeurons,
                                                   alpha, batchSize, learningRate, maxIterations, tol,
                                                   beta_1, beta_2, epsilon);
            var accuracy = _metricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.9);
        }
    }
}
