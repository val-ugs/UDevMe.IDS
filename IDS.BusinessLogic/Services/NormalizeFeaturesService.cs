using IDS.Domain.Abstractions;
using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class NormalizeFeaturesService : INormalizeFeaturesService
    {
        int _featureCount;
        double _min, _max; 
        double[] _maxValue, _minValue;

        public List<Sample> NormalizeTrainSamples(List<Sample> samples, double min, double max)
        {
            _featureCount = samples[0].Features.Count;
            _min = min;
            _max = max;
            _maxValue = new double[_featureCount];
            _minValue = new double[_featureCount];

            for (int i = 0; i < _featureCount; i++)
            {
                _maxValue[i] = -999;
                _minValue[i] = 999;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                for (int j = 0; j < _featureCount; j++)
                {
                    _maxValue[j] = samples[i].Features[j] > _maxValue[j] ? samples[i].Features[j] : _maxValue[j];
                    _minValue[j] = samples[i].Features[j] < _minValue[j] ? samples[i].Features[j] : _minValue[j];
                }
            }

            return NormalizeSamples(samples);
        }
        public List<Sample> NormalizeTestSamples(List<Sample> samples)
        {
            if (_minValue == null && _maxValue == null)
                throw new Exception("Samples not normalize");
            return NormalizeSamples(samples);
        }

        private List<Sample> NormalizeSamples(List<Sample> samples)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                for (int j = 0; j < samples[i].Features.Count; j++)
                {
                    samples[i].Features[j] = Normalize(samples[i].Features[j], j, _min, _max);
                }
            }

            return samples;
        }

        private double Normalize(double value, int index, double min, double max)
        {
            double range = _maxValue[index] - _minValue[index];
            if (range == 0)
                return 1;
            double normalizeValue = (value - _minValue[index]) / range;
            normalizeValue = (1 - normalizeValue) * min + normalizeValue * max;

            return normalizeValue;
        }
    }
}
