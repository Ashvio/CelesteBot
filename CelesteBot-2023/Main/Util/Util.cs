﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CelesteBot_2023
{

    public class Util
    {
        private static Dictionary<string, List<Vector2>> positionalFitnesses;
        private static Dictionary<string, List<Vector2>> velocityFitnesses;
        private static List<string> rawLevels;

        public static IEnumerable<T> SliceRow<T>(T[,] array, int row)
        {
            for (var i = 0; i < array.GetLength(0); i++)
            {
                yield return array[i, row];
            }
        }
        public static double Normalize(double value, double min, double max)
        {
            return (value - (max - min) / 2) / ((max - min) / 2);
        }
        public static Dictionary<string, List<Vector2>> GetPositionFitnesses(string FitnessPath)
        {
            if (positionalFitnesses == null)
            {
                rawLevels = new List<string>();
                positionalFitnesses = new Dictionary<string, List<Vector2>>();
                velocityFitnesses = new Dictionary<string, List<Vector2>>();
                string[] lines = System.IO.File.ReadAllLines(FitnessPath);

                foreach (string line in lines)
                {
                    if (!line.Contains(": "))
                    {
                        continue;
                    }
                    string[] items = line.Split(new string[] { ": " }, StringSplitOptions.None);
                    string name = items[0];
                    string temp1 = items[1].Split(new string[] { "[" }, StringSplitOptions.None)[1];
                    string temp2 = temp1.Split(new string[] { "]" }, StringSplitOptions.None)[0];
                    string[] values = temp2.Split(new string[] { ", " }, StringSplitOptions.None);
                    if (positionalFitnesses.ContainsKey(name))
                    {
                        // Okay so we already contain this exact name, we need to do something new now...
                        // We need to instead make this value a special list of vector2s that contains all that need to be followed.
                        // The problem here is that for 2-1 (one of the rooms u must cross twice) we have a problem... It's not as easy as a key or something like that
                        // Because we actually have to go from one floor to another. In this case, I think we need to add a new parameter (are we dreaming?)
                        // And the dictionary should change to have vectors as lists
                        // Also we should add a parameter whether we need to cross the room, or if we just stay in the room for the key. This should probably be hardcoded
                        // Possibly with a new key that we press
                        // (shift-space?): Will cross the room again (need to keep track of rooms that we will cross many times)
                        // (space): Will only enter-exit the room once
                        // The data that this dictionary contains should now be objects instead of vector2 lists... they need to contains lots of info, so lets set that up too.
                    }
                    else
                    {
                        positionalFitnesses.Add(name, new List<Vector2>());
                        velocityFitnesses.Add(name, new List<Vector2>());
                    }
                    Vector2 position = new((float)Convert.ToDouble(values[0]), (float)Convert.ToDouble(values[1]));
                    Vector2 velocity = new((float)Convert.ToDouble(values[2]), (float)Convert.ToDouble(values[3]));

                    rawLevels.Add(name);
                    positionalFitnesses[name].Add(position);
                    velocityFitnesses[name].Add(velocity);
                }
            }

            return positionalFitnesses;
        }
        public static Dictionary<string, List<Vector2>> GetVelocityFitnesses(string FitnessPath)
        {
            if (velocityFitnesses == null)
            {
                GetPositionFitnesses(FitnessPath);
            }
            return velocityFitnesses;
        }
        public static List<string> GetRawLevelsInOrder(string FitnessPath)
        {
            if (rawLevels == null)
            {
                GetPositionFitnesses(FitnessPath);
            }
            return rawLevels;
        }
    }
}
