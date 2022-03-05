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
            foreach (List<double> testFeaturesRow in testTrafficData.Features)
            {
                List<List<double>> neighbors = GetNeighbors(trainTrafficData, testFeaturesRow, numNeighbors);
                List<double> labels = new List<double>();
                foreach (List<double> neighbor in neighbors)
                    labels.Add(neighbor[neighbor.Count - 1]); // Get label from neigbor
                predictions.Add((int)labels.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key);
            }

            return predictions;
        }

        private List<List<double>> GetNeighbors(TrafficData trainTrafficData, List<double> testFeaturesRow, int numNeighbors)
        {
            Dictionary<int, double> distances = new Dictionary<int, double>();
            List<List<double>> neighbors = new List<List<double>>();
            int index = 0;
            foreach (List<double> trainFeaturesRow in trainTrafficData.Features)
            {
                double distance = EuclideanDistance(testFeaturesRow, trainFeaturesRow);
                distances[index] = distance;
                index++;
            }

            var sortedDistances = from entry in distances orderby entry.Value ascending select entry;
            
            for (int i = 0; i < numNeighbors; i++)
                neighbors.Add(new List<double>(trainTrafficData.Features[sortedDistances.ElementAt(i).Key]));

            return neighbors;
        }

        private double EuclideanDistance(List<double> row1, List<double> row2)
        {
            double distance = 0;

            for (int i = 0; i < row1.Count - 1; i++)
            {
                distance += Math.Pow((row1[i] - row2[i]), 2);
            }

            return Math.Sqrt(distance);
        }
    }
}
