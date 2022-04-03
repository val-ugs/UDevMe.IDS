using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
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
        private List<int> _distinctLabels;

        public Knn(int numberOfNeighbors)
        {
            _numberOfNeighbors = numberOfNeighbors;
        }

        public void Train(TrafficData trainTrafficData)
        {
            _trainTrafficData = trainTrafficData;

            _distinctLabels = trainTrafficData.Samples.Select(s => s.Label).Distinct().ToList();
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
            List<double> labelWeights = new List<double>(new double[_distinctLabels.Count]);
            foreach (Neighbor neighbor in neighbors)
            {
                for (int i = 0; i < _distinctLabels.Count; i++)
                {
                    if (neighbor.Sample.Label == _distinctLabels[i])
                        labelWeights[i] += neighbor.Weight;
                }
            }

            // Get max label index based on weights
            int labelIndex = labelWeights.IndexOf(labelWeights.Max());

            return _distinctLabels[labelIndex];
        }
    }

    public class KnnService : IClassifierService
    {
        private Knn _knn;

        public KnnService(int numberOfNeighbors)
        {
            _knn = new Knn(numberOfNeighbors);
        }

        public void Train(TrafficData trainTrafficData)
        {
            _knn.Train(trainTrafficData);
        }

        public List<int> Predict(TrafficData testTrafficData)
        {
            return _knn.Predict(testTrafficData);
        }
    }
}
