using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CelesteBot_2023
{
    public class Observation
    {
        int[][] Vision;
        int[] Speed;
        float Stamina;
        float CanDash;
        public Observation(int[][] vision, float speedX, float speedY, float stamina, bool canDash) {
            Vision = vision;
            Speed = new int[] { (int)speedX, (int)speedY };
            Stamina = CelesteBotManager.Normalize(stamina, -1, 120) ;
            CanDash = canDash ? 1 : 0;
        }
    }
    public class ExternalObservationManager
    {
        BlockingCollection<Observation> ObservationQueue;

        public ExternalObservationManager()
        {
            ObservationQueue = new BlockingCollection<Observation>();
        }

        public void AddObservation(Observation obs)
        {
            ObservationQueue.Add(obs);
            CelesteBotManager.Log("Added Observation to queue");

        }

        public Observation PythonGetNextObservation()
        {
            return ObservationQueue.Take();
        }
    }
}
