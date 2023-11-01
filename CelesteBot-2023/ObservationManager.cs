using Python.Runtime;
using System.Collections.Concurrent;

namespace CelesteBot_2023
{
    public class GameState
    {
        public PyList Vision { get; }
        public double Reward { get; }
        public bool DeathFlag { get; }
        public bool FinishedLevel { get; }
        public int[] Speed { get; }
        public double Stamina { get; }
        public float CanDash { get; }
        public GameState(PyList vision, float speedX, float speedY, float stamina, bool canDash, double reward, bool deathFlag, bool finishedLevel)
        {
            // Observation
            Vision = vision;
            Speed = new int[] { (int)speedX, (int)speedY };
            Stamina = Util.Normalize(stamina, -1, 120);
            CanDash = canDash ? 1 : 0;
            // Reward
            Reward = reward;
            DeathFlag = deathFlag;
            FinishedLevel = finishedLevel;
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
        }

        public GameState PythonGetNextObservation()
        {
            return GameStateQueue.Take();
        }
    }
}
