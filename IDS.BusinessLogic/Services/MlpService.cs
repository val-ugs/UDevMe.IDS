using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public abstract class BaseStochasticOptimizer
    {
        private List<double[][]> _weights;
        private List<double[]> _bias;
        protected double learningRateInit;
        protected double learningRate;

        protected BaseStochasticOptimizer(double learningRate, ref List<double[][]> weights, ref List<double[]> bias)
        {
            _weights = weights;
            _bias = bias;
            learningRateInit = learningRate;
            this.learningRate = learningRate;
        }

        public abstract void GetUpdates(ref List<double[][]> deltaWeights, ref List<double[]> deltaBias);

        public void UpdateParams(List<double[][]> weightsGrads, List<double[]> biasGrads)
        {
            List<double[][]> deltaWeights = new List<double[][]>(weightsGrads.Count);
            for (int n = 1; n < weightsGrads.Count; n++)
            {
                double[][] dW = new double[weightsGrads[n - 1].Length][];
                for (int i = 0; i < weightsGrads[n - 1].Length; i++)
                {
                    dW[i] = new double[weightsGrads[n - 1][i].Length];
                }
                deltaWeights.Add(dW);
            }
            for (int n = 1; n < weightsGrads.Count; n++)
            {
                for (int i = 0; i < weightsGrads[n - 1].Length; i++)
                {
                    for (int j = 0; j < weightsGrads[n - 1][i].Length; j++)
                    {
                        deltaWeights[n - 1][i][j] = weightsGrads[n - 1][i][j];
                    }
                }
            }

            List<double[]> deltaBias = new List<double[]>(biasGrads.Count);
            for (int n = 1; n < biasGrads.Count; n++)
            {
                deltaBias.Add(new double[biasGrads[n - 1].Length]);
            }
            for (int n = 1; n < biasGrads.Count; n++)
            {
                for (int i = 0; i < biasGrads[n - 1].Length; i++)
                {
                    deltaBias[n - 1][i] = biasGrads[n - 1][i];
                }
            }

            GetUpdates(ref deltaWeights, ref deltaBias);
            for (int n = 0; n < deltaWeights.Count; n++)
            {
                for (int i = 0; i < deltaWeights[n].Length; i++)
                {
                    for (int j = 0; j < deltaWeights[n][i].Length; j++)
                    {
                        _weights[n][i][j] += deltaWeights[n][i][j];
                    }
                }
            }
            for (int n = 0; n < deltaBias.Count; n++)
            {
                for (int i = 0; i < deltaBias[n].Length; i++)
                {
                    _bias[n][i] += deltaBias[n][i];
                }
            }
        }
    }

    public class AdamOptimizer : BaseStochasticOptimizer
    {
        private double _beta_1;
        private double _beta_2;
        private double _epsilon;
        private double _t;
        private List<double[][]> _mWeights;
        private List<double[][]> _vWeights;
        private List<double[]> _mBias;
        private List<double[]> _vBias;

        public AdamOptimizer(double learningRate, double beta_1, double beta_2, double epsilon,
                             ref List<double[][]> weights, ref List<double[]> bias)
                             : base(learningRate, ref weights, ref bias)
        {
            _beta_1 = beta_1;
            _beta_2 = beta_2;
            _epsilon = epsilon;
            _t = 0;

            _mWeights = new List<double[][]>(weights.Count);
            _vWeights = new List<double[][]>(weights.Count);
            for (int n = 1; n < weights.Count; n++)
            {
                double[][] mW = new double[weights[n - 1].Length][];
                double[][] vW = new double[weights[n - 1].Length][];
                for (int i = 0; i < weights[n - 1].Length; i++)
                {
                    mW[i] = new double[weights[n - 1][i].Length];
                    vW[i] = new double[weights[n - 1][i].Length];
                }
                _mWeights.Add(mW);
                _vWeights.Add(vW);
            }
            for (int n = 0; n < _mWeights.Count; n++)
            {
                for (int i = 0; i < _mWeights[n].Length; i++)
                {
                    for (int j = 0; j < _mWeights[n][i].Length; j++)
                    {
                        _mWeights[n][i][j] = 0;
                        _vWeights[n][i][j] = 0;
                    }
                }
            }

            _mBias = new List<double[]>(bias.Count);
            _vBias = new List<double[]>(bias.Count);
            for (int n = 1; n < bias.Count; n++)
            {
                _mBias.Add(new double[bias[n - 1].Length]);
                _vBias.Add(new double[bias[n - 1].Length]);
            }

            for (int n = 1; n < bias.Count; n++)
            {
                for (int i = 0; i < bias[n].Length; i++)
                {
                    _mBias[n - 1][i] = 0;
                    _mBias[n - 1][i] = 0;
                }
            }
        }

        public override void GetUpdates(ref List<double[][]> deltaWeights, ref List<double[]> deltaBias)
        {
            _t++;
            for (int n = 0; n < _mWeights.Count; n++)
            {
                for (int i = 0; i < _mWeights[n].Length; i++)
                {
                    for (int j = 0; j < _mWeights[n][i].Length; j++)
                    {
                        _mWeights[n][i][j] = _beta_1 * _mWeights[n][i][j] + (1 - _beta_1) * deltaWeights[n][i][j];
                        _vWeights[n][i][j] = _beta_2 * _vWeights[n][i][j] + (1 - _beta_2) * Math.Pow(deltaWeights[n][i][j], 2);
                    }
                }
            }
            for (int n = 0; n < _mBias.Count; n++)
            {
                for (int i = 0; i < _mBias[n].Length; i++)
                {
                    _mBias[n][i] = _beta_1 * _mBias[n][i] + (1 - _beta_1) * deltaBias[n][i];
                    _vBias[n][i] = _beta_2 * _vBias[n][i] + (1 - _beta_2) * Math.Pow(deltaBias[n][i], 2);
                }
            }

            learningRate = (learningRateInit * Math.Sqrt(1 - Math.Pow(_beta_2, _t)) / (1 - Math.Pow(_beta_1, _t)));

            for (int n = 0; n < _mWeights.Count; n++)
            {
                for (int i = 0; i < _mWeights[n].Length; i++)
                {
                    for (int j = 0; j < _mWeights[n][i].Length; j++)
                    {
                        deltaWeights[n][i][j] = - learningRate * _mWeights[n][i][j] / (Math.Sqrt(_vWeights[n][i][j]) + _epsilon);
                    }
                }
            }
            for (int n = 0; n < _mBias.Count; n++)
            {
                for (int i = 0; i < _mBias[n].Length; i++)
                {
                    deltaBias[n][i] = - learningRate * _mBias[n][i] / (Math.Sqrt(_vBias[n][i]) + _epsilon);
                }
            }
        }
    }


    public class Mlp
    {
        enum LabelType
        {
            Binary,
            Multiclass
        }

        private List<int> _hiddenLayersWithNeurons;
        private double _alpha;
        private int _batchSize;
        private double _learningRate;
        private int _maxIterations;
        private double _tol;
        private double _beta_1;
        private double _beta_2;
        private double _epsilon;

        AdamOptimizer _optimizer;

        private int _distinctLabelCount;
        private List<int> _layersOfNeurons;

        private List<double[]> _bias;
        private List<double[][]> _weights;

        private LabelType _labelType;

        public Mlp(List<int> hiddenLayersWithNeurons, double alpha,  int batchSize, double learningRate,
                   int maxIterations, double tol, double beta_1, double beta_2, double epsilon)
        {
            _hiddenLayersWithNeurons = hiddenLayersWithNeurons;
            _alpha = alpha;
            _batchSize = batchSize;
            _learningRate = learningRate;
            _maxIterations = maxIterations;
            _tol = tol;
            _beta_1 = beta_1;
            _beta_2 = beta_2;
            _epsilon = epsilon;
        }

        public void Train(TrafficData trainTrafficData)
        {
            _distinctLabelCount = trainTrafficData.Samples.Select(s => s.Label).Distinct().Count();

            _layersOfNeurons = new List<int>();
            _layersOfNeurons.Add(trainTrafficData.Samples[0].Features.Count); // Input layer
            _layersOfNeurons.AddRange(_hiddenLayersWithNeurons); // Hidden layers
            _layersOfNeurons.Add(_distinctLabelCount); // output layer

            // Adam Optimizer parameter
            _batchSize = Math.Min(_batchSize, trainTrafficData.Samples.Count);

            // Initialize arrays
            InitializeWeights();

            // Adam Optimizer
            _optimizer = new AdamOptimizer(_learningRate, _beta_1, _beta_2, _epsilon, ref _weights, ref _bias);

            Train(trainTrafficData.Samples);
        }

        private void InitializeWeights()
        {
            _bias = new List<double[]>(_layersOfNeurons.Count - 1);
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                _bias.Add(new double[_layersOfNeurons[n]]);
            }

            _weights = new List<double[][]>(_layersOfNeurons.Count - 1);
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                double[][] w = new double[_layersOfNeurons[n - 1]][];
                for (int i = 0; i < _layersOfNeurons[n - 1]; i++)
                {
                    w[i] = new double[_layersOfNeurons[n]];
                }
                _weights.Add(w);
            }

            Random rand = new Random();
            double factor = 6;
            double bound, max, min;

            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                bound = Math.Sqrt(factor / (_layersOfNeurons[n - 1] + _layersOfNeurons[n]));
                max = bound;
                min = -bound;

                for (int j = 0; j < _layersOfNeurons[n]; j++)
                {
                    _bias[n - 1][j] = rand.NextDouble() * (max - min) + min;
                }

                for (int i = 0; i < _layersOfNeurons[n - 1]; i++)
                {
                    for (int j = 0; j < _layersOfNeurons[n]; j++)
                    {
                        _weights[n - 1][i][j] = rand.NextDouble() * (max - min) + min;
                    }
                }
            }
        }

        private void Train(List<Sample> samples)
        {
            DefineLabelType(samples);

            // Graduated bias and weights
            List<double[][]> weightsGrads = new List<double[][]>(_layersOfNeurons.Count - 1);
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                double[][] wG = new double[_layersOfNeurons[n - 1]][];
                for (int i = 0; i < _layersOfNeurons[n - 1]; i++)
                {
                    wG[i] = new double[_layersOfNeurons[n]];
                }
                weightsGrads.Add(wG);
            }

            List<double[]> biasGrads = new List<double[]>(_layersOfNeurons.Count - 1);
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                biasGrads.Add(new double[_layersOfNeurons[n]]);
            }

            double loss = 999;
            int it = 0;

            while (it < _maxIterations && loss > _tol)
            {
                double accumulatedLoss = 0;
                foreach (List<Sample> batchSlice in GenerateBatches(samples))
                {
                    int batchSize = batchSlice.Count;
                    List<double[][]> activations = InitializeActivations(batchSize);

                    for (int i = 0; i < batchSlice.Count; i++)
                    {
                        for (int j = 0; j < batchSlice[0].Features.Count; j++)
                        {
                            activations[0][i][j] = batchSlice[i].Features[j];
                        }
                    }
                    
                    activations = ForwardPass(activations, batchSize);
                    double[][] trueLabels = GetTrueLabels(batchSlice);
                    double batchLoss = GetLoss(activations, trueLabels, batchSize);
                    BackProp(activations, trueLabels, batchSize, ref weightsGrads, ref biasGrads);
                    _optimizer.UpdateParams(weightsGrads, biasGrads);

                    accumulatedLoss += batchLoss * batchSlice.Count;
                }

                loss = accumulatedLoss / samples.Count;
                it++;
            }
        }

        private double[][] GetTrueLabels(List<Sample> samples)
        {
            double[][] trueLabels = new double[samples.Count][];
            for (int i = 0; i < samples.Count; i++)
            {
                trueLabels[i] = new double[_distinctLabelCount];
            }
            for (int i = 0; i < samples.Count; i++)
            {
                for (int j = 0; j < _distinctLabelCount; j++)
                {
                    if (samples[i].Label == j)
                    {
                        trueLabels[i][j] = 1;
                    }
                    else
                    {
                        trueLabels[i][j] = 0;
                    }
                }
            }

            return trueLabels;
        }

        private void DefineLabelType(List<Sample> samples)
        {
            if (_distinctLabelCount == 2) // binary
                _labelType = LabelType.Binary;
            else if (_distinctLabelCount > 2)
                _labelType = LabelType.Multiclass;
            else
                throw new Exception("It's not binary or multiclass. Check the number of unique labels.");
        }

        private IEnumerable<List<Sample>> GenerateBatches(List<Sample> samples)
        {
            int sampleCountLeft = samples.Count;
            int i = 0;

            while (sampleCountLeft != 0)
            {
                if (sampleCountLeft > _batchSize)
                {
                    yield return samples.Skip(i * _batchSize).Take(_batchSize).ToList();
                    sampleCountLeft -= _batchSize;
                    i++;
                }
                else
                {
                    yield return samples.Skip(i * _batchSize).Take(sampleCountLeft).ToList();
                    sampleCountLeft -= sampleCountLeft;
                }
            }
        }

        private List<double[][]> ForwardPass(List<double[][]> activations, int sampleSize)
        {
            // Iterate over the hidden layers
            for (int n = 1; n < _layersOfNeurons.Count; n++) //Layers
            {
                for (int i = 0; i < sampleSize; i++)
                {
                    for (int j = 0; j < _layersOfNeurons[n]; j++) // Neurons
                    {
                        double sum = 0;
                        for (int k = 0; k < _layersOfNeurons[n - 1]; k++)
                        {
                            sum += activations[n - 1][i][k] * _weights[n - 1][k][j];
                        }
                        activations[n][i][j] = sum + _bias[n - 1][j];
                        if (n != _layersOfNeurons.Count - 1)
                        {
                            activations[n][i][j] = Relu(activations[n][i][j]);
                        }
                    }
                }
            }

            // For the last layer
            for (int i = 0; i < sampleSize; i++)
            {
                activations[_layersOfNeurons.Count - 1][i] = OutputActivationFunction(activations[_layersOfNeurons.Count - 1][i]);
            }

            return activations;
        }

        private double GetLoss(List<double[][]> activations, double[][] trueLabels, int sampleSize)
        {
            double loss = 0;

            if (_labelType == LabelType.Binary)
                    loss = BinaryLogLoss(trueLabels, activations[_layersOfNeurons.Count - 1]);
            if (_labelType == LabelType.Multiclass)
                    loss = LogLoss(trueLabels, activations[_layersOfNeurons.Count - 1]);

            // Add L2 regularization term to loss
            double sumSquareWeights = 0;
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                for (int i = 0; i < _layersOfNeurons[n - 1]; i++)
                {
                    for (int j = 0; j < _layersOfNeurons[n]; j++)
                    {
                        sumSquareWeights += _weights[n - 1][i][j] * _weights[n - 1][i][j];
                    }
                }
            }
            loss += (0.5 * _alpha) * sumSquareWeights / sampleSize;
            

            return loss;
        }

        private double BinaryLogLoss(double[][] trueLabels, double[][] predLabels)
        {
            double sum = 0;
            for (int i = 0; i < predLabels.Length; i++)
            {
                for (int j = 0; j < predLabels[i].Length; j++)
                {
                    sum += XLogY(trueLabels[i][j], predLabels[i][j]) + XLogY(1 - trueLabels[i][j], 1 - predLabels[i][j]);
                }
            }
            return - sum / predLabels.Length;
        }

        private double LogLoss(double[][] trueLabels, double[][] predLabels)
        {
            double sum = 0;
            for (int i = 0; i < predLabels.Length; i++)
            {
                for (int j = 0; j < predLabels[i].Length; j++)
                {
                    sum += XLogY(trueLabels[i][j], predLabels[i][j]);
                }
            }
            return - sum / predLabels.Length;
        }

        private double XLogY(double x, double y)
        {
            return x * Math.Log(y);
        }

        private void BackProp(List<double[][]> activations, double[][] trueLabels, int sampleSize,
                              ref List<double[][]> weightsGrads, ref List<double[]> biasGrads)
        {

            double[][][] errors = new double[_layersOfNeurons.Count - 1][][];
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                errors[n - 1] = new double[sampleSize][];
                for (int i = 0; i < sampleSize; i++)
                {
                    errors[n - 1][i] = new double[_layersOfNeurons[n]];
                }
            }
            for (int n = 1; n < _layersOfNeurons.Count; n++)
            {
                for (int i = 0; i < sampleSize; i++)
                {
                    for (int j = 0; j < _layersOfNeurons[n]; j++)
                    {
                        errors[n - 1][i][j] = 0;
                    }
                }
            }

            int lastLayer = _layersOfNeurons.Count - 1;

            for (int i = 0; i < sampleSize; i++)
            {
                for (int j = 0; j < _layersOfNeurons[lastLayer]; j++)
                {
                    errors[lastLayer - 1][i][j] = activations[lastLayer][i][j] - trueLabels[i][j];
                }
            }

            // Compute gradient for the last layer
            ComputeLossGrad(lastLayer, activations, sampleSize, errors, ref weightsGrads, ref biasGrads);

            // Iterate over the hidden layers 
            for (int n = lastLayer - 1; n > 0; n--)
            {
                for (int i = 0; i < sampleSize; i++)
                {
                    for (int j = 0; j < _layersOfNeurons[n]; j++)
                    {
                        for (int k = 0; k < _layersOfNeurons[n + 1]; k++)
                        {
                            errors[n - 1][i][j] += errors[n][i][k] * _weights[n][j][k];
                        }
                        errors[n - 1][i][j] *= ReluDerivative(activations[n][i][j]);
                    }
                }

                ComputeLossGrad(n, activations, sampleSize, errors, ref weightsGrads, ref biasGrads);
            }
        }

        private void ComputeLossGrad(int layer, List<double[][]> activations, int sampleSize,
                                     double[][][] errors, ref List<double[][]> weightsGrads, ref List<double[]> biasGrads)
        {
            for (int j = 0; j < _layersOfNeurons[layer - 1]; j++)
            {
                for (int k = 0; k < _layersOfNeurons[layer]; k++)
                {
                    double sum = 0;
                    for (int i = 0; i < sampleSize; i++)
                    {
                        sum += activations[layer - 1][i][j] * errors[layer - 1][i][k];
                    }
                    weightsGrads[layer - 1][j][k] = sum + _alpha * _weights[layer - 1][j][k];
                    weightsGrads[layer - 1][j][k] /= sampleSize;
                }
            }

            for (int k = 0; k < _layersOfNeurons[layer]; k++)
            {
                double sum = 0;
                for (int i = 0; i < sampleSize; i++)
                {
                    sum += errors[layer - 1][i][k];
                }
                biasGrads[layer - 1][k] = sum / sampleSize;
            }
        }

        private double[] OutputActivationFunction(double[] x)
        {
            if (_labelType == LabelType.Binary)
                return Logistic(x);
            if (_labelType == LabelType.Multiclass)
                return Softmax(x);
            throw new Exception("It's not binary or multiclass. Check the number of unique labels.");
        }

        private double[] Logistic(double[] x)
        {
            double[] p = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                p[i] = LogisticFunction(x[i]);
            }
            return p;
        }

        private double LogisticFunction(double x)
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

        private double Relu(double x)
        {
            return Math.Max(0, x);// x < 0 ? 0 : x;
        }

        private double ReluDerivative(double x)
        {
            return Math.Max(0, 1);// x < 0 ? 0 : 1;
        }

        public int Predict(Sample sample)
        {
            List<Sample> samples = new List<Sample>();
            samples.Add(sample);

            return Predict(samples)[0];
        }

        public List<int> Predict(List<Sample> samples)
        {
            int sampleSize = samples.Count;
            List<int> preds = new List<int>();
            List<double[][]> activations = InitializeActivations(sampleSize);
            for (int i = 0; i < sampleSize; i++)
            {
                for (int j = 0; j < samples[0].Features.Count; j++)
                {
                    activations[0][i][j] = samples[i].Features[j];
                }
            }

            activations = ForwardPass(activations, sampleSize);

            for (int i = 0; i < sampleSize; i++)
            {
                List<double> samplePreds = activations[_layersOfNeurons.Count - 1][i].ToList();
                int pred = samplePreds.IndexOf(samplePreds.Max());
                preds.Add(pred);
            }

            return preds;
        }

        private List<double[][]> InitializeActivations(int sampleSize)
        {
            List<double[][]> activations = new List<double[][]>(_layersOfNeurons.Count);
            for (int n = 0; n < _layersOfNeurons.Count; n++)
            {
                double[][] a = new double[sampleSize][];
                for (int i = 0; i < sampleSize; i++)
                {
                    a[i] = new double[_layersOfNeurons[n]];
                }
                activations.Add(a);
            }

            return activations;
        }
    }

    public class MlpService : IClassifierService
    {
        private Mlp _mlp;

        public MlpService(List<int> hiddenLayersWithNeurons, double alpha, int batchSize,
                          double learningRate, int maxIterations, double tol,
                          double beta_1, double beta_2, double epsilon)
        {
            _mlp = new Mlp(hiddenLayersWithNeurons, alpha, batchSize, learningRate,
                           maxIterations, tol, beta_1, beta_2, epsilon);
        }

        public void Train(TrafficData trainTrafficData)
        {
            _mlp.Train(trainTrafficData);
        }

        public List<int> Predict(TrafficData testTrafficData)
        {
            return _mlp.Predict(testTrafficData.Samples);
        }
    }
}
