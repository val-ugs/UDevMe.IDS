using IDS.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.BusinessLogic.Services
{
    public class F1ScoreMetricService : IMetricService
    {
        public double Calculate(List<int> trueLabels, List<int> predictedLabels)
        {
            if (trueLabels.Count != predictedLabels.Count)
                return 0;

            int[] distinctLabels = trueLabels.Distinct().ToArray();
            double[] f1Scores = new double[distinctLabels.Length];

            for (int i = 0; i < distinctLabels.Length; i++)
            {
                double tp = 0, fp = 0, fn = 0;

                for (int j = 0; j < trueLabels.Count; j++)
                {
                    if (trueLabels[j] == predictedLabels[j]
                        && trueLabels[j] == distinctLabels[i])
                        tp++;
                    else
                    {
                        if (distinctLabels[i] == predictedLabels[j])
                            fp++;
                        if (distinctLabels[i] == trueLabels[j])
                            fn++;
                    };
                }

                double precision = tp / (tp + fp);
                double recall = tp / (tp + fn);

                f1Scores[i] = 2 * precision * recall / (precision + recall);
            }

            return f1Scores.Sum() / distinctLabels.Length;
        }
    }
}
