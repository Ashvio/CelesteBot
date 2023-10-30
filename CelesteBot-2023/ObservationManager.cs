using System.Collections.Concurrent;

namespace CelesteBot_2023
{
    public class GameState
    {
        public int[][] Vision { get; }
        public double Reward { get; }
        public bool DeathFlag { get; }
        public int[] Speed { get; }
        public float Stamina { get; }
        public float CanDash { get; }
        public GameState(int[][] vision, float speedX, float speedY, float stamina, bool canDash, double reward, bool deathFlag)
        {
            // Observation
            Vision = vision;
            Speed = new int[] { (int)speedX, (int)speedY };
            Stamina = CelesteBotManager.Normalize(stamina, -1, 120);
            CanDash = canDash ? 1 : 0;
            // Reward
            Reward = reward;
            DeathFlag = deathFlag;
            // Death

        }

    }
    public class ExternalGameStateManager
    {
        BlockingCollection<GameState> GameStateQueue;

        public ExternalGameStateManager()
        {
            GameStateQueue = new BlockingCollection<GameState>();
        }

        public void AddObservation(GameState obs)
        {
            GameStateQueue.Add(obs);
            CelesteBotManager.Log("Added Observation to queue");

        }

        public GameState PythonGetNextObservation()
        {
            return GameStateQueue.Take();
        }
    }
}
