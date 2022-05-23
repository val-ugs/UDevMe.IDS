using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Models
{
    public class Sample
    {
        public Sample() { }

        public Sample(List<double> features, int label = -1)
        {
            Features = features;
            Label = label;
        }
        public List<double> Features { get; set; }
        public int Label { get; set; }
    }

    public class TrafficData
    {
        public TrafficData(int numberOfSamples)
        {
            Samples = new List<Sample>(numberOfSamples);
        }

        public List<Sample> Samples { get; set; }
    }
}
