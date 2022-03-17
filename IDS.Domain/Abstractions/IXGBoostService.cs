using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface IXGBoostService
    {
        public List<int> Predict(TrafficData trainTrafficData, TrafficData testTrafficData, int rounds,
                                 int maxDepth, int minSize, double learningRate,
                                 double lambda, int gamma, double nFeaturesRatio);
    }
}
