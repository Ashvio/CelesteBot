using Celeste;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Monocle;
using System.Text.RegularExpressions;
using System.IO;
using Celeste.Mod;
using CelesteBot_2023.SimplifiedGraphics;
using static CelesteBot_2023.CelesteBotMain;

namespace CelesteBot_2023
{

    public class TargetFinder
    {
        const string LEVEL_ORDER_PATH = @"levels.csv";

        public static Vector2 CurrentTarget { get => currentTarget; }
        private static List<Tuple<string, string>> levelKeysInCompletionOrder;
        private static Dictionary<Tuple<string, string>, Vector2> targetSpawnPoints;
        private static Tuple<string, string> currentTargetLevel;
        private static Vector2 currentTarget;
        private static string targetedLevel = "";
        private static HashSet<string> seenLevels;
        private static Stack<string> backTrackedLevels;
        public static string CurrentMap { get; private set; }
        public static string FurthestSeenLevel { get; private set; }
        public static bool TargetReachedRewardWaiting { get; private set; }
        private static Session session { get => SceneExtensions.GetSession(Engine.Scene); }
        private static Player player { get => SceneExtensions.GetPlayer(Engine.Scene); }
        
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
            seenLevels = new();
            backTrackedLevels = new();
        }
        public static string[] levelFilenameAndNameFromKey(string levelKey)
        {
            string pattern = @"(^[\d+\-\p{Lu}\p{Ll}]+)_(\d+)";
            Regex regex = new(pattern);
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
                    CutsceneManager.Log("Found level spawn point: " + spawnPoint.ToString() + "for level " + levelName);
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
        //public static Vector2 GetEstimatedTarget(Player player)
        //{
        //    // find gaps on the walls opposing the player and return the furthest one
        //    Level currentLevel = SceneExtensions.GetLevel(Engine.Scene);
        //    Rectangle bounds = currentLevel.Bounds;
        //    currentTarget = GetOppositeSideOfBounds(player, bounds);
        //    lastLevel = currentLevel;
        //    return currentTarget;
        //}

        private static Vector2 GetClosestLightBeamTarget()
        {
            List<Vector2> lightBeams = GetLightBeamTargets();
            //find closest lightbeam from player
            Vector2 closestLightBeam = Vector2.Zero;
            double closestDistance = double.MaxValue;
            foreach (Vector2 lightBeam in lightBeams)
            {
                double distance = Vector2.Distance(player.Position, lightBeam);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLightBeam = lightBeam;
                }
            }
            return closestLightBeam;

        }
        public static Vector2 GetFurthestLightBeamTarget()
        {
            List<Vector2> lightBeams = GetLightBeamTargets();
            //find urthest lightbeam from player
            Vector2 furthestLightBeam = Vector2.Zero;
            double furthestDistance = 0;
            foreach (Vector2 lightBeam in lightBeams)
            {
                double distance = Vector2.Distance(player.Position, lightBeam);
                if (distance > furthestDistance)
                {
                    furthestDistance = distance;
                    furthestLightBeam = lightBeam;
                }
            }
            return furthestLightBeam;
        }
        public static Vector2 LevelToWorldCoord(Vector2 levelCoord, Level level)
        {
            return new Vector2(level.Bounds.Left + levelCoord.X, level.Bounds.Top + levelCoord.Y);
        }
        public static List<Vector2> GetLightBeamTargets()
        {
            // Randomizer mod adds lightbeams to each potential level exit, so we can use those as targets
            LevelData currentLevelData = session?.MapData?.Get(session?.Level);
            if (currentLevelData == null)
            {
                return new();
            }
            Level currentLevel = SceneExtensions.GetLevel(Engine.Scene);
            List<Vector2> lightBeams = new();
            foreach (EntityData entityData in currentLevelData.Entities)
            {
                if (entityData.Name == "lightbeam")
                {
                    Vector2 worldPosition = LevelToWorldCoord(entityData.Position, currentLevel);
                    lightBeams.Add(worldPosition);
                }
            }
            return lightBeams;
        }
        public static Vector2 GetOppositeSideOfBounds(Player player, Rectangle bounds)
        {
            Vector2 oppositeSide = new();
            // return two vectors, one for each coordinate of each bound of the opposite side

            if (player.Position.X < bounds.Center.X)
            {
                oppositeSide.X = bounds.Right;
            }
            else
            {
                oppositeSide.X = bounds.Left;
            }

            if (player.Position.Y < bounds.Center.Y)
            {
                oppositeSide.Y = bounds.Bottom;
            }
            else
            {
                oppositeSide.Y = bounds.Top;
            }

            return oppositeSide;
        }

        public static Vector2 GetNextTarget(Level currentLevel)
        {
            // todo: find all levels bordering the current level, and return their coordinates as a list of potential targets
            // base target will be the end of the last level of the map
            if (currentLevel == null)
            {
                return Vector2.Zero;
            }
            Session currentSession = SceneExtensions.GetSession(Engine.Scene);

            if (FurthestSeenLevel == "")
            {
                FurthestSeenLevel = currentSession.FurthestSeenLevel;
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
                CutsceneManager.Log("Next level is not in the same map, so we are at the end of the chapter. Getting bounds of level and setting top right as target.", LogLevel.Info);
                LevelData levelData = currentLevel.Session.MapData.Get(levelKey.Item2);

                nextTarget = new Vector2(levelData.Bounds.Right, levelData.Bounds.Top);
            }
            else
            {
                nextTarget = targetSpawnPoints[nextLevelKey];

                if (nextTarget == currentTarget)
                {
                    CutsceneManager.Log("Next target is the same as current target!", LogLevel.Warn);
                    throw new InvalidOperationException("Next target is the same as current target: " + nextTarget.ToString());
                    //nextTarget = targetSpawnPoints[levelKeysInCompletionOrder[indexOfCurrentLevel + 2]];
                }
            }
            CutsceneManager.Log("Setting target to " + nextTarget.ToString() + " for level " + levelKey);
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
            return Vector2.Distance(player.Position, currentTarget);
        }
        public static Vector2 CurrentVectorDistanceFromTarget(Player player)
        {
            if (player == null)
            {
                return Vector2.Zero;
            }
            return player.Position - currentTarget;
        }
        public static Vector2 UpdateCurrentTarget()
        {
            string currentLevel = session.Level;
            bool isFirstLevel = currentTarget == Vector2.Zero;
            bool levelIsDifferent = currentLevel != targetedLevel;
            bool seenLevelBefore = seenLevels.Contains(currentLevel);
            string oldTargetedLevel = targetedLevel;
            bool backtracking = false;
            if (levelIsDifferent)
            {
                bool doingBacktrackedLevels = backTrackedLevels.Count > 0 && backTrackedLevels.Peek() == currentLevel;
                if (seenLevelBefore && doingBacktrackedLevels)
                {
                    backTrackedLevels.Pop();
                    backtracking = true;
                }
                else if (seenLevelBefore && !doingBacktrackedLevels)
                {
                    backTrackedLevels.Push(targetedLevel);
                }

                targetedLevel = currentLevel;
            }
            bool isFurthestLevel = backTrackedLevels.Count == 0;

            if (levelIsDifferent && !isFurthestLevel && !backtracking)
            {
                // need to backtrack! l1=>l2=>l1
                CutsceneManager.Log("Need to backtrack: " + oldTargetedLevel + " " + currentLevel, LogLevel.Info);
                currentTarget = GetClosestLightBeamTarget();
            }

            else if (isFirstLevel || backtracking || (levelIsDifferent && isFurthestLevel))
            {
                // need to go forward!
                CutsceneManager.Log("Need to go forward: " + oldTargetedLevel + " " + currentLevel, LogLevel.Info);
                if (targetedLevel != "" && !seenLevelBefore)
                {
                    TargetReachedRewardWaiting = true;
                }
                seenLevels.Add(currentLevel);
                currentTarget = GetFurthestLightBeamTarget();
            }
            //if (currentTarget == Vector2.Zero)
            //{
            //    Player player = SceneExtensions.GetPlayer(Engine.Scene);

            //    //currentTarget = GetEstimatedTarget(player);
            //    //currentTarget = GetNextTarget(currentLevel);
            //}
            return currentTarget;
        }

        public static bool RedeemTargetReward()
        {
            //cashes out a reward waiting for the agent
            if (TargetReachedRewardWaiting)
            {
                CutsceneManager.Log("Redeeming target reward", LogLevel.Info);
                TargetReachedRewardWaiting = false;
                return true;
            }
            return false;
        }

        public static bool CheckTargetReached(Player player)
        {
            // Returns true if target is reached

            // Updates the target based off of the current position
            //furthestseenlevel != currentsession.furthestseenlevel
            // Returns true if target is reached
            UpdateCurrentTarget();

            return false;
            //if (currentTarget == Vector2.Zero)
            //{
            //    GetNextTarget(TileFinder.GetCelesteLevel());
            //    return true;
            //}
            //return false;
        }
        public static Vector2 GetOppositeSideXY(Player player, Rectangle levelBounds)
        {

            Vector2 oppositeSide = new();
            // return two vectors, one for each coordinate of each bound of the opposite side

            if (player.Position.X < levelBounds.Center.X)
            {
                oppositeSide.X = levelBounds.Right;
            }
            else
            {
                oppositeSide.X = levelBounds.Left;
            }

            if (player.Position.Y < levelBounds.Center.Y)
            {
                oppositeSide.Y = levelBounds.Bottom;
            }
            else
            {
                oppositeSide.Y = levelBounds.Top;
            }

            return oppositeSide;
        }

    }
}
