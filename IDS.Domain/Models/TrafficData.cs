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

        public Sample(List<double> features, int label)
        {
            Features = features;
            Label = label;
        }
        public List<double> Features { get; set; }
        public int Label { get; set; }
    }

    public class TrafficData
    {
        public TrafficData()
        {
            Samples = new List<Sample>();
        }

        public List<Sample> Samples { get; set; }
    }
}
