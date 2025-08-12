using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ImmutableData.Utilities
{
    public static class StepParser
    {
        public static bool TryParseStepString(string stepString, out StepType stepType, out int steps, out float min, out float max)
        {
            stepType = StepType.Linear;
            steps = 0;
            min = 0;
            max = 0;

            var pattern = @"(LIN|LOG|EXP|MUL)\((.+)\)";
            var match = Regex.Match(stepString, pattern);

            if (!match.Success)
            {
                return false;
            }

            stepType = match.Groups[1].Value switch
            {
                "LIN" => StepType.Linear,
                "LOG" => StepType.Logarithmic,
                "EXP" => StepType.Exponential,
                "MUL" => StepType.Multiplier,
                _ => StepType.Linear
            };
            
            var arguments = match.Groups[2].Value.Split(',');
            if (arguments.Length != 3)
            {
                throw new Exception($"Malformed step string {stepString}");
            }
            
            try
            {
                steps = int.Parse(arguments[0]);
                min = float.Parse(arguments[1]);
                max = float.Parse(arguments[2]);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to parse step string {stepString}", e);
            }

            return true; // Successfully parsed
        }
    }
}