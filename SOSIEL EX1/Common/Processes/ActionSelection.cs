﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Processes
{
    using Entities;
    using Helpers;
    using Enums;
    using Exceptions;


    /// <summary>
    /// Action selection process implementation.
    /// </summary>
    public class ActionSelection : VolatileProcess
    {
        Goal processedGoal;
        GoalState goalState;


        Dictionary<DecisionOption, Dictionary<Goal, double>> anticipatedInfluence;

        DecisionOption[] matchedDecisionOptions;


        DecisionOption priorPeriodActivatedDecisionOption;
        DecisionOption decisionOptionForActivating;

        #region Specific logic for tendencies
        protected override void EqualToOrAboveFocalValue()
        {
            //We don't do anything. Do nothing decisionOption will be selected later.
        }

        protected override void Maximize()
        {
            if (matchedDecisionOptions.Length > 0)
            {
                DecisionOption[] selected = matchedDecisionOptions.GroupBy(r => anticipatedInfluence[r][processedGoal]).OrderByDescending(hg => hg.Key).First().ToArray();

                decisionOptionForActivating = selected.RandomizeOne();
            }
        }

        protected override void Minimize()
        {
            if (matchedDecisionOptions.Length > 0)
            {
                DecisionOption[] selected = matchedDecisionOptions.GroupBy(r => anticipatedInfluence[r][processedGoal]).OrderBy(hg => hg.Key).First().ToArray();

                decisionOptionForActivating = selected.RandomizeOne();
            }
        }
        #endregion

        /// <summary>
        /// Shares collective action among same household agents
        /// </summary>
        /// <param name="currentAgent"></param>
        /// <param name="decisionOption"></param>
        /// <param name="agentStates"></param>
        void ShareCollectiveAction(IAgent currentAgent, DecisionOption decisionOption, Dictionary<IAgent, AgentState> agentStates)
        {
            var scope = decisionOption.Scope;

            foreach (IAgent neighbour in currentAgent.ConnectedAgents
                .Where(connected => connected[scope] == currentAgent[scope] || scope == null))
            {
                if (neighbour.AssignedDecisionOptions.Contains(decisionOption) == false)
                {
                    neighbour.AssignNewDecisionOption(decisionOption, currentAgent.AnticipationInfluence[decisionOption]);
                }
            }
        }

        /// <summary>
        /// Executes first part of action selection for specific agent and site
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="lastIteration"></param>
        /// <param name="rankedGoals"></param>
        /// <param name="processedDecisionOptions"></param>
        /// <param name="site"></param>
        public void ExecutePartI(IAgent agent, LinkedListNode<Dictionary<IAgent, AgentState>> lastIteration,
            Goal[] rankedGoals, DecisionOption[] processedDecisionOptions, Site site)
        {
            decisionOptionForActivating = null;

            AgentState agentState = lastIteration.Value[agent];
            AgentState priorPeriod = lastIteration.Previous?.Value[agent];

            //adds new decisionOption history for specific site if it doesn't exist
            if (agentState.DecisionOptionsHistories.ContainsKey(site) == false)
                agentState.DecisionOptionsHistories.Add(site, new DecisionOptionsHistory());

            DecisionOptionsHistory history = agentState.DecisionOptionsHistories[site];

            processedGoal = rankedGoals.First(g => processedDecisionOptions.First().Layer.Set.AssociatedWith.Contains(g));
            goalState = agentState.GoalsState[processedGoal];

            matchedDecisionOptions = processedDecisionOptions.Except(history.Blocked).Where(h => h.IsMatch(agent)).ToArray();

            if (matchedDecisionOptions.Length == 0)
            {
                return;
            }

            if (matchedDecisionOptions.Length > 1)
            {
                if (priorPeriod != null)
                    priorPeriodActivatedDecisionOption = priorPeriod.DecisionOptionsHistories[site].Activated.FirstOrDefault(r => r.Layer == processedDecisionOptions.First().Layer);

                //set anticipated influence before execute specific logic
                anticipatedInfluence = agent.AnticipationInfluence;

                SpecificLogic(processedGoal.Tendency);
            }
            else
                decisionOptionForActivating = matchedDecisionOptions[0];

            if (processedDecisionOptions.First().Layer.Set.Layers.Count > 1)
                decisionOptionForActivating.Apply(agent);


            if (decisionOptionForActivating.IsCollectiveAction)
            {
                ShareCollectiveAction(agent, decisionOptionForActivating, lastIteration.Value);
            }

            if (decisionOptionForActivating != null)
            {
                history.Activated.Add(decisionOptionForActivating);
            }

            history.Matched.AddRange(matchedDecisionOptions);
        }

        /// <summary>
        /// Executes second part of action selection for specific site
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="lastIteration"></param>
        /// <param name="rankedGoals"></param>
        /// <param name="processedDecisionOptions"></param>
        /// <param name="site"></param>
        public void ExecutePartII(IAgent agent, LinkedListNode<Dictionary<IAgent, AgentState>> lastIteration,
            Goal[] rankedGoals, DecisionOption[] processedDecisionOptions, Site site)
        {
            AgentState agentState = lastIteration.Value[agent];

            DecisionOptionsHistory history = agentState.DecisionOptionsHistories[site];

            DecisionOptionLayer layer = processedDecisionOptions.First().Layer;


            DecisionOption selectedDecisionOptions = history.Activated.Single(r => r.Layer == layer);

            if (selectedDecisionOptions.IsCollectiveAction)
            {
                var scope = selectedDecisionOptions.Scope;

                //counting agents which selected this decision option
                int numberOfInvolvedAgents = agent.ConnectedAgents.Where(connected => agent[scope] == connected[scope] || scope == null)
                    .Count(a => lastIteration.Value[a].DecisionOptionsHistories[site].Activated.Any(decisionOption => decisionOption == selectedDecisionOptions));

                int requiredParticipants = selectedDecisionOptions.RequiredParticipants - 1;

                //add decision option to blocked
                if (numberOfInvolvedAgents < requiredParticipants)
                {
                    history.Blocked.Add(selectedDecisionOptions);

                    history.Activated.Remove(selectedDecisionOptions);

                    ExecutePartI(agent, lastIteration, rankedGoals, processedDecisionOptions, site);

                    ExecutePartII(agent, lastIteration, rankedGoals, processedDecisionOptions, site);
                }
            }
        }
    }
}

