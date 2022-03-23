using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface IMetricService
    {
        double Calculate(List<int> trueLabels, List<int> predictedLabels);
    }
}
