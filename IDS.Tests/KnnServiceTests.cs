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
        private KnnService _algorithmService;
        private AccuracyMetricService _metricService;

        [SetUp]
        public void Setup()
        {
            _convertService = new ConvertToTrafficFeaturesService();
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

                    trainTrafficData.Features.Add(features.ToList());
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

                    trueLabels.Add((int)features[features.Count - 1]);
                    testTrafficData.Features.Add(features.ToList());
                }
            }

            trueLabels = trueLabels.Take(5000).ToList();
            trainTrafficData.Features = trainTrafficData.Features.Take(5000).ToList();
            testTrafficData.Features = testTrafficData.Features.Take(5000).ToList();

            // act
            var result = _algorithmService.Predict(trainTrafficData, testTrafficData, numNeighbors);
            var accuracy = _metricService.Calculate(trueLabels, result);

            // assert
            Assert.IsTrue(accuracy >= 0.9);
        }
    }
}