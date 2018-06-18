using System;
using System.Collections.Generic;
using System.Linq;
using Common.Configuration;
using Common.Entities;
using Common.Helpers;
using Common.Randoms;

namespace Common.Processes.Demographic
{
    public class Demographic
    {
        private DemographicProcessesConfiguration _configuration;
        private ProbabilityTable<int> _birthProbability;
        private ProbabilityTable<int> _deathProbability;

        private Dictionary<int, List<IAgent>> _births = new Dictionary<int, List<IAgent>>();


        public Demographic(DemographicProcessesConfiguration configuration, ProbabilityTable<int> birthProbability, ProbabilityTable<int> deathProbability)
        {
            _configuration = configuration;
            _birthProbability = birthProbability;
            _deathProbability = deathProbability;
        }


        public void ChangeDemographic(int iteration, Dictionary<IAgent, AgentState> iterationState, ICollection<IAgent> agents)
        {
            ProcessBirths(iteration, iterationState, agents);

            ProcessPairing(iteration, agents);
        }

        private void ProcessBirths(int iteration, Dictionary<IAgent, AgentState> iterationState, ICollection<IAgent> agents)
        {
            var iterationAgents = new List<IAgent>();

            var unactiveAgents = _births.Where(kvp => kvp.Key >= iteration - _configuration.YearsBetweenBirths)
                .SelectMany(kvp => kvp.Value)
                .ToList();

            var activeAgents = agents.Except(unactiveAgents).ToList();

            var pairs = activeAgents.Where(a => a[SosielVariables.PairStatus] == PairStatus.Paired)
                .GroupBy(a => a[SosielVariables.NuclearFamily])
                .ToList();

            foreach (var pair in pairs)
            {
                var pairList = pair.ToList();

                if(pairList.Count != 2) continue;

                var averageAge = (int)Math.Ceiling(pair.Average(a => (int)a[SosielVariables.Age]));

                //generate random value to determine gender
                var gender = LinearUniformRandom.GetInstance.Next(2);
                var baseParent = LinearUniformRandom.GetInstance.Next(2);

                var baseAgent = pairList[baseParent];
                var baseAgentState = iterationState[baseAgent];

                if (_birthProbability.IsVariableSpecificEventOccur(averageAge))
                {
                    var child = baseAgent.CreateChild(gender == 0 ? Gender.Male : Gender.Female);

                    child.SetId(agents.Count + 1);

                    var childState = baseAgentState.CreateChildCopy();

                    agents.Add(child);
                    iterationState[child] = childState;

                    iterationAgents.AddRange(pairList);
                }
            }

            _births[iteration] = iterationAgents;
        }

        private void ProcessPairing(int iteration, ICollection<IAgent> agents)
        {
            
        }

    }
}