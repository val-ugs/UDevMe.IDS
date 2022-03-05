using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Models
{
    public class TrafficData
    {
        public TrafficData()
        {
            Features = new List<List<double>>();
        }

        public List<List<double>> Features { get; set; }
    }
}
