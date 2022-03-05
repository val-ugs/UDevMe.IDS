using IDS.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class ConvertToTrafficFeaturesService : IConvertToTrafficFeaturesService
    {
        public List<double> ConvertFromUnswData(string[] values)
        {
            List<double> features = new List<double>();
            double number;

            List<string> attackNames = new List<string>
            {
                "Fuzzers", "Analysis", "Backdoor", "DoS", "Exploits",
                "Generic", "Reconnaissance", "Shellcode", "Worms"
            };

            for (int i = 0; i < values.Length; i++)
            {
                if (i == values.Length - 2)
                {
                    // Labels
                    if (values[i] == "Normal")
                    {
                        features.Add(0);
                        break;
                    }
                    foreach (string attackName in attackNames)
                        if (values[i] == attackName)
                            features.Add(1);
                    break;
                }
                else
                {
                    bool result = double.TryParse(values[i], NumberStyles.Any, CultureInfo.InvariantCulture, out number);
                    if (result)
                    {
                        features.Add(number);
                    }
                    else
                    {
                        // if string then OneHotEncoder or Ignore
                        // TODO: OneHotEncoder
                    }
                }
            }

            return features;
        }

        private double[] OneHotEncoder(string value)
        {
            //install 
            //return CategoricalCatalog.OneHotEncoding()
            return new double[] { 0, 0 };
        }
    }
}
