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
        public class Neighbor
        {
            public Neighbor(Sample sample)
            {
                Sample = sample;
            }

            public Sample Sample { get; set; }
            public double Distance { get; set; }
            public double Weight { get; set; }
            
        }

        public class Knn
        {
            private TrafficData _trainTrafficData;
            private int _numberOfNeighbors;

            public Knn(TrafficData trainTrafficData, int numberOfNeighbors)
            {
                _trainTrafficData = trainTrafficData;
                _numberOfNeighbors = numberOfNeighbors;
            }

            public List<int> Predict(TrafficData testTrafficData)
            {
                List<int> predictions = new List<int>();
                foreach (Sample testSample in testTrafficData.Samples)
                {
                    List<Neighbor> neighbors = GetNeighbors(testSample);
                    MakeWeights(neighbors);

                    predictions.Add(CalculateLabel(neighbors));
                }

                return predictions;
            }

            private List<Neighbor> GetNeighbors(Sample testSample)
            {
                List<Neighbor> neighbors = new List<Neighbor>();

                foreach (Sample trainSample in _trainTrafficData.Samples)
                {
                    Neighbor neighbor = new Neighbor(trainSample);
                    neighbor.Distance = EuclideanDistance(testSample, trainSample);
                    neighbors.Add(neighbor);
                }

                // Sort distances in ascending order and take n neighbors
                List<Neighbor> nearestNeighbors = neighbors.OrderBy(n => n.Distance).Take(_numberOfNeighbors).ToList();

                return nearestNeighbors;
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

            private void MakeWeights(List<Neighbor> neighbors, double constant = 0.1)
            {
                foreach (Neighbor neighbor in neighbors)
                {
                    neighbor.Weight = 1 / (neighbor.Distance + constant);
                }
            }

            private int CalculateLabel(List<Neighbor> neighbors)
            {
                double labelWeight = 0;
                double totalWeight = 0;
                foreach (Neighbor neighbor in neighbors)
                {
                    labelWeight += neighbor.Weight * neighbor.Sample.Label;
                    totalWeight += neighbor.Weight;
                }

                return (int)Math.Round((labelWeight / totalWeight), MidpointRounding.AwayFromZero);
            }
        }

        public List<int> Predict(TrafficData trainTrafficData, TrafficData testTrafficData, int numberOfNeighbors)
        {
            Knn knn = new Knn(trainTrafficData, numberOfNeighbors);

            return knn.Predict(testTrafficData);
        }
    }
}
