using IDS.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class AccuracyMetricService : IMetricService
    {
        public double Calculate(List<int> trueLabels, List<int> predictedLabels)
        {
            if (trueLabels.Count != predictedLabels.Count)
                return 0;

            double counter = 0;
            for (int i = 0; i < trueLabels.Count; i++)
            {
                if (trueLabels[i] == predictedLabels[i])
                    counter++;
            }

            return counter / trueLabels.Count;
        }
    }
}
