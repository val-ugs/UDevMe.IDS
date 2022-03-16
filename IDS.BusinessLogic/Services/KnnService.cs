using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class KnnService : IKnnService
    {
        public List<int> Predict(TrafficData trainTrafficData, TrafficData testTrafficData, int numNeighbors)
        {
            List<int> predictions = new List<int>();
            foreach (Sample testSample in testTrafficData.Samples)
            {
                List<Sample> neighbors = GetNeighbors(trainTrafficData, testSample, numNeighbors);
                List<double> labels = new List<double>();
                foreach (Sample neighbor in neighbors)
                    labels.Add(neighbor.Label); // Get label from neigbor
                predictions.Add((int)labels.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key);
            }

            return predictions;
        }

        private List<Sample> GetNeighbors(TrafficData trainTrafficData, Sample testSample, int numNeighbors)
        {
            Dictionary<int, double> distances = new Dictionary<int, double>();
            List<Sample> neighbors = new List<Sample>();
            int index = 0;
            foreach (Sample trainSample in trainTrafficData.Samples)
            {
                double distance = EuclideanDistance(testSample, trainSample);
                distances[index] = distance;
                index++;
            }

            var sortedDistances = from entry in distances orderby entry.Value ascending select entry;
            
            for (int i = 0; i < numNeighbors; i++)
                neighbors.Add(trainTrafficData.Samples[sortedDistances.ElementAt(i).Key]);

            return neighbors;
        }

        private double EuclideanDistance(Sample testSample, Sample trainSample)
        {
            double distance = 0;

            for (int i = 0; i < testSample.Features.Count; i++)
            {
                distance += Math.Pow((testSample.Features[i] - trainSample.Features[i]), 2);
            }

            return Math.Sqrt(distance);
        }
    }
}
