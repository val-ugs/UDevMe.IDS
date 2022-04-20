using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class XGBoostTreeNode
    {
        public XGBoostTreeNode LeftNode { get; set; }
        public XGBoostTreeNode RightNode { get; set; }
        public List<Sample> Samples { get; set; }
        public int FeatureIndex { get; set; }
        public List<double> Grad { get; set; }
        public List<double> Hess { get; set; }
        public double SplitValue { get; set; }
        public double Value { get; set; }
        public double Score { get; set; }


        public XGBoostTreeNode()
        {
            Samples = new List<Sample>();
            Grad = new List<double>();
            Hess = new List<double>();
            FeatureIndex = 999;
            SplitValue = 999;
            Value = 0;
            Score = -999;
        }
    }

    public class XGBoostTree
    {
        private XGBoostTreeNode _rootNode { get; set; }
        private double _lambda;
        private int _gamma;

        public XGBoostTree(TrafficData trainTrafficData, List<double> grad, List<double> hess,
                           int maxDepth, int minSize, double lambda,
                           int gamma, double nFeaturesRatio)
        {
            _lambda = lambda;
            _gamma = gamma;

            int nFeatures = (int)(trainTrafficData.Samples[0].Features.Count * nFeaturesRatio);

            _rootNode = new XGBoostTreeNode();
            _rootNode.Samples = trainTrafficData.Samples;
            _rootNode.Grad = grad;
            _rootNode.Hess = hess;
            getSplit(_rootNode, nFeatures);
            Split(_rootNode, maxDepth, minSize, nFeatures, 1);
        }

        private void getSplit(XGBoostTreeNode node, int nFeatures)
        {
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
                    XGBoostTreeNode[] nodes = testSplit(featureIndex, sample.Features[featureIndex], node);
                    double score = Gain(nodes);

                    if (score > node.Score)
                    {
                        node.FeatureIndex = featureIndex;
                        node.SplitValue = sample.Features[featureIndex];
                        node.Score = score;
                        if (nodes[0].Samples.Count > 0 && nodes[1].Samples.Count > 0)
                        {
                            node.LeftNode = nodes[0];
                            node.RightNode = nodes[1];
                        }
                    }
                }
            }
        }

        private XGBoostTreeNode[] testSplit(int featureIndex, double value, XGBoostTreeNode mainNode)
        {
            XGBoostTreeNode[] nodes = new XGBoostTreeNode[2];
            for (int i = 0; i < nodes.Length; i++)
                nodes[i] = new XGBoostTreeNode();
            for (int i = 0; i < mainNode.Samples.Count; i++)
            {
                if (mainNode.Samples[i].Features[featureIndex] <= value)
                {
                    nodes[0].Samples.Add(mainNode.Samples[i]);
                    nodes[0].Grad.Add(mainNode.Grad[i]);
                    nodes[0].Hess.Add(mainNode.Hess[i]);
                }
                else
                {
                    nodes[1].Samples.Add(mainNode.Samples[i]);
                    nodes[1].Grad.Add(mainNode.Grad[i]);
                    nodes[1].Hess.Add(mainNode.Hess[i]);
                }

            }

            return nodes;
        }

        private double Gain(XGBoostTreeNode[] nodes)
        {
            double leftNodeGrad = nodes[0].Grad.Sum();
            double leftNodeHess = nodes[0].Hess.Sum();
            double rightNodeGrad = nodes[1].Grad.Sum();
            double rightNodeHess = nodes[1].Hess.Sum();

            double gain = 0.5 * ( (Math.Pow(leftNodeGrad, 2) / (leftNodeHess + _lambda))
                                  + (Math.Pow(rightNodeGrad, 2) / (rightNodeHess + _lambda))
                                  - (Math.Pow((leftNodeGrad + rightNodeGrad), 2) / (leftNodeHess + rightNodeHess + _lambda))
                                ) - _gamma;

            return gain;
        }

        private void Split(XGBoostTreeNode node, int maxDepth, int minSize, int nFeatures, int currentDepth)
        {
            if (node.LeftNode == null || node.RightNode == null)
            {
                node.Value = ComputeGamma(node.Grad, node.Hess);
                return;
            }
            if (currentDepth >= maxDepth)
            {
                node.LeftNode.Value = ComputeGamma(node.LeftNode.Grad, node.LeftNode.Hess);
                node.RightNode.Value = ComputeGamma(node.RightNode.Grad, node.RightNode.Hess);
                return;
            }
            if (node.LeftNode.Samples.Count <= minSize)
                node.LeftNode.Value = ComputeGamma(node.LeftNode.Grad, node.LeftNode.Hess);
            else
            {
                getSplit(node.LeftNode, nFeatures);
                Split(node.LeftNode, maxDepth, minSize, nFeatures, currentDepth + 1);
            }
            if (node.RightNode.Samples.Count <= minSize)
                node.RightNode.Value = ComputeGamma(node.RightNode.Grad, node.RightNode.Hess);
            else
            {
                getSplit(node.RightNode, nFeatures);
                Split(node.RightNode, maxDepth, minSize, nFeatures, currentDepth + 1);
            }
        }

        private double ComputeGamma(List<double> grad, List<double> hess)
        {
            return (- grad.Sum() / (hess.Sum() + _lambda));
        }

        public double Predict(Sample sample)
        {
            return Predict(_rootNode, sample);
        }

        private double Predict(XGBoostTreeNode node, Sample sample)
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

            return node.Value;
        }
    }

    public class XGBoost
    {
        private int _rounds;
        private int _maxDepth;
        private int _minSize;
        private double _learningRate;
        private double _lambda;
        private int _gamma;
        private double _nFeaturesRatio;

        private int _distinctLabelCount;
        private List<XGBoostTree> _trees;
        
        public XGBoost(int rounds, int maxDepth,
                       int minSize, double learningRate, double lambda,
                       int gamma, double nFeaturesRatio)
        {
            _rounds = rounds;
            _maxDepth = maxDepth;
            _minSize = minSize;
            _learningRate = learningRate;
            _lambda = lambda;
            _gamma = gamma;
            _nFeaturesRatio = nFeaturesRatio;

            _trees = new List<XGBoostTree>();

            
        }

        public void Train(TrafficData trainTrafficData)
        {
            List<int> labels = trainTrafficData.Samples.Select(s => s.Label).ToList(); // All labels
            _distinctLabelCount = labels.Distinct().Count();

            List<double> basePreds;
            if (_distinctLabelCount > 2)
            {
                basePreds = Enumerable.Repeat(0.0, trainTrafficData.Samples.Count * _distinctLabelCount).ToList(); // Fill 0

                for (int i = 0; i < _rounds; i++)
                {
                    List<double> grad, hess;
                    MulticlassGradHess(basePreds, labels, out grad, out hess);
                    for (int j = 0; j < _distinctLabelCount; j++)
                    {
                        // Take grad and hess for current label
                        List<double> currentGrad = grad.Where((x, index) => index % _distinctLabelCount == j).ToList();
                        List<double> currentHess = hess.Where((x, index) => index % _distinctLabelCount == j).ToList();
                        XGBoostTree tree = new XGBoostTree(trainTrafficData, currentGrad, currentHess, _maxDepth,
                                                           _minSize, _lambda, _gamma, _nFeaturesRatio);
                        _trees.Add(tree);
                    }

                    for (int j = 0; j < trainTrafficData.Samples.Count; j++)
                        for (int k = 0; k < _distinctLabelCount; k++)
                            basePreds[j * _distinctLabelCount + k] += _learningRate * _trees[i * _distinctLabelCount + k].Predict(trainTrafficData.Samples[j]);
                }
            }
            else
            {
                basePreds = Enumerable.Repeat(1.0, trainTrafficData.Samples.Count).ToList(); // Fill 1

                for (int i = 0; i < _rounds; i++)
                {
                    List<double> grad, hess;
                    GradHess(basePreds, labels, out grad, out hess);
                    XGBoostTree tree = new XGBoostTree(trainTrafficData, grad, hess, _maxDepth,
                                                       _minSize, _lambda, _gamma, _nFeaturesRatio);

                    for (int j = 0; j < trainTrafficData.Samples.Count; j++)
                        basePreds[j] += _learningRate * tree.Predict(trainTrafficData.Samples[j]);

                    _trees.Add(tree);
                }
            }
        }

        private void GradHess(List<double> preds, List<int> labels, out List<double> grad, out List<double> hess)
        {
            grad = new List<double>(preds.Count);
            hess = new List<double>(preds.Count);
            double p;

            for (int i = 0; i < preds.Count; i++)
            {
                p = Sigmoid(preds[i]);
                grad.Add(p - labels[i]);
                hess.Add(p * (1 - p));
            }
        }

        private void MulticlassGradHess(List<double> preds, List<int> labels, out List<double> grad, out List<double> hess)
        {
            grad = new List<double>(preds.Count);
            hess = new List<double>(preds.Count);

            for (int i = 0; i < labels.Count; i++)
            {
                double[] p = Softmax(preds.Skip(i * _distinctLabelCount).Take(_distinctLabelCount).ToArray());

                for (int j = 0; j < _distinctLabelCount; j++)
                {
                    grad.Add(j == labels[i] ? (p[j] - 1) : p[j]);
                    hess.Add(2 * p[j] * (1 - p[j]));
                }
            }

        }

        public List<int> Predict(TrafficData trafficData)
        {
            List<double> preds = Enumerable.Repeat(0.0, trafficData.Samples.Count).ToList(); // fill 0

            if (_distinctLabelCount > 2)
            {
                List<double> predictedProbas = Enumerable.Repeat(0.0, trafficData.Samples.Count * _distinctLabelCount).ToList(); // fill 0

                for (int i = 0; i < _trees.Count; i++)
                {
                    for (int j = 0; j < trafficData.Samples.Count; j++)
                    {
                        predictedProbas[i % _distinctLabelCount + j * _distinctLabelCount] += _learningRate * _trees[i].Predict(trafficData.Samples[j]);
                    }
                }

                for (int i = 0; i < preds.Count; i++)
                {
                    List<double> currentPredictedProbas = predictedProbas.Skip(i * _distinctLabelCount)
                                                                         .Take(_distinctLabelCount)
                                                                         .ToList();

                    currentPredictedProbas = Softmax(currentPredictedProbas.ToArray()).ToList();

                    preds[i] = currentPredictedProbas.IndexOf(currentPredictedProbas.Max());
                }
            }
            else
            {
                foreach (XGBoostTree tree in _trees)
                {
                    for (int i = 0; i < preds.Count; i++)
                    {
                        preds[i] += _learningRate * tree.Predict(trafficData.Samples[i]);
                    }
                }

                List<double> predictedProbas = new List<double>();

                for (int i = 0; i < preds.Count; i++)
                {
                    predictedProbas.Add(Sigmoid(1 + preds[i]));
                }

                for (int i = 0; i < preds.Count; i++)
                {
                    if (predictedProbas[i] > predictedProbas.Average())
                        preds[i] = 1;
                    else
                        preds[i] = 0;
                }
            }

            return preds.Select(p => (int)p).ToList();
        }

        private double Sigmoid(double x)
        {
            return 1 / (1 + Math.Exp(-x));
        }

        private double[] Softmax(double[] x)
        {
            double[] p = new double[x.Length];
            double sum = 0;
            for (int i = 0; i < x.Length; i++)
            {
                p[i] = Math.Exp(x[i]);
                sum += p[i];
            }
            for (int i = 0; i < p.Length; i++)
            {
                p[i] /= sum;
            }

            return p;
        }
    }

    public class XGBoostService : IClassifierService
    {
        private XGBoost _xgBoost;

        public XGBoostService(int rounds, int maxDepth, int minSize, double learningRate,
                              double lambda, int gamma, double nFeaturesRatio)
        {
            _xgBoost = new XGBoost(rounds, maxDepth, minSize, learningRate, lambda, gamma, nFeaturesRatio);
        }

        public void Train(TrafficData trainTrafficData)
        {
            _xgBoost.Train(trainTrafficData);
        }

        public List<int> Predict(TrafficData testTrafficData)
        {
            return _xgBoost.Predict(testTrafficData);
        }
    }
}
