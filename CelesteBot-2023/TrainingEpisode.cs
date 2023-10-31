using Celeste;
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
        int NumFrames { set; get; }
        public Vector2 Target { get; private set; }
        public bool Died { get;  set; }

        public bool FinishedLevel { get; set; }

        CelestePlayer player;
        string FitnessPath = @"fitnesses.fit";
        // Target Fitness and stuff
        public Dictionary<string, List<Vector2>> positionFitnesses;
        public Dictionary<string, List<Vector2>> velocityFitnesses;

        private List<Vector2>.Enumerator enumForFitness;
        private List<string>.Enumerator enumForLevels;
        private double LastDistanceFromTarget;
        readonly int FramesBetweenCalcuations;

        public TrainingEpisode(CelestePlayer player)
        {
            NumFrames = 0;
            this.player = player;
            Target = Vector2.Zero;
            positionFitnesses = Util.GetPositionFitnesses(FitnessPath);
            velocityFitnesses = Util.GetVelocityFitnesses(FitnessPath);
            FramesBetweenCalcuations = (int) (60 / CelesteBotInteropModule.Settings.CalculationsPerSecond);
        }

        public void IncrementFrames()
        {
            NumFrames++;
        }
        public void ResetEpisode()
        {
            NumFrames = 0;
            Died = false; 
            FinishedLevel = false;
        }

        public double GetReward()
        {
            if (player == null)
            {
                return 0;
            }
            double distanceFromTarget = (player.player.BottomCenter - Target).Length();
            //double reward = quasiFitness * 10;
            //if (player.LastDistanceFromTarget == 0)
            //{
            //    LastDistanceFromTarget = distanceFromTarget;
            //}            //if (player.LastDistanceFromTarget == 0)
            //{
            //    LastDistanceFromTarget = distanceFromTarget;
            //}
            double newDistance = distanceFromTarget - LastDistanceFromTarget;
            if (newDistance > 1000)
            {
                newDistance = 1000;
            }
            if (newDistance < -1000)
            {
                newDistance = -1000;
            }
            double reward = -1 * newDistance;
            if (NumFrames > 600)
            {
                reward -= NumFrames / 10;
            }
            LastDistanceFromTarget = newDistance;
            if (Died)
            {
                reward = -10000;
            }
            else if (reward < -750)
            {
                CelesteBotManager.Log("Killing player because it's reward is too low");

                player.KillPlayer();
            }
            if (FinishedLevel)
            {
                reward = 10000;
            } 
             
            CelesteBotManager.Log("Reward: " + reward.ToString("F2"));
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
                    enumForFitness = positionFitnesses[level.Session.MapData.Filename + "_" + level.Session.Level + "_" + "0"].GetEnumerator();
                    enumForLevels = Util.GetRawLevelsInOrder(FitnessPath).GetEnumerator();
                    enumForLevels.MoveNext(); // Should always be one ahead of the current level/fitness
                    enumForFitness.MoveNext();
                    Target = enumForFitness.Current;
                    CelesteBotManager.Log("Key: " + enumForLevels.Current + "==" + level.Session.MapData.Filename + "_" + level.Session.Level + "_" + "0" + " out: " + Target.ToString());
                }
                catch (KeyNotFoundException)
                {
                    // In a level that doesn't have a valid fitness enumerator
                    Target = new Vector2(10000, -10000);
                    CelesteBotManager.Log("Unknown Fitness Enumerator for: " + level.Session.MapData.Filename + "_" + level.Session.Level + "_" + "0");
                    CelesteBotManager.Log("With FitnessPath: " + enumForLevels);
                }
                ResetEpisode();
            }
            // Updates the target based off of the current position
            if ((player.player.BottomCenter - Target).Length() < CelesteBotManager.UPDATE_TARGET_THRESHOLD)
            {
                enumForFitness.MoveNext();
                //enumForLevels.MoveNext();
                if (enumForFitness.Current == Vector2.Zero)
                {
                    // We are at the end of the enumerator. Now is the tricky part: We need to move to the next fitness.
                    // We need to create an enumerator that we would use for the next level, but... how do we know the next level?
                    enumForLevels.MoveNext();
                    enumForFitness = positionFitnesses[enumForLevels.Current].GetEnumerator();
                    enumForFitness.MoveNext();
                }
                CelesteBotManager.Log("Updating Target Location: " + enumForFitness.Current);
                Target = enumForFitness.Current;
                // Use the enumerator to attempt to enumerate to next possible option. If it doesn't exist in this level (as in the enumerator is done) then use the next level's fitness
                FinishedLevel = true;
                //TargetsPassed++; // Increase the targets we have passed, which should give us a large boost in fitness
            }
        }

        internal bool IsCalculateFrame()
        {
            // We only calculate an action every so often
            return NumFrames % FramesBetweenCalcuations == 0;
        }
    }
}
