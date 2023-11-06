using Celeste;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Monocle;
using System.Threading.Tasks;
using static CelesteBot_2023.CelesteBotInteropModule;
using System.Text.RegularExpressions;
using System.IO;
using Celeste.Mod;

namespace CelesteBot_2023
{

    public class TargetFinder
    {
        const string LEVEL_ORDER_PATH = @"levels.csv";
        private static List<Tuple<string, string>> levelKeysInCompletionOrder;
        private static Dictionary<Tuple<string, string>, Vector2> targetSpawnPoints;
        private static string maxLevelIndex;
        private static Tuple<string, string> currentTargetLevel;
        private static Vector2 currentTarget;

        public static string CurrentMap { get; private set; }

        [Initialize]
        public static void Initialize()
        {
            using (var reader = new StreamReader(LEVEL_ORDER_PATH))
            {
                levelKeysInCompletionOrder = new();
                bool firstLine = true;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (firstLine)
                    {
                        firstLine = false;
                        continue;
                    }
                    var values = line.Split(',');
                    Tuple<string, string> tuple = Tuple.Create(values[0], values[1]);

                    levelKeysInCompletionOrder.Add(tuple);
                }
            }
        }
        public static string[] levelFilenameAndNameFromKey(string levelKey)
        {
            string pattern = @"(^[\d+\-\p{Lu}\p{Ll}]+)_(\d+)";
            Regex regex = new Regex(pattern);
            MatchCollection matches = regex.Matches(levelKey);
            string[] results = new string[2];
            foreach (Match match in matches)
            {
                results[0] = match.Groups[1].Value;
                results[1] = match.Groups[2].Value;
            }
            return results;
        }
        public static void LoadLevelsForCurrentMap(Level currentLevel)
        {
            targetSpawnPoints = new();
            //Find the next levels and add spawn points for the follow levels
            if (currentLevel == null)
            {
                return;
            }
            MapData map = currentLevel.Session.MapData;
            foreach (Tuple<string, string> levelKey in levelKeysInCompletionOrder)
            {
                string levelName = levelKey.Item2;
                LevelData levelData = map.Get(levelName);

                if (levelData != null)
                {
                    Vector2? spawnPoint = levelData.DefaultSpawn;
                    if (spawnPoint == null)
                    {
                        spawnPoint = levelData.Spawns.ClosestTo(new Vector2(levelData.Bounds.Left, levelData.Bounds.Bottom));
                        //CelesteBotManager.Log("No default spawn point found for level " + levelName + ", using " + spawnPoint.ToString() + " instead.");
                    }
                    CelesteBotManager.Log("Found level spawn point: " + spawnPoint.ToString() + "for level " + levelName);
                    targetSpawnPoints.Add(levelKey, (Vector2)spawnPoint);
                    //Vector2 bounds = new Vector2(levelData.Bounds.Left, levelData.Bounds.Bottom)
                    //levelData.Spawns.ClosestTo(bounds)

                }
            }
            CurrentMap = map.Filename;
        }
        public static Tuple<string, string> GetLevelKey(Level currentLevel)
        {
            if (currentLevel == null)
            {
                return null;
            }
            return Tuple.Create(currentLevel.Session.MapData.Filename, currentLevel.Session.Level);
        }


        public static Vector2 GetNextTarget(Level currentLevel)
        {
            if (currentLevel == null)
            {
                return Vector2.Zero;
            }
            if (targetSpawnPoints == null || currentLevel.Session.MapData.Filename != CurrentMap)
            {
                LoadLevelsForCurrentMap(currentLevel);
            }

            Tuple<string, string> levelKey = GetLevelKey(currentLevel);
            if (currentTargetLevel != null && levelKey == currentTargetLevel)
            {
                return currentTarget;
            }
            int indexOfCurrentLevel = levelKeysInCompletionOrder.IndexOf(levelKey);
            Tuple<string, string> nextLevelKey = levelKeysInCompletionOrder[indexOfCurrentLevel + 1];
            Vector2 nextTarget;
            if (nextLevelKey.Item1 != CurrentMap)
            {
                // this is the end of the chapter, so how do we get target? Estimate by getting bounds of level and comparing to respawn target. Choose next target by getting the opposite side of the level.
                CelesteBotManager.Log("Next level is not in the same map, so we are at the end of the chapter. Getting bounds of level and setting top right as target.", LogLevel.Info);
                LevelData levelData = currentLevel.Session.MapData.Get(levelKey.Item2);

                nextTarget = new Vector2(levelData.Bounds.Right, levelData.Bounds.Top);
            }
            else
            {
                nextTarget = targetSpawnPoints[nextLevelKey];

                if (nextTarget == currentTarget)
                {
                    CelesteBotManager.Log("Next target is the same as current target!", LogLevel.Warn);
                    throw new InvalidOperationException("Next target is the same as current target: " + nextTarget.ToString());
                    //nextTarget = targetSpawnPoints[levelKeysInCompletionOrder[indexOfCurrentLevel + 2]];
                }
            }
            CelesteBotManager.Log("Setting target to " + nextTarget.ToString() + " for level " + levelKey);
            currentTarget = nextTarget;
            currentTargetLevel = levelKey;
            return nextTarget;
            
        }
        public static double CurrentDistanceFromTarget(Player player)
        {
            if (player == null)
            {
                return 0;
            }
            return (player.BottomCenter - currentTarget).Length();
        }
        public static Vector2 CurrentVectorDistanceFromTarget(Player player)
        {
            if (player == null)
            {
                return Vector2.Zero;
            }
            return player.BottomCenter - currentTarget;
        }
        public static Vector2 GetCurrentTarget(Level currentLevel)
        {
            if (currentTarget == Vector2.Zero)
            {
                currentTarget = GetNextTarget(currentLevel);
            }
            return currentTarget;
        }
        
        public static bool CheckTargetReached(Player player)
        {
            // Returns true if target is reached

            // Updates the target based off of the current position
            if (currentTarget == Vector2.Zero || CurrentDistanceFromTarget(player) < CelesteBotManager.UPDATE_TARGET_THRESHOLD)
            {
                GetNextTarget(TileFinder.GetCelesteLevel());
                return true;
            }
            return false;
        }
    }
}
