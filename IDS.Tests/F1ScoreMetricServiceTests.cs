using IDS.BusinessLogic.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Tests
{
    public class F1ScoreMetricServiceTests
    {
        private F1ScoreMetricService _f1ScoreMetricService;

        [SetUp]
        public void Setup()
        {
            _f1ScoreMetricService = new F1ScoreMetricService();
        }

        [Test]
        public void Calculate_Binary_ShouldReturnTrue()
        {
            // arrange
            List<int> trueLabels = new List<int>() { 0, 1, 0, 1, 0 };
            List<int> predictedLabels = new List<int> { 0, 1, 0, 0, 1 };

            double expectedResult = 0.58;

            // act
            double result = _f1ScoreMetricService.Calculate(trueLabels, predictedLabels);
            result = Math.Round(result, 2);

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
            double result = _f1ScoreMetricService.Calculate(trueLabels, predictedLabels);

            // assert
            Assert.IsFalse(result == expectedResult);
        }

        [Test]
        public void Calculate_Multiclass_ShouldReturnTrue()
        {
            // arrange
            List<int> trueLabels = new List<int>() { 0, 1, 2, 0, 3 };
            List<int> predictedLabels = new List<int> { 0, 1, 2, 1, 2 };

            double expectedResult = 0.5;

            // act
            double result = _f1ScoreMetricService.Calculate(trueLabels, predictedLabels);
            result = Math.Round(result, 2);

            // assert
            Assert.IsTrue(result == expectedResult);
        }
    }
}