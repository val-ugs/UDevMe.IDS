using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface IMlpService
    {
        public List<int> Predict(TrafficData trainTrafficData, TrafficData testTrafficData,
                                 List<int> hiddenLayersWithNeurons, double alpha, int batchSize,
                                 double learningRate, int maxIterations, double tol,
                                 double beta_1, double beta_2, double epsilon);
    }
}
