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
    public class TrainingEpisode
    {
        public int NumFrames { private set; get; }
        public Vector2 Target { get; private set; }
        public bool Died { get; set; }

        public bool FinishedLevel { get; set; }
        public static double LastEpisodeReward { get; private set; }
        public double TotalReward { get; private set; }

        CelestePlayer player;
        string FitnessPath = @"fitnesses.fit";
        // Target Fitness and stuff
        public Dictionary<string, List<Vector2>> positionFitnesses;
        public Dictionary<string, List<Vector2>> velocityFitnesses;

        private List<Vector2>.Enumerator targetList;
        private List<string>.Enumerator levelEnumerator;
        public double LastDistanceFromTarget { get; private set;}
        public bool IsClimbing { get; internal set; }
        private int framesUntilNextCalculation;
        public int FramesUntilNextCalculation
        {
            get => framesUntilNextCalculation;
            internal set
            {
                NumFrames = 0;
                framesUntilNextCalculation = value;
            } 
        }

        private bool firstFrame = true;
        private double ClosestDistanceFromTargetReached;
        private double OriginalDistanceFromTarget;
        private bool waitForReset = false;
        public TrainingEpisode(CelestePlayer player)
        {
            NumFrames = 0;
            this.player = player;
            Target = Vector2.Zero;
            positionFitnesses = Util.GetPositionFitnesses(FitnessPath);
            velocityFitnesses = Util.GetVelocityFitnesses(FitnessPath);
            FramesUntilNextCalculation = CelesteBotInteropModule.Settings.CalculationsPerSecond;

        }

        public void IncrementFrames()
        {
            NumFrames++;
        }
        public void ResetEpisode()
        {
            //Should only be called when player exists
            waitForReset = false;
            firstFrame = true;
            LastEpisodeReward = TotalReward;
            TotalReward = 0;
            NumFrames = 0;
            Died = false;
            FinishedLevel = false;
            OriginalDistanceFromTarget = CurrentDistanceFromTarget();
            ClosestDistanceFromTargetReached = OriginalDistanceFromTarget;
            FramesUntilNextCalculation = CelesteBotInteropModule.Settings.CalculationsPerSecond;
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
                return 0;
            }
            if (firstFrame)
            {
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
            double newDistance = distanceFromTarget - LastDistanceFromTarget;
            newDistance *= -1;
            LastDistanceFromTarget = distanceFromTarget;

            if (newDistance > 3000)
            {
                newDistance = 3000;
            }
            if (newDistance < -3000)
            {
                newDistance = -3000;
            }
            double reward = newDistance;
            if (NumFrames > 60 * 60 * 5) // 5 minutes to try to get to target
            {
                reward -= 3000;
            }

            
            //if (player.player.Dead && !FinishedLevel)
            //{
            //    waitForReset = true;
            //    reward = OriginalDistanceFromTarget - distanceFromTarget * 2 ;
            //    CelesteBotManager.Log("Died, reward: " + reward.ToString("F2")
            //        + "Original distance from target: " + OriginalDistanceFromTarget.ToString("F2") 
            //        + " Closest distance from target: " + ClosestDistanceFromTargetReached.ToString("F2"), LogLevel.Info);
            //}
            else if (reward < -3000)
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
            if (NumFrames % 80 == 0 || Died)
            {
                CelesteBotManager.Log("Reward: " + reward.ToString("F2"));
            }
            TotalReward += reward;
            return reward;
        }
        public void UpdateTarget()
        {
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
                ResetEpisode();
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
                FinishedLevel = true;
                //TargetsPassed++; // Increase the targets we have passed, which should give us a large boost in fitness
            }
        }

        internal bool IsCalculateFrame()
        {
            // We only calculate an action every so often, based on what the bot wants
            return NumFrames >= FramesUntilNextCalculation;
        }

        internal bool IsRewardFrame()
        {
            // Calculate reward 2 frames before we calculate the next action
            if (!CelesteBotInteropModule.Settings.TrainingEnabled)
            {
                return NumFrames % 8 == 0;
            }
            return NumFrames == FramesUntilNextCalculation - 2;
        }
    }
}
