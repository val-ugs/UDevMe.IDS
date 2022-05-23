using IDS.BusinessLogic.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Tests
{
    public class AccuracyMetricServiceTests
    {
        private AccuracyMetricService _accuracyMetricService;

        [SetUp]
        public void Setup()
        {
            _accuracyMetricService = new AccuracyMetricService();
        }

        [Test]
        public void Calculate_Binary_ShouldReturnTrue()
        {
            // arrange
            List<int> trueLabels = new List<int>() { 0, 1, 0, 0, 1};
            List<int> predictedLabels = new List<int> { 0, 1, 0, 1, 0 };

            double expectedResult = 0.6;

            // act
            double result = _accuracyMetricService.Calculate(trueLabels, predictedLabels);

            // assert
            Assert.IsTrue(result == expectedResult);
        }

        [Test]
        public void Calculate_Binary_ShouldReturnFalse()
        {
            // arrange
            List<int> trueLabels = new List<int>() { 0, 1, 0, 0, 1 };
            List<int> predictedLabels = new List<int> { 0, 1, 0, 1, 0 };

            double expectedResult = 0.4;

            // act
            double result = _accuracyMetricService.Calculate(trueLabels, predictedLabels);
            
            // assert
            Assert.IsFalse(result == expectedResult);
        }

        [Test]
        public void Calculate_Multiclass_ShouldReturnTrue()
        {
            // arrange
            List<int> trueLabels = new List<int>() { 0, 1, 2, 0, 3 };
            List<int> predictedLabels = new List<int> { 0, 1, 2, 1, 2 };

            double expectedResult = 0.6;

            // act
            double result = _accuracyMetricService.Calculate(trueLabels, predictedLabels);

            // assert
            Assert.IsTrue(result == expectedResult);
        }
    }
}
