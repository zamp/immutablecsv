using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ImmutableData.Utilities
{
    public static class StepGenerator
    {
        public static int[] GenerateSteps(StepType stepType, int stepCount, float min, float max)
        {
            var steps = new int[stepCount];
        
            switch (stepType)
            {
                case StepType.Linear:
                    steps = GenerateLinearSteps(stepCount, Mathf.FloorToInt(min), Mathf.FloorToInt(max));
                    break;
                case StepType.Logarithmic:
                    steps = GenerateLogarithmicSteps(stepCount, Mathf.FloorToInt(min), Mathf.FloorToInt(max));
                    break;
                case StepType.Exponential:
                    steps = GenerateExponentialSteps(stepCount, Mathf.FloorToInt(min), Mathf.FloorToInt(max));
                    break;
                case StepType.Multiplier:
                    steps = GenerateMultiplierSteps(stepCount, Mathf.FloorToInt(min), max);
                    break;
            }
        
            return steps;
        }

        private static int[] GenerateLinearSteps(int stepCount, int min, int max)
        {
            var steps = new int[stepCount];
            var stepSize = (max - min) / (float)(stepCount - 1);
        
            for (var i = 0; i < stepCount; i++)
            {
                steps[i] = min + (int)(i * stepSize);
            }
        
            return steps;
        }

        private static int[] GenerateLogarithmicSteps(int stepCount, int min, int max)
        {
            var steps = new int[stepCount];
            var logMin = Math.Log(min);
            var logMax = Math.Log(max);
            var logStepSize = (logMax - logMin) / (stepCount - 1);
        
            for (var i = 0; i < stepCount; i++)
            {
                steps[i] = (int)Math.Round(Math.Exp(logMin + i * logStepSize));
            }
        
            return steps;
        }

        private static int[] GenerateExponentialSteps(int stepCount, int min, int max)
        {
            var steps = new int[stepCount];
            var expFactor = Math.Pow((double)max / min, 1.0 / (stepCount - 1));
        
            for (var i = 0; i < stepCount; i++)
            {
                steps[i] = (int)Math.Round(min * Math.Pow(expFactor, i));
            }
        
            return steps;
        }
        
        private static int[] GenerateMultiplierSteps(int stepCount, int start, float multiplier)
        {
            var steps = new int[stepCount];
            var current = start;
            
            for (var i = 0; i < stepCount; i++)
            {
                steps[i] = current;
                current = Mathf.FloorToInt(current * multiplier);
            }

            return steps;
        }
    }
}