using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class TreeNode
    {
        public TreeNode LeftNode { get; set; }
        public TreeNode RightNode { get; set; }
        public List<Sample> Samples { get; set; }
        public int FeatureIndex { get; set; }
        public double SplitValue { get; set; }
        public double Score { get; set; }
        public int Label { get; set; }


        public TreeNode()
        {
            Samples = new List<Sample>();
            FeatureIndex = 999;
            SplitValue = 999;
            Score = 999;
            Label = -1;
        }
    }

    public class Tree
    {
        private TreeNode _rootNode { get; set; }
        public Tree(TrafficData trainTrafficData, int maxDepth, int minSize, int nFeatures)
        {
            _rootNode = new TreeNode();
            _rootNode.Samples = trainTrafficData.Samples;
            getSplit(_rootNode, nFeatures);
            Split(_rootNode, maxDepth, minSize, nFeatures, 1);
        }

        private void getSplit(TreeNode node, int nFeatures)
        {
            List<int> labels = new List<int>();
            foreach (var sample in node.Samples)
            {
                labels.Add(sample.Label);
            }

            Random rand = new Random();
            // Sort features, shuffle and take n
            List<int> featureIndices = Enumerable.Range(0, node.Samples[0].Features.Count)
                                                 .OrderBy(x => rand.Next())
                                                 .Take(nFeatures)
                                                 .ToList();

            foreach (int featureIndex in featureIndices)
            {
                foreach (Sample sample in node.Samples)
                {
                    TreeNode[] nodes = testSplit(featureIndex, sample.Features[featureIndex], node.Samples);
                    double giniIndex = CalculateGiniIndex(nodes, labels);

                    if (giniIndex < node.Score)
                    {
                        node.FeatureIndex = featureIndex;
                        node.SplitValue = sample.Features[featureIndex];
                        node.Score = giniIndex;
                        if (nodes[0].Samples.Count > 0 && nodes[1].Samples.Count > 0)
                        {
                            node.LeftNode = nodes[0];
                            node.RightNode = nodes[1];
                        }
                    }
                }
            }
        }

        private TreeNode[] testSplit(int featureIndex, double value, List<Sample> samples)
        {
            TreeNode[] nodes = new TreeNode[2];
            for (int i = 0; i < nodes.Length; i++)
                nodes[i] = new TreeNode();
            foreach (Sample sample in samples)
            {
                if (sample.Features[featureIndex] <= value)
                    nodes[0].Samples.Add(sample);
                else
                    nodes[1].Samples.Add(sample);
            }

            return nodes;
        }

        private double CalculateGiniIndex(TreeNode[] nodes, List<int> labels)
        {
            double nInstances = nodes.Select(g => g.Samples.Count).Sum();
            double gini = 0;
            foreach (TreeNode node in nodes)
            {
                double size = node.Samples.Count;
                if (size == 0)
                    continue;
                double score = 0;
                foreach (int label in labels)
                {
                    double counter = 0;
                    foreach (Sample sample in node.Samples)
                    {
                        if (sample.Label == label)
                            counter++;
                    }
                    double p = counter / size;
                    score += p * p;
                }
                gini += (1 - score) * (size / nInstances);
            }
            return gini;
        }

        private void Split(TreeNode node, int maxDepth, int minSize, int nFeatures, int currentDepth)
        {
            if (node.LeftNode == null || node.RightNode == null)
            {
                node.Label = ToTerminal(node);
                return;
            }
            if (currentDepth >= maxDepth)
            {
                node.LeftNode.Label = ToTerminal(node.LeftNode);
                node.RightNode.Label = ToTerminal(node.RightNode);
                return;
            }
            if (node.LeftNode.Samples.Count <= minSize)
                node.LeftNode.Label = ToTerminal(node.LeftNode);
            else
            {
                getSplit(node.LeftNode, nFeatures);
                Split(node.LeftNode, maxDepth, minSize, nFeatures, currentDepth + 1);
            }
            if (node.RightNode.Samples.Count <= minSize)
                node.RightNode.Label = ToTerminal(node.RightNode);
            else
            {
                getSplit(node.RightNode, nFeatures);
                Split(node.RightNode, maxDepth, minSize, nFeatures, currentDepth + 1);
            }
        }

        private int ToTerminal(TreeNode node)
        {
            // Create list from sample labels
            List<int> labels = node.Samples.Select(s => s.Label).ToList();
            // Take max repeated label
            return labels.GroupBy(l => l).OrderByDescending(l => l.Count()).First().Key;
        }

        public int Predict(Sample sample)
        {
            return Predict(_rootNode, sample);
        }

        private int Predict(TreeNode node, Sample sample)
        {
            if (node.FeatureIndex < sample.Features.Count)
                if (sample.Features[node.FeatureIndex] <= node.SplitValue)
                {
                    if (node.LeftNode != null)
                        return Predict(node.LeftNode, sample);
                }
                else
                {
                    if (node.RightNode != null)
                        return Predict(node.RightNode, sample);
                }
                    
            return node.Label;
        }
    }

    public class RandomForestService : IRandomForestService
    {
        public List<int> Predict(TrafficData trainTrafficData, TrafficData testTrafficData, int numTrees,
                                 int maxDepth, int minSize, double partOfTrafficDataRatio)
        {
            int nFeatures = (int)Math.Sqrt(trainTrafficData.Samples[0].Features.Count);

            Tree[] trees = new Tree[numTrees];
            List<int> predictions = new List<int>();

            for (int i = 0; i < numTrees; i++)
            {
                TrafficData partOfTrainTrafficData = GetPartOfTrainTrafficData(trainTrafficData, partOfTrafficDataRatio);
                trees[i] = new Tree(partOfTrainTrafficData, maxDepth, minSize, nFeatures);
            }

            foreach (Sample testSample in testTrafficData.Samples)
            {
                int predictedFromTree = Predict(trees, testSample);
                predictions.Add(predictedFromTree);
            }

            return predictions;
        }

        private TrafficData GetPartOfTrainTrafficData(TrafficData trainTrafficData, double partOfTrafficDataRatio)
        {
            TrafficData partOfTrafficData = new TrafficData();
            int n = (int)Math.Round(trainTrafficData.Samples.Count * partOfTrafficDataRatio);
            Random rand = new Random();

            while (partOfTrafficData.Samples.Count < n)
            {
                int index = rand.Next(trainTrafficData.Samples.Count);
                partOfTrafficData.Samples.Add(trainTrafficData.Samples[index]);
            }

            return partOfTrafficData;
        }

        private int Predict(Tree[] trees, Sample testSample)
        {
            List<int> predictions = new List<int>();
            foreach (Tree tree in trees)
            {
                predictions.Add(tree.Predict(testSample));
            }

            return predictions.GroupBy(p => p).OrderByDescending(p => p.Count()).First().Key;
        }
    }
}
