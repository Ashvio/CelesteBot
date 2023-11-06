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
        public bool ReachedTarget { get; set; }
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
        public Vector2 LastVectorDistanceFromTarget;
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
            LastEpisodeReward = TotalReward != 0 ? TotalReward : LastEpisodeReward;
            TotalReward = 0;
            NumFrames = 0;
            ReachedTarget = false;

            
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
                LastDistanceFromTarget = TargetFinder.CurrentDistanceFromTarget(player.player);
                LastVectorDistanceFromTarget = TargetFinder.CurrentVectorDistanceFromTarget(player.player);
                firstFrame = false;
                return 0;
            }
            //double distanceFromTarget = TargetFinder.CurrentDistanceFromTarget(player.player);
            Vector2 currentVectorDistanceFromTarget = TargetFinder.CurrentVectorDistanceFromTarget(player.player);
            double distanceFromTarget = currentVectorDistanceFromTarget.Length();
            double rewardMultipler = 1;
            if (distanceFromTarget < ClosestDistanceFromTargetReached)
            {
                // reward getting further than ever before!
                ClosestDistanceFromTargetReached = distanceFromTarget;
                rewardMultipler = 2.5;
            }
            double changeInDistance =   LastDistanceFromTarget - distanceFromTarget;
            Vector2 changeInVectorDistance = LastVectorDistanceFromTarget - currentVectorDistanceFromTarget;
            double distanceChangeReward = Math.Max(changeInVectorDistance.X, changeInVectorDistance.Y);
            double reward = Math.Max(distanceChangeReward, changeInDistance);
            changeInDistance *= -1;
            if (changeInDistance < 0)
            {
                // dont penalize backtracking as much as forward progress, sometimes it's necessary!
                changeInDistance *= 0.35;

            }
            LastDistanceFromTarget = distanceFromTarget;
            LastVectorDistanceFromTarget = currentVectorDistanceFromTarget;

            double reward = changeInDistance;
            if (NumFrames > 60 * 60 * 5) // 5 minutes to try to get to target
            {
                CelesteBotManager.Log("Took too long to get to target, reducing reward", LogLevel.Warn);
                reward -= 3000;
            }

            if (player.player.Dead)
            {
                // reward is correlated to percentage of the level completed
                reward = ((OriginalDistanceFromTarget - distanceFromTarget * 2) / OriginalDistanceFromTarget) * 200;
                if (OriginalDistanceFromTarget == 0)
                {
                    throw new DivideByZeroException("OriginalDistanceFromTarget is 0!");
                }
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
                CelesteBotInteropModule.PlayerDied = true;
                CelesteBotInteropModule.NeedImmediateGameStateUpdate = true;
                ResetEpisode();
            }
            else
            {
                reward *= rewardMultipler;
            }
            if (ReachedTarget)
            {
                reward += 1000;
            }
            if (NumFrames % 80 == 0 || Dead)
            {
                //CelesteBotManager.Log("Reward: " + reward.ToString("F2"));
            }
            if (reward == 0)
            {
                int re = 0;
            }
            // normalize reward on a per-frame basis
            reward = reward / FramesBetweenCalculations;

            TotalReward += reward;
            LastActionReward = reward; 
            return reward;
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

        internal void NewEpisodeSetOriginalDistance()
        {
            OriginalDistanceFromTarget = TargetFinder.CurrentDistanceFromTarget(player.player);
            CelesteBotManager.Log("Resetting episode, new OriginalDistanceFromTarget: " + OriginalDistanceFromTarget.ToString("F2"));
            ClosestDistanceFromTargetReached = OriginalDistanceFromTarget;
        }
    }
}
