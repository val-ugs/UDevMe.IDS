using IDS.BusinessLogic.Services;
using IDS.Domain.Models;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IDS.Tests
{
    public class KnnServiceTests
    {
        private ConvertToTrafficFeaturesService _convertService;
        private NormalizeFeaturesService _normalizeService;
        private KnnService _algorithmService;
        private AccuracyMetricService _metricService;

        [SetUp]
        public void Setup()
        {
            _convertService = new ConvertToTrafficFeaturesService();
            _normalizeService = new NormalizeFeaturesService();
            _algorithmService = new KnnService();
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
            int numNeighbors = 3;

            List<int> trueLabels = new List<int>();

            using (var reader = new StreamReader(trainCsvFileName))
            {
                string headerLine = reader.ReadLine();
                while (!reader.EndOfStream)
                {

                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    List<double> features =  _convertService.ConvertFromUnswData(values);

                    trainTrafficData.Samples.Add( new Sample(features.Take(features.Count - 1).ToList(),
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

            trainTrafficData.Samples = trainTrafficData.Samples.Take(1000).ToList();
            testTrafficData.Samples = testTrafficData.Samples.Take(1000).ToList();
            trueLabels = testTrafficData.Samples.Select(s => s.Label).ToList();

            trainTrafficData.Samples = _normalizeService.NormalizeTrainSamples(trainTrafficData.Samples, 0, 1);
            testTrafficData.Samples = _normalizeService.NormalizeTestSamples(testTrafficData.Samples);

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, numNeighbors);
            var accuracy = _metricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.9);
        }
    }
}