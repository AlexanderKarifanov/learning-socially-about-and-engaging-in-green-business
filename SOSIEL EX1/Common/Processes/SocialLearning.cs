﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Processes
{
    using Entities;
    using Helpers;


    /// <summary>
    /// Social learning process implementation.
    /// </summary>
    public class SocialLearning
    {
        /// <summary>
        /// Executes social learning process of current agent for specific decision option set layer
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="lastIteration"></param>
        /// <param name="layer"></param>
        public void ExecuteLearning(IAgent agent, LinkedListNode<Dictionary<IAgent, AgentState>> lastIteration, DecisionOptionLayer layer)
        {
            Dictionary<IAgent, AgentState> priorIterationState = lastIteration.Previous.Value;

            agent.ConnectedAgents.Randomize().ForEach(neighbour =>
            {
                AgentState priorIteration;
                if (!priorIterationState.TryGetValue(neighbour, out priorIteration)) return;

                IEnumerable<DecisionOption> activatedDecisionOptions = priorIteration.DecisionOptionsHistories
                    .SelectMany(rh => rh.Value.Activated).Where(r => r.Layer == layer);

                activatedDecisionOptions.ForEach(decisionOption =>
                {
                    if (agent.AssignedDecisionOptions.Contains(decisionOption) == false)
                    {
                        agent.AssignNewDecisionOption(decisionOption, neighbour.AnticipationInfluence[decisionOption]);
                    }
                });

            });
        }
    }
}
