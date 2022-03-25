using IDS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.Domain.Abstractions
{
    public interface ITrafficDataConverterService
    {
        TrafficData ConvertTrainData(List<string[]> data, DataSource dataType,
                                     ClassificationType classificationType, bool hasOneHotEncode);
        TrafficData ConvertTestData(List<string[]> data);
    }
}
