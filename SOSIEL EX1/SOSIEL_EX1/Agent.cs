﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common.Entities;
using Common.Helpers;
using Common.Randoms;
using SOSIEL_EX1.Configuration;

namespace SOSIEL_EX1
{
    public sealed class Agent : Common.Entities.Agent
    {
        public AgentStateConfiguration AgentStateConfiguration { get; private set; }

        public override Common.Entities.Agent Clone()
        {
            Agent agent = (Agent)base.Clone();

            return agent;
        }

        public override Common.Entities.Agent CreateChild(string gender)
        {
            Agent child = (Agent)base.CreateChild(gender);

            child[AlgorithmVariables.AgentIncome] = 0;
            child[AlgorithmVariables.AgentExpenses] = 0;
            child[AlgorithmVariables.AgentSavings] = 0;

            return child;
        }

        protected override Common.Entities.Agent CreateInstance()
        {
            return new Agent();
        }

        public void GenerateCustomParams()
        {
            
        }

        /// <summary>
        /// Creates agent instance based on agent prototype and agent configuration 
        /// </summary>
        /// <param name="agentConfiguration"></param>
        /// <param name="prototype"></param>
        /// <returns></returns>
        public static Agent CreateAgent(AgentStateConfiguration agentConfiguration, AgentPrototype prototype)
        {
            Agent agent = new Agent();

            agent.Prototype = prototype;
            agent.privateVariables = new Dictionary<string, dynamic>(agentConfiguration.PrivateVariables);

            agent.AssignedDecisionOptions = prototype.DecisionOptions.Where(r => agentConfiguration.AssignedDecisionOptions.Contains(r.Id)).ToList();
            agent.AssignedGoals = prototype.Goals.Where(g => agentConfiguration.AssignedGoals.Contains(g.Name)).ToList();

            agent.AssignedDecisionOptions.ForEach(kh => agent.DecisionOptionActivationFreshness.Add(kh, 1));

            //generate goal importance
            agentConfiguration.GoalsState.ForEach(kvp =>
            {
                var goalName = kvp.Key;
                var configuration = kvp.Value;

                var goal = agent.AssignedGoals.FirstOrDefault(g => g.Name == goalName);
                if (goal == null) return;

                double importance = configuration.Importance;

                if (configuration.Randomness)
                {
                    if (string.IsNullOrEmpty(configuration.BasedOn))
                    {
                        var from = (int)(configuration.RandomFrom * 10);
                        var to = (int)(configuration.RandomTo * 10);

                        importance = GenerateImportance(agent, configuration.RandomFrom, configuration.RandomTo);
                    }
                    else
                    {
                        var anotherGoalImportance = agent.InitialGoalStates[agent.AssignedGoals.FirstOrDefault(g => g.Name == configuration.BasedOn)]
                            .Importance;

                        importance = Math.Round(1 - anotherGoalImportance, 2);
                    }
                }

                GoalState goalState = new GoalState(configuration.Value, goal.FocalValue, importance);

                agent.InitialGoalStates.Add(goal, goalState);

                agent[string.Format("{0}_Importance", goal.Name)] = importance;
            });

            //initializes initial anticipated influence for each kh and goal assigned to the agent
            agent.AssignedDecisionOptions.ForEach(kh =>
            {
                Dictionary<string, double> source;

                if (kh.AutoGenerated && agent.Prototype.DoNothingAnticipatedInfluence != null)
                {
                    source = agent.Prototype.DoNothingAnticipatedInfluence;
                }
                else
                {
                    agentConfiguration.AnticipatedInfluenceState.TryGetValue(kh.Id, out source);
                }


                Dictionary<Goal, double> inner = new Dictionary<Goal, double>();

                agent.AssignedGoals.ForEach(g =>
                {
                    inner.Add(g, source != null && source.ContainsKey(g.Name) ? source[g.Name] : 0);
                });

                agent.AnticipationInfluence.Add(kh, inner);
            });


            InitializeDynamicvariables(agent);

            agent.AgentStateConfiguration = agentConfiguration;

            return agent;
        }

        private static void InitializeDynamicvariables(Agent agent)
        {
            agent[AlgorithmVariables.IsActive] = true;
        }

        private static double GenerateImportance(Agent agent, double min, double max)
        {
            double rand;

            if (agent.ContainsVariable(AlgorithmVariables.Mean) && agent.ContainsVariable(AlgorithmVariables.StdDev))
                rand = NormalDistributionRandom.GetInstance.Next(agent[AlgorithmVariables.Mean], agent[AlgorithmVariables.StdDev]);
            else
                rand = NormalDistributionRandom.GetInstance.Next();

            rand = Math.Round(rand, 1, MidpointRounding.AwayFromZero);

            if (rand < min || rand > max)
                return GenerateImportance(agent, min, max);

            return rand;
        }
    }
}
