﻿using System;
using System.Linq;
using Common.Entities;
using Common.Helpers;
using SOSIEL_EX1.Models;

namespace SOSIEL_EX1.Helpers
{
    public static class ProbabilityTableParser
    {
        /// <summary>
        /// Parses the specified probability table.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="headerExists"></param>
        /// <returns></returns>
        public static ProbabilityTable<T> Parse<T>(string fileName, bool headerExists)
        {
            var probabilities = CSVHelper.ReadAllRecords<ProbabilityRecord<T>>(fileName, headerExists, typeof(ProbabilityRecordMap<T>));

            return new ProbabilityTable<T>(probabilities.ToDictionary(p => p.Value, p => p.Probability));
        }
    }
}