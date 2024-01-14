using Celeste.Mod;
using CelesteBot_2023;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteBot_2023.Main
{
    public class TrainingEpisodeState
    {
        public int NumFrames { private set; get; }
        //public Vector2 Target { get; private set; }
        public bool Dead
        {
            get
            {
                return player.CelesteGamePlayer?.Dead ?? false;
            }
        }
        public bool ReachedTarget { get; set; }
        public static double LastEpisodeReward { get; private set; }
        public double LastActionReward { get; private set; }

        public double TotalReward { get; private set; }

        readonly CelesteBotRunner player;
        readonly string FitnessPath = @"fitnesses.fit";
        // Target Fitness and stuff
        public Dictionary<string, List<Vector2>> positionFitnesses;
        public Dictionary<string, List<Vector2>> velocityFitnesses;

        public double LastDistanceFromTarget { get; private set; }
        public Vector2 LastVectorDistanceFromTarget;
        public bool IsClimbing { get; internal set; }
        private int FramesBetweenCalculations;
        public bool FirstObservationSent { get; internal set; }
        public Vector2 Target { get => TargetFinder.CurrentTarget; }

        public int FinishedLevelCount { get; internal set; }
        public string LastLevelName { get; private set; }
        public string CurrentLevelName { get => TargetFinder.CelesteSession.Level; }
        private bool firstFrame;
        private double ClosestDistanceFromTargetReached;
        private Vector2 ClosestVectorDistanceFromTargetReached;
        public int FramesSinceMadeProgress { get; private set; }
        public static double OriginalDistanceFromTarget;
        private Vector2 OriginalVectorDistanceFromTarget;
        private bool waitForReset = false;
        public TrainingEpisodeState(CelesteBotRunner player)
        {
            FirstObservationSent = false;
            NumFrames = 0;
            this.player = player;
            firstFrame = true;
            positionFitnesses = Util.GetPositionFitnesses(FitnessPath);
            velocityFitnesses = Util.GetVelocityFitnesses(FitnessPath);
            FramesBetweenCalculations = 60 / CelesteBotMain.Settings.CalculationsPerSecond;
            FinishedLevelCount = 0;
        }

        public void IncrementFrames()
        {
            NumFrames++;
        }
        public void ResetEpisode()
        {
            //Should only be called when player exists
            CelesteBotMain.Log("Resetting episode, total reward: " + TotalReward, LogLevel.Info);
            FirstObservationSent = false;
            waitForReset = false;
            firstFrame = true;
            LastEpisodeReward = TotalReward != 0 ? TotalReward : LastEpisodeReward;
            TotalReward = 0;
            NumFrames = 0;
            ReachedTarget = false;
            FramesSinceMadeProgress = 0;
            FinishedLevelCount = TargetFinder.FinishedLevelCount;
            LastLevelName = CurrentLevelName;
        }

        public bool FinishedLevel
        {
            get
            {
                return TargetFinder.FinishedLevelCount > FinishedLevelCount && TargetFinder.FinishedLevelCount > 1 && TargetFinder.DoingBacktrackedLevels == false;
            }
        }
        public double GetReward()
        {
            if (waitForReset)
            {
                CelesteBotMain.Log("Waiting for reset, no reward given", LogLevel.Info);
                return 0;
            }

            if (player == null)
            {
                CelesteBotMain.Log("Player is null, no reward given", LogLevel.Info);
                return 0;
            }
            if (firstFrame)
            {
                CelesteBotMain.Log("First action", LogLevel.Info);
                LastDistanceFromTarget = TargetFinder.CurrentDistanceFromTarget(player.CelesteGamePlayer);
                LastVectorDistanceFromTarget = TargetFinder.CurrentVectorDistanceFromTarget(player.CelesteGamePlayer);
                firstFrame = false;
                return 0;
            }
            //double distanceFromTarget = TargetFinder.CurrentDistanceFromTarget(player.player);
            Vector2 currentVectorDistanceFromTarget = TargetFinder.CurrentVectorDistanceFromTarget(player.CelesteGamePlayer);
            double distanceFromTarget = currentVectorDistanceFromTarget.Length();
            double changeInDistance = LastDistanceFromTarget - distanceFromTarget;
            Vector2 changeInVectorDistance = LastVectorDistanceFromTarget - currentVectorDistanceFromTarget;
            //reward going in the right direction in at least on axis
            double distanceChangeReward = Math.Max(-changeInVectorDistance.X, changeInVectorDistance.Y) + Math.Min(-changeInVectorDistance.X, changeInVectorDistance.Y);
            //double reward = Math.Max(distanceChangeReward, changeInDistance);
            double reward = changeInDistance;
            double rewardMultipler = 1;

            if (distanceFromTarget < ClosestDistanceFromTargetReached)
            {
                // reward getting further than ever before!
                ClosestDistanceFromTargetReached = distanceFromTarget;
                ClosestVectorDistanceFromTargetReached = currentVectorDistanceFromTarget;

                FramesSinceMadeProgress = 0;
                rewardMultipler = 4;
            }
            else
            {
                FramesSinceMadeProgress += FramesBetweenCalculations;
            }

            //CelesteBotManager.Log("changeInVectorDistanceX: " + (-changeInVectorDistance.X).ToString("F2") + "changeInVectorDistanceY: " + changeInVectorDistance.Y.ToString("F2") + " changeInDistance: " + changeInDistance.ToString() + " reward: " + reward.ToString("F2") + " distanceChangeReward: " + distanceChangeReward.ToString("F2") + " distanceFromTarget: " + distanceFromTarget.ToString("F2"));
            //if (reward < 0)
            //{
            //    // dont penalize backtracking as much as forward progress, sometimes it's necessary!
            //    rewardMultipler *= 0.35;

            //}
            LastDistanceFromTarget = distanceFromTarget;
            LastVectorDistanceFromTarget = currentVectorDistanceFromTarget;
            //if (NumFrames > 60 * 30 && reward < 0)
            //{
            //    // time penalty
            //    rewardMultipler *= 1.5;
            //}

            if (player.CelesteGamePlayer.Dead)
            {
                // reward is correlated to percentage of the level completed
                //changeInVectorDistance = OriginalVectorDistanceFromTarget - currentVectorDistanceFromTarget * 3.5;
                //double finalDistanceChangeReward = Math.Max(-changeInVectorDistance.X, changeInVectorDistance.Y) + Math.Min(-changeInVectorDistance.X, changeInVectorDistance.Y);

                reward = 5 * FramesBetweenCalculations * ((OriginalDistanceFromTarget - distanceFromTarget * 1.2) / OriginalDistanceFromTarget);
                if (OriginalDistanceFromTarget == 0)
                {
                    throw new DivideByZeroException("OriginalDistanceFromTarget is 0!");
                }
                CelesteBotMain.Log("Died, reward: " + reward.ToString("F2")
                                       + "Original distance from target: " + OriginalDistanceFromTarget.ToString("F2")
                                                          + " Closest distance from target: " + ClosestDistanceFromTargetReached.ToString("F2"), LogLevel.Info);
                waitForReset = true;
            }

            Func<bool, bool> setLevelFinished = (bool finished) =>
            {
                player.PlayerFinishedLevel = finished;
                player.NeedImmediateGameStateUpdate = true;
                LastLevelName = CurrentLevelName;
                return true;
            };

            if (FinishedLevel)
            {
                // new level reached!
                reward += 150 * FramesBetweenCalculations; // denormalized flat reward for beating level
                setLevelFinished(true);
            } else if (!TargetFinder.MovingForwardBacktrackedLevels && (CurrentLevelName != LastLevelName && CurrentLevelName != TargetFinder.CelesteSession.FurthestSeenLevel))
            {
                CelesteBotMain.Log("Backtracking, punishing", LogLevel.Info);
                reward -= 75 * FramesBetweenCalculations; // denormalized flat punishment for backtracking levels
                setLevelFinished(false);
            } else if (CurrentLevelName != LastLevelName && TargetFinder.MovingForwardBacktrackedLevels)
            {
                CelesteBotMain.Log("Unbacktracking, rewarding", LogLevel.Info);

                reward += 50 * FramesBetweenCalculations; // denormalized flat reward for un-backtracking
                setLevelFinished(true);
            }
            // Punish if backtracking to level it shouldnt be on.


            //if (player.player.Dead && !FinishedLevel)
            //{
            //    waitForReset = true;
            //    reward = OriginalDistanceFromTarget - distanceFromTarget * 2 ;
            //    CelesteBotManager.Log("Died, reward: " + reward.ToString("F2")
            //        + "Original distance from target: " + OriginalDistanceFromTarget.ToString("F2") 
            //        + " Closest distance from target: " + ClosestDistanceFromTargetReached.ToString("F2"), LogLevel.Info);
            //}
            //if (FramesSinceMadeProgress > 60 * 6) // 15 seconds of no progress 
            //{
            //    //CelesteBotManager.Log("Took too long to get to target, reducing reward", LogLevel.Warn);
            //    reward -= (FramesSinceMadeProgress - 60 * 6) / (60);
            //}

            else if (FramesSinceMadeProgress > 60 * 10)
            {
                CelesteBotMain.Log("Killing player because it timed out");
                reward = -75 * FramesBetweenCalculations;
                player.KillPlayer();
                player.PlayerDied = true;
                player.NeedImmediateGameStateUpdate = true;
            }
            else
            {
                reward *= rewardMultipler;
            }
            //if (TargetFinder.RedeemTargetReward())
            //{
            //    reward += 1000;
            //    ReachedTarget = false;
            //    NewEpisodeSetOriginalDistance();
            //    FramesSinceMadeProgress = 0;
            //}

            // normalize reward on a per-frame basis
            reward /= FramesBetweenCalculations;

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
            return NumFrames % FramesBetweenCalculations == FramesBetweenCalculations - 2;

        }

        internal void NewEpisodeSetOriginalDistance()
        {
            OriginalDistanceFromTarget = TargetFinder.CurrentDistanceFromTarget(player.CelesteGamePlayer);
            OriginalVectorDistanceFromTarget = TargetFinder.CurrentVectorDistanceFromTarget(player.CelesteGamePlayer);

            CelesteBotMain.Log("Resetting episode, new OriginalDistanceFromTarget: " + OriginalDistanceFromTarget.ToString("F2"));
            ClosestDistanceFromTargetReached = OriginalDistanceFromTarget;
            ClosestVectorDistanceFromTargetReached = OriginalVectorDistanceFromTarget;

        }
    }
}
