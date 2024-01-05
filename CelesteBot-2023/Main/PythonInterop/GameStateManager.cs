using Celeste;
using Celeste.Mod.CelesteBot_2023.Main;
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

        public GameState(PyList vision, Player player, TrainingEpisodeState episode, bool playerDied, bool playerFinishedLevel)
        {
            // Observation
            Vision = vision;
            Speed = new float[] { player.Speed.X, player.Speed.Y };
            Position = new float[] { player.X, player.Y };
            Stamina = Util.Normalize(player.Stamina, -1, 120);
            CanDash = player.CanDash ? 1 : 0;
            // Reward
            //Reward = reward;
            DeathFlag = playerDied;
            FinishedLevel = playerFinishedLevel;
            IsClimbing = episode.IsClimbing;
            if (!DeathFlag)
            {
                OnGround = player.OnGround();
            }
            else
            {
                OnGround = false;
            }
               ScreenPosition = CameraManager.CameraPosition;
            Target = new float[2] { episode.Target.X, episode.Target.Y };
        }

    }
    public class ExternalGameStateManager
    {
        readonly BlockingCollection<GameState> GameStateQueue;
        readonly BlockingCollection<double> RewardQueue;
        public static int NumSentObservations { get; set; }

        public ExternalGameStateManager()
        {
            GameStateQueue = new BlockingCollection<GameState>();
            RewardQueue = new BlockingCollection<double>();
        }

        public void AddObservation(GameState obs)
        {
            if (CelesteBotMain.Settings.TrainingEnabled)
            {
                GameStateQueue.Add(obs);
            }
        }

        public GameState PythonGetNextObservation()
        {
            NumSentObservations++;
            return GameStateQueue.Take();
        }
        public void AddReward(double reward)
        {
            if (CelesteBotMain.Settings.TrainingEnabled)
            {
                RewardQueue.Add(reward);
            }
        }
        public double PythonGetNextReward()
        {
            return RewardQueue.Take();
        }
    }
}
