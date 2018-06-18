using System.Collections;
using System.Collections.Generic;
using Common.Randoms;

namespace Common.Entities
{
    public class ProbabilityTable<T>
    {
        private const int _multiplexer = 1000;

        private Dictionary<T, double> _probabilityTable;

        public ProbabilityTable(Dictionary<T, double> pairs)
        {
            _probabilityTable = new Dictionary<T, double>(pairs);
        }

        public double GetProbability(T value)
        {
            double probability;
            _probabilityTable.TryGetValue(value, out probability);
            return probability;
        }

        public bool IsVariableSpecificEventOccur(T value)
        {
            var probability = GetProbability(value) * _multiplexer;
            var randomValue = LinearUniformRandom.GetInstance.Next(1, _multiplexer + 1);

            return randomValue <= probability;
        }
    }
}