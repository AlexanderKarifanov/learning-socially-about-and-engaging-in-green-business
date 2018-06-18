using System;
using System.Data;

namespace Common.Helpers
{
    /// <summary>
    /// Contains variable names used in code.
    /// </summary>
    public class SosielVariables
    {
        public const string AgentType = "AgentType";
        public const string AgentStatus = "AgentStatus";

        public const string AgentPrefix = "Agent";
        public const string PreviousPrefix = "Previous";

        public const string Household = "Household";
        public const string NuclearFamily = "NuclearFamily";
        public const string ExternalRelations = "ExternalRelations";

        public const string PairStatus = "PairStatus";
        public const string Age = "Age";
        public const string Gender = "Gender";
    }

    public class PairStatus
    {
        public const string Paired = "paired";
        public const string Unpaired = "unpaired";
    }

    public class Gender
    {
        public const string Male = "male";
        public const string Female = "female";
    }
}
