using Celeste;
using Celeste.Mod.CelesteBot_2023;
using Microsoft.Xna.Framework;
using Python.Runtime;
using System.Collections.Concurrent;

namespace CelesteBot_2023
{
    public class GameState
    {
        public PyList Vision { get; }
        public bool DeathFlag { get; }
        public bool FinishedLevel { get; }
        public bool IsClimbing { get; }
        public object OnGround { get; private set; }
        public float[] Target { get; private set; }
        public float[] Position { get; private set; }
        public float[] Speed { get; }
        public double Stamina { get; }
        public float CanDash { get; }
        public float[] ScreenPosition { get; internal set; }

        public GameState(PyList vision, Player player, TrainingEpisode episode)
        {
            // Observation
            Vision = vision;
            Speed = new float[] { player.Speed.X, player.Speed.Y };
            Position = new float[] { player.X, player.Y };
            Stamina = Util.Normalize(player.Stamina, -1, 120);
            CanDash = player.CanDash ? 1 : 0;
            // Reward
            //Reward = reward;
            DeathFlag = episode.Died;
            FinishedLevel = episode.FinishedLevel;
            IsClimbing = episode.IsClimbing;
            OnGround = player.OnGround(); 
            ScreenPosition = CameraManager.CameraPosition;
            Target = new float[2] { episode.Target.X, episode.Target.Y };
        }

    }
    public class ExternalGameStateManager
    {
        BlockingCollection<GameState> GameStateQueue;
        BlockingCollection<double> RewardQueue;
        public int NumSentObservations { get; set; }

        public ExternalGameStateManager()
        {
            GameStateQueue = new BlockingCollection<GameState>();
            RewardQueue = new BlockingCollection<double>();
        }

        public void AddObservation(GameState obs)
        {
            GameStateQueue.Add(obs);
        }

        public GameState PythonGetNextObservation()
        {
            NumSentObservations++;
            return GameStateQueue.Take();
        }
        public void AddReward(double reward)
        {

            RewardQueue.Add(reward);
        }
        public double PythonGetNextReward()
        {
            return RewardQueue.Take();
        }
    }
}
