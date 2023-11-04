using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CelesteBot_2023
{
    public class TrainingEpisodeState
    {
        public int NumFrames { private set; get; }
        public Vector2 Target { get; private set; }
        public bool Dead
        {
            get
            {
               return player.player?.Dead ?? false;
            }
        }
        public bool FinishedLevel { get; set; }
        public static double LastEpisodeReward { get; private set; }
        public double LastActionReward { get; private set; }

        public double TotalReward { get; private set; }

        AIPlayerLoop player;
        string FitnessPath = @"fitnesses.fit";
        // Target Fitness and stuff
        public Dictionary<string, List<Vector2>> positionFitnesses;
        public Dictionary<string, List<Vector2>> velocityFitnesses;

        private List<Vector2>.Enumerator targetList;
        private List<string>.Enumerator levelEnumerator;
        public double LastDistanceFromTarget { get; private set;}
        public bool IsClimbing { get; internal set; }
        readonly int FramesBetweenCalculations;
        public bool FirstObservationSent { get; internal set; }



        private bool firstFrame;
        private double ClosestDistanceFromTargetReached;
        private double OriginalDistanceFromTarget;
        private bool waitForReset = false;
        public TrainingEpisodeState(AIPlayerLoop player)
        {
            FirstObservationSent = false;
            NumFrames = 0;
            this.player = player;
            firstFrame = true;
            Target = Vector2.Zero;
            positionFitnesses = Util.GetPositionFitnesses(FitnessPath);
            velocityFitnesses = Util.GetVelocityFitnesses(FitnessPath);
            FramesBetweenCalculations = (60 / CelesteBotInteropModule.Settings.CalculationsPerSecond);
        }

        public void IncrementFrames()
        {
            NumFrames++;
        }
        public void ResetEpisode()
        {
            //Should only be called when player exists
            FirstObservationSent = false;
            waitForReset = false;
            firstFrame = true;
            LastEpisodeReward = TotalReward;
            TotalReward = 0;
            NumFrames = 0;
            FinishedLevel = false;
            OriginalDistanceFromTarget = CurrentDistanceFromTarget();
            CelesteBotManager.Log("Resetting episode, new OriginalDistanceFromTarget: " + OriginalDistanceFromTarget.ToString("F2"));
            ClosestDistanceFromTargetReached = OriginalDistanceFromTarget;
            
        }
        public double CurrentDistanceFromTarget()
        {
            if (player.player == null)
            {
                return 0;
            }
            return (player.player.BottomCenter - Target).Length();
        }
        public double GetReward()
        {
            if (waitForReset)
            {
                CelesteBotManager.Log("Waiting for reset, no reward given", LogLevel.Info);
                return 0;
            }

            if (player == null)
            {
                CelesteBotManager.Log("Player is null, no reward given", LogLevel.Info);
                return 0;
            }
            if (firstFrame)
            {
                CelesteBotManager.Log("First action", LogLevel.Info);
                LastDistanceFromTarget = CurrentDistanceFromTarget();
                firstFrame = false;
                return 0;
            }
            double distanceFromTarget = CurrentDistanceFromTarget();
            int rewardMultipler = 1;
            if (distanceFromTarget < ClosestDistanceFromTargetReached)
            {
                // reward getting further than ever before!
                ClosestDistanceFromTargetReached = distanceFromTarget;
                rewardMultipler = 2;
            }
            double changeInDistance = distanceFromTarget - LastDistanceFromTarget;
            changeInDistance *= -1;
            LastDistanceFromTarget = distanceFromTarget;

            if (changeInDistance > 3000)
            {
                changeInDistance = 3000;
            }
            if (changeInDistance < -3000)
            {
                changeInDistance = -3000;
            }
            double reward = changeInDistance;
            if (NumFrames > 60 * 60 * 5) // 5 minutes to try to get to target
            {
                CelesteBotManager.Log("Took too long to get to target, reducing reward", LogLevel.Warn);
                reward -= 3000;
            }

            if (player.player.Dead)
            {
                reward = OriginalDistanceFromTarget - distanceFromTarget * 1.5;
                CelesteBotManager.Log("Died, reward: " + reward.ToString("F2")
                                       + "Original distance from target: " + OriginalDistanceFromTarget.ToString("F2")
                                                          + " Closest distance from target: " + ClosestDistanceFromTargetReached.ToString("F2"), LogLevel.Info);
                waitForReset = true;
            }
            
            //if (player.player.Dead && !FinishedLevel)
            //{
            //    waitForReset = true;
            //    reward = OriginalDistanceFromTarget - distanceFromTarget * 2 ;
            //    CelesteBotManager.Log("Died, reward: " + reward.ToString("F2")
            //        + "Original distance from target: " + OriginalDistanceFromTarget.ToString("F2") 
            //        + " Closest distance from target: " + ClosestDistanceFromTargetReached.ToString("F2"), LogLevel.Info);
            //}
            else if (reward <= -3000)
            {
                CelesteBotManager.Log("Killing player because its reward is too low");

                player.KillPlayer();
            }
            else
            {
                reward *= rewardMultipler;
            }
            if (FinishedLevel)
            {
                reward += 5000;
            }
            if (NumFrames % 80 == 0 || Dead)
            {
                CelesteBotManager.Log("Reward: " + reward.ToString("F2"));
            }
            if (reward == 0)
            {
                int re = 0;
            }
            TotalReward += reward;
            LastActionReward = reward; 
            return reward;
        }
        public bool UpdateTarget()
        {
            // Returns true if target is reached
            if (Target == Vector2.Zero)
            {

                // Enum does not exist yet, lets make it.
                Level level = TileFinder.GetCelesteLevel();
                try
                {
                    targetList = positionFitnesses[level.Session.MapData.Filename + "_" + level.Session.Level + "_" + "0"].GetEnumerator();
                    levelEnumerator = Util.GetRawLevelsInOrder(FitnessPath).GetEnumerator();
                    levelEnumerator.MoveNext(); // Should always be one ahead of the current level/fitness
                    targetList.MoveNext();
                    Target = targetList.Current;
                    CelesteBotManager.Log("Key: " + levelEnumerator.Current + "==" + level.Session.MapData.Filename + "_" + level.Session.Level + "_" + "0" + " out: " + Target.ToString());
                }
                catch (KeyNotFoundException)
                {
                    // In a level that doesn't have a valid fitness enumerator
                    Target = new Vector2(10000, -10000);
                    CelesteBotManager.Log("Unknown Fitness Enumerator for: " + level.Session.MapData.Filename + "_" + level.Session.Level + "_" + "0");
                    CelesteBotManager.Log("With FitnessPath: " + levelEnumerator);
                }
                return false;
            }

            // Updates the target based off of the current position
            if ((player.player.BottomCenter - Target).Length() < CelesteBotManager.UPDATE_TARGET_THRESHOLD)
            {
                targetList.MoveNext();
                //enumForLevels.MoveNext();
                if (targetList.Current == Vector2.Zero)
                {
                    // We are at the end of the enumerator. Now is the tricky part: We need to move to the next fitness.
                    // We need to create an enumerator that we would use for the next level, but... how do we know the next level?
                    levelEnumerator.MoveNext();
                    targetList = positionFitnesses[levelEnumerator.Current].GetEnumerator();
                    targetList.MoveNext();
                }
                CelesteBotManager.Log("Updating Target Location: " + targetList.Current);
                Target = targetList.Current;
                // Use the enumerator to attempt to enumerate to next possible option. If it doesn't exist in this level (as in the enumerator is done) then use the next level's fitness
                //TargetsPassed++; // Increase the targets we have passed, which should give us a large boost in fitness
                return true;
            }
            return false;
        }

        internal bool IsCalculateFrame()
        {
            // We only calculate an action every so often
            return NumFrames > 0 && NumFrames % FramesBetweenCalculations == 0;
        }


        internal bool IsRewardFrame()
        {
            // Calculate reward 2 frames before we calculate the next action
            return NumFrames % FramesBetweenCalculations  == FramesBetweenCalculations - 2;

        }
    }
}
