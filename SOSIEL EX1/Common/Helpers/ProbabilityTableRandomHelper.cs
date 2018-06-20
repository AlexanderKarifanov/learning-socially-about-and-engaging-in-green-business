using System;
using System.Linq;
using Common.Entities;
using Common.Randoms;

namespace Common.Helpers
{
    /// <summary>
    /// Probability table random helper
    /// </summary>
    public static class ProbabilityTableRandomHelper
    {
        private const double _batchCount = 10;
        private const double _probabilityMultiplier = 1000;

        /// <summary>
        /// Gets the random value within a specified range, except max value;
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="min">The minimum.</param>
        /// <param name="max">The maximum.</param>
        /// <returns></returns>
        public static int GetRandomValue(this ProbabilityTable<int> table, int min, int max)
        {
            var potentialValues = Enumerable.Range(min, max - min).ToList();

            var batchSize = (int)Math.Ceiling(potentialValues.Count / _batchCount);

            var potentialValuesTable = potentialValues.GroupBy(v => v / batchSize + 1)
                .SelectMany(g => g.Select(v => new { Value = v, Probability = table.GetProbability((int)g.Key) })).ToList();

            var randomTable = potentialValuesTable
                .SelectMany(v => Enumerable.Range(1, (int)Math.Ceiling(v.Probability * _probabilityMultiplier))
                    .Select(i => v.Value))
                .ToList();

            var random = LinearUniformRandom.GetInstance.Next(randomTable.Count);

            return randomTable[random];
        }
    }
}