using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Processes
{
    using Enums;
    using Entities;
    using Helpers;
    using Randoms;

    /// <summary>
    /// Innovation process implementation.
    /// </summary>
    public class Innovation
    {
        /// <summary>
        /// Executes agent innovation process for specific site
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="lastIteration"></param>
        /// <param name="goal"></param>
        /// <param name="layer"></param>
        public void Execute(IAgent agent, LinkedListNode<Dictionary<IAgent, AgentState>> lastIteration, Goal goal,
            DecisionOptionLayer layer, Site site)
        {
            Dictionary<IAgent, AgentState> currentIteration = lastIteration.Value;
            Dictionary<IAgent, AgentState> priorIteration = lastIteration.Previous.Value;

            //gets prior period activated decision options
            DecisionOptionsHistory history = priorIteration[agent].DecisionOptionsHistories[site];
            DecisionOption priorPeriodDecisionOption = history.Activated.Single(r=>r.Layer == layer);

            LinkedListNode<Dictionary<IAgent, AgentState>> tempNode = lastIteration.Previous;

            //if prior period decision option is do nothing then looking for any do something decision option
            while (priorPeriodDecisionOption.IsAction == false && tempNode.Previous != null)
            {
                tempNode = tempNode.Previous;

                history = tempNode.Value[agent].DecisionOptionsHistories[site];

                priorPeriodDecisionOption = history.Activated.Single(r => r.Layer == layer);
            }

            //if the layer or prior period decision option are modifiable then generate new decision option
            if (layer.LayerConfiguration.Modifiable || (!layer.LayerConfiguration.Modifiable && priorPeriodDecisionOption.IsModifiable))
            {
                DecisionOptionLayerConfiguration parameters = layer.LayerConfiguration;

                Goal selectedGoal = goal;

                GoalState selectedGoalState = lastIteration.Value[agent].GoalsState[selectedGoal];

                #region Generating consequent
                double min = parameters.MinValue(agent);
                double max = parameters.MaxValue(agent);

                double consequentValue = string.IsNullOrEmpty(priorPeriodDecisionOption.Consequent.VariableValue)
                    ? priorPeriodDecisionOption.Consequent.Value
                    : agent[priorPeriodDecisionOption.Consequent.VariableValue];

                double newConsequent = consequentValue;

                switch (selectedGoalState.AnticipatedDirection)
                {
                    case AnticipatedDirection.Up:
                        {
                            if (DecisionOptionLayerConfiguration.ConvertSign(parameters.ConsequentRelationshipSign[goal.Name]) == ConsequentRelationship.Positive)
                            {
                                if (consequentValue == max) return;

                                max = Math.Abs(consequentValue - max);

                                newConsequent += (Math.Abs(PowerLawRandom.GetInstance.Next(min, max) - max));
                            }
                            if (DecisionOptionLayerConfiguration.ConvertSign(parameters.ConsequentRelationshipSign[goal.Name]) == ConsequentRelationship.Negative)
                            {
                                if (consequentValue == min) return;

                                max = Math.Abs(consequentValue - min);

                                newConsequent -= (Math.Abs(PowerLawRandom.GetInstance.Next(min, max) - max));
                            }

                            break;
                        }
                    case AnticipatedDirection.Down:
                        {
                            if (DecisionOptionLayerConfiguration.ConvertSign(parameters.ConsequentRelationshipSign[goal.Name]) == ConsequentRelationship.Positive)
                            {
                                if (consequentValue == min) return;

                                max = Math.Abs(consequentValue - min);

                                newConsequent -= (Math.Abs(PowerLawRandom.GetInstance.Next(min, max) - max));
                            }
                            if (DecisionOptionLayerConfiguration.ConvertSign(parameters.ConsequentRelationshipSign[goal.Name]) == ConsequentRelationship.Negative)
                            {
                                if (consequentValue == max) return;

                                max = Math.Abs(consequentValue - max);

                                newConsequent += (Math.Abs(PowerLawRandom.GetInstance.Next(min, max) - max));
                            }

                            break;
                        }
                    default:
                        {
                            throw new Exception("Not implemented for AnticipatedDirection == 'stay'");
                        }
                }

                DecisionOptionConsequent consequent = DecisionOptionConsequent.Renew(priorPeriodDecisionOption.Consequent, newConsequent);
                #endregion


                #region Generating antecedent
                List<DecisionOptionAntecedentPart> antecedentList = new List<DecisionOptionAntecedentPart>(priorPeriodDecisionOption.Antecedent.Length);

                foreach (DecisionOptionAntecedentPart antecedent in priorPeriodDecisionOption.Antecedent)
                {
                    dynamic newConst = agent[antecedent.Param];

                    DecisionOptionAntecedentPart newAntecedent = DecisionOptionAntecedentPart.Renew(antecedent, newConst);

                    antecedentList.Add(newAntecedent);
                }
                #endregion

                AgentState agentState = currentIteration[agent];

                DecisionOption newDecisionOption = DecisionOption.Renew(priorPeriodDecisionOption, antecedentList.ToArray(), consequent);


                //change base ai values for the new decision option
                double consequentChangeProportion;
                if (consequentValue == 0)
                {
                    consequentChangeProportion = 0;
                }
                else
                {
                    consequentChangeProportion = Math.Abs(newDecisionOption.Consequent.Value - consequentValue) / consequentValue;
                }

                
                Dictionary<Goal, double> baseAI = agent.AnticipationInfluence[priorPeriodDecisionOption];

                Dictionary<Goal, double> proportionalAI = new Dictionary<Goal, double>();



                agent.AssignedGoals.ForEach(g =>
                {
                    double ai = baseAI[g];

                    ConsequentRelationship relationship = DecisionOptionLayerConfiguration.ConvertSign(priorPeriodDecisionOption.Layer.LayerConfiguration.ConsequentRelationshipSign[g.Name]);

                    double difference = Math.Abs(ai * consequentChangeProportion);


                    switch(selectedGoalState.AnticipatedDirection)
                    {
                        case AnticipatedDirection.Up:
                            {
                                if (relationship == ConsequentRelationship.Positive)
                                {
                                    ai -= difference;
                                }
                                else
                                {
                                    ai += difference;
                                }

                                break;
                            }
                        case AnticipatedDirection.Down:
                            {
                                if (relationship == ConsequentRelationship.Positive)
                                {
                                    ai += difference;
                                }
                                else
                                {
                                    ai -= difference;
                                }

                                break;
                            }
                    }

                    proportionalAI.Add(g, ai);
                });


                //add the generated decision option to the prototype's mental model and assign one to the agent's mental model 
                if (agent.Prototype.IsSimilarDecisionOptionExists(newDecisionOption) == false)
                {
                    //add to the prototype and assign to current agent
                    agent.AddDecisionOption(newDecisionOption, layer, proportionalAI);
                }
                else if (agent.AssignedDecisionOptions.Any(decisionOption => decisionOption == newDecisionOption) == false)
                {
                    var kh = agent.Prototype.DecisionOptions.FirstOrDefault(h => h == newDecisionOption);

                    //assign to current agent only
                    agent.AssignNewDecisionOption(kh, proportionalAI);
                }


                if (layer.Set.Layers.Count > 1)
                    //set consequent to actor's variables for next layers
                    newDecisionOption.Apply(agent);
            }
        }
    }
}