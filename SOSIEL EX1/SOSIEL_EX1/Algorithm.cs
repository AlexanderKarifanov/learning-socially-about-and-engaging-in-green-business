using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Algorithm;
using Common.Configuration;
using Common.Entities;
using Common.Exceptions;
using Common.Helpers;
using SOSIEL_EX1.Configuration;
using SOSIEL_EX1.Helpers;
using SOSIEL_EX1.Output;

namespace SOSIEL_EX1
{
    public sealed class Algorithm : SosielAlgorithm, IAlgorithm
    {
        public string Name { get { return "SOSIEL"; } }

        string _outputFolder;

        ConfigurationModel _configuration;

        public static ProcessesConfiguration GetProcessConfiguration()
        {
            return new ProcessesConfiguration
            {
                ActionTakingEnabled = true,
                AnticipatoryLearningEnabled = true,
                DecisionOptionSelectionEnabled = true,
                DecisionOptionSelectionPart2Enabled = true,
                SocialLearningEnabled = true,
                CounterfactualThinkingEnabled = true,
                InnovationEnabled = true,
                ReproductionEnabled = false,
                AgentRandomizationEnabled = true,
                AgentsDeactivationEnabled = false,
                AlgorithmStopIfAllAgentsSelectDoNothing = false
            };
        }

        public Algorithm(ConfigurationModel configuration) : base(1, GetProcessConfiguration())
        {
            _configuration = configuration;

            _outputFolder = @"Output\";

            if (Directory.Exists(_outputFolder) == false)
                Directory.CreateDirectory(_outputFolder);
        }

        public string Run()
        {
            Initialize();

            var sites = new Site[] { Site.DefaultSite };

            Enumerable.Range(1, _configuration.AlgorithmConfiguration.NumberOfIterations).ForEach(iteration =>
            {
                Console.WriteLine((string)"Starting {0} iteration", (object)iteration);

                RunSosiel(sites);
            });

            return _outputFolder;
        }

        /// <summary>
        /// Executes algorithm initialization
        /// </summary>
        public void Initialize()
        {
            InitializeAgents();

            InitializeProbabilities();

            AfterInitialization();
        }

        /// <inheritdoc />
        protected override void InitializeAgents()
        {
            var agents = new List<IAgent>();

            Dictionary<string, AgentPrototype> agentPrototypes = _configuration.AgentConfiguration;

            if (agentPrototypes.Count == 0)
            {
                throw new SosielAlgorithmException("Agent prototypes were not defined. See configuration file");
            }

            InitialStateConfiguration initialState = _configuration.InitialState;

            //add donothing decision option if necessary
            agentPrototypes.ForEach(kvp =>
            {
                AgentPrototype prototype = kvp.Value;

                string prototypeName = kvp.Key;

                if (prototype.MentalProto.Any(set => set.Layers.Any(layer => layer.LayerConfiguration.UseDoNothing)))
                {
                    var added = prototype.AddDoNothingDecisionOptions();

                    initialState.AgentsState.Where(aState => aState.PrototypeOfAgent == prototypeName).ForEach(aState =>
                    {
                        aState.AssignedDecisionOptions = aState.AssignedDecisionOptions.Concat(added).ToArray();
                    });
                }
            });

            Dictionary<string, List<Common.Entities.Agent>> networks = initialState.AgentsState.SelectMany(state => state.SocialNetwork ?? new string[0]).Distinct()
                .ToDictionary(network => network, network => new List<Common.Entities.Agent>());

            //create agents, groupby is used for saving agents numeration, e.g. FE1, HM1. HM2 etc
            initialState.AgentsState.GroupBy(state => state.PrototypeOfAgent).ForEach((agentStateGroup) =>
            {
                AgentPrototype prototype = agentPrototypes[agentStateGroup.Key];

                int index = 1;

                agentStateGroup.ForEach((agentState) =>
                {
                    for (int i = 0; i < agentState.NumberOfAgents; i++)
                    {
                        Common.Entities.Agent agent = Agent.CreateAgent(agentState, prototype);
                        agent.SetId(index);

                        agents.Add(agent);

                        //check social network and add to temp dictionary
                        if (agentState.SocialNetwork != null)
                        {
                            //set first network to agent variables as household 
                            agent[SosielVariables.Network] = agentState.SocialNetwork.First();

                            agentState.SocialNetwork.ForEach((network) => networks[network].Add(agent));
                        }

                        index++;
                    }
                });
            });

            //convert temp networks to list of connetcted agents
            networks.ForEach(kvp =>
            {
                var connectedAgents = kvp.Value;

                connectedAgents.ForEach(agent =>
                {
                    agent.ConnectedAgents.AddRange(connectedAgents.Where(a => a != agent));
                });

            });


            agentList = new AgentList(agents, agentPrototypes.Select(kvp => kvp.Value).ToList());
        }

        private void InitializeProbabilities()
        {
            var probabilitiesList = new Probabilities();

            foreach (var probabilityElementConfiguration in _configuration.AlgorithmConfiguration.ProbabilitiesConfiguration)
            {
                var variableType = VariableTypeHelper.ConvertStringToType(probabilityElementConfiguration.VariableType);
                var parseTableMethod = ReflectionHelper.GetGenerecMethod(variableType, typeof(ProbabilityTableParser), "Parse");

                dynamic table = parseTableMethod.Invoke(null, new object[] { probabilityElementConfiguration.FilePath, probabilityElementConfiguration.WithHeader });

                var addToListMethod =
                    ReflectionHelper.GetGenerecMethod(variableType, typeof(Probabilities), "AddProbabilityTable");

                addToListMethod.Invoke(probabilitiesList, new object[] { probabilityElementConfiguration.Variable, table});
            }
        }

        protected override void AfterInitialization()
        {
            base.AfterInitialization();

            var hmAgents = agentList.GetAgentsWithPrefix("HM");

            hmAgents.ForEach(agent =>
            {
                agent[AlgorithmVariables.AgentIncome] = 0d;
            });
        }

        /// <inheritdoc />
        protected override Dictionary<IAgent, AgentState> InitializeFirstIterationState()
        {
            var states = new Dictionary<IAgent, AgentState>();

            agentList.Agents.ForEach(agent =>
            {
                //creates empty agent state
                AgentState agentState = AgentState.Create(agent.Prototype.IsSiteOriented);

                //copy generated goal importance
                agent.InitialGoalStates.ForEach(kvp =>
                {
                    var goalState = kvp.Value;
                    goalState.Value = agent[kvp.Key.ReferenceVariable];

                    agentState.GoalsState[kvp.Key] = goalState;
                });

                states.Add(agent, agentState);
            });

            return states;
        }

       protected override void PreIterationCalculations(int iteration)
        {
            base.PreIterationCalculations(iteration);

            if (iteration > 1)
            {
                //----
                //calculate household values (income, expenses, savings) for each agent in specific household
                var hmAgents = agentList.GetAgentsWithPrefix("HM");

                hmAgents.GroupBy(agent => agent[SosielVariables.Network])
                    .ForEach(householdAgents =>
                    {
                        double householdIncome =
                            householdAgents.Sum(agent => (double) agent[AlgorithmVariables.AgentIncome]);
                        double householdExpenses =
                            householdAgents.Sum(agent => (double) agent[AlgorithmVariables.AgentExpenses]);
                        double householdSavings = householdIncome - householdExpenses;

                        householdAgents.ForEach(agent =>
                        {
                            agent[AlgorithmVariables.HouseholdIncome] = householdIncome;
                            agent[AlgorithmVariables.HouseholdExpenses] = householdExpenses;
                            agent[AlgorithmVariables.HouseholdSavings] += householdSavings;
                        });
                    });
            }
        }

        protected override void Maintenance()
        {
            base.Maintenance();

            var hmAgents = agentList.GetAgentsWithPrefix("HM");

            hmAgents.ForEach(agent =>
            {
                //increase household members age
                agent[AlgorithmVariables.Age] += 1;
            });
        }

        /// <inheritdoc />
        protected override void PostIterationStatistic(int iteration)
        {
            base.PostIterationStatistic(iteration);

            agentList.Agents.ForEach(agent =>
            {
                var details = new AgentDetailsOutput
                {
                    Iteration = iteration,
                    AgentId = agent.Id,
                    Age = agent[AlgorithmVariables.Age],
                    Income = agent[AlgorithmVariables.AgentIncome],
                    Expenses = agent[AlgorithmVariables.AgentExpenses],
                    Savings = agent[AlgorithmVariables.HouseholdSavings],
                    NumberOfKH = agent.AssignedDecisionOptions.Count
                };

                CSVHelper.AppendTo(_outputFolder + string.Format(AgentDetailsOutput.FileName, agent.Id), details);
            });
        }
    }
}
