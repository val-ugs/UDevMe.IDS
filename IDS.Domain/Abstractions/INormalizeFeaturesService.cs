using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface INormalizeFeaturesService
    {
        public List<Sample> NormalizeTrainSamples(List<Sample> samples, double min, double max);
        public List<Sample> NormalizeTestSamples(List<Sample> samples);
    }
}
