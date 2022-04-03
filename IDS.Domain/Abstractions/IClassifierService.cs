using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface IClassifierService
    {
        public void Train(TrafficData trainTrafficData);
        public List<int> Predict(TrafficData testTrafficData);
    }
}
