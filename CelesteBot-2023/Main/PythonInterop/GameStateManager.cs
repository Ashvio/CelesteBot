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

        public GameState(CameraManager cameraManager, Player player, TrainingEpisodeState episode, bool playerDied, bool playerFinishedLevel)
        {
            // Observation
            Vision = cameraManager.CameraVision;
            Speed = new float[] { player.Speed.X, player.Speed.Y };
            
            Position = new float[] { cameraManager.PlayerTile.X, cameraManager.PlayerTile.Y };
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
            Target = new float[2] { cameraManager.TargetTile.X, cameraManager.TargetTile.Y };
        }
    }
    public class ExternalGameStateManager
    {
        readonly BlockingCollection<GameState> GameStateQueue;
        readonly BlockingCollection<double> RewardQueue;
        public static int NumSentObservations { get; set; }

        public ExternalGameStateManager()
        {
            GameStateQueue = new BlockingCollection<GameState>(40);
            RewardQueue = new BlockingCollection<double>(40);
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
