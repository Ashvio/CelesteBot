using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Python.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
/*
* The CelestePlayer class represents the player in the game and is located in the same file as the CelesteBotInteropModule. 
* It contains a Brain property which represents its neural network, a Fitness property to keep track of its performance in
* the game, InputData and Vision properties which are used to provide input to the neural network, and various other properties
* and methods related to gameplay.
The Brain property is used to decide which actions to take based on the inputs given to it. The Population of players contains 
multiple instances of CelestePlayer. During gameplay, the current player is retrieved from the Population and manipulated using
the CurrentPlayer property.
*/
namespace CelesteBot_2023
{
    public class CelestePlayer : IDisposable
    {
        public static PyList Vision2D;

        public Player player;
        Vector2 startPos = new Vector2(0, 0);

        public float Fitness = -1;
        public double LastEpisodeReward { get { return TrainingEpisode.LastEpisodeReward; } }
        private float AverageSpeed = 0;
        private float AverageStamina = 110;
        public float UnadjustedFitness;
        public ArrayList ReplayActions = new ArrayList();
        public int Lifespan = 0;
        public bool WaitingForRespawn = false;
        public bool Replay = false;
        private Stopwatch timer;
        private Stopwatch deathTimer;
        public string Name;


        public Vector2 Target = Vector2.Zero;
        public int TargetsPassed = 0;

        private Vector2 MaxPlayerPos = new Vector2(-10000, -10000);

        public bool VisionSetup = false;
        public List<double> Rewards;

        public bool DeathFlag { get; private set; }
        public object LastAction { get; internal set; }
        public CameraManager cameraManager;
        public TrainingEpisode Episode;

        public CelestePlayer()
        {
            timer = new Stopwatch();
            deathTimer = new Stopwatch();

            Rewards = new List<double>();
            Episode = new TrainingEpisode(this);
            cameraManager = null;
        }
        bool CheckDeathTimer()
        {
            if (deathTimer.ElapsedMilliseconds * CelesteBotInteropModule.FrameLoops > CelesteBotManager.PLAYER_DEATH_TIME_BEFORE_RESET * 1000)
            {
                WaitingForRespawn = false;
                deathTimer.Reset();
                return true;
            }
            else
            {
                return false;
            }
        }
        bool CheckPlayer()
        {
            try
            {
                player = Monocle.Engine.Scene.Tracker.GetEntity<Player>();
                if (!player.Dead && Fitness == -1)
                {
                    startPos = player.BottomCenter;
                }
                cameraManager = new CameraManager(this);
                return true;
            }
            catch (NullReferenceException)
            {
                Logger.Log(CelesteBotInteropModule.ModLogKey, "Player has not been created yet, or is null for some other reason.");
                return false;
            }
        }
        void SetDead()
        {
            Episode.Died = true;
            WaitingForRespawn = true;
            GameState gameState = new GameState(cameraManager.CameraVision, player, Episode);
            
            CelesteBotInteropModule.GameStateManager.AddObservation(gameState);
            double reward = Episode.GetReward();

            CelesteBotInteropModule.GameStateManager.AddReward(reward);
            // CelesteBotInteropModule.ActionManager.Flush();
            if (!deathTimer.IsRunning)
            {
                deathTimer.Start();
            }

        }
        public void Update()
        {
            if (WaitingForRespawn)
            {
                bool done = CheckDeathTimer();
                if (!done)
                {
                    return;
                }
                player = Monocle.Engine.Scene.Tracker.GetEntity<Player>();
                Episode.ResetEpisode();
            }
            if (player == null)
            {
                bool done = CheckPlayer();
                if (!done)
                {
                    return;
                }
                Episode.ResetEpisode();
            }
            // This is to make sure that we don't try to reset while we are respawning
            if (player.Dead)
            {

                SetDead();
                return;
            }
            //UpdateVision();
            Episode.UpdateTarget();
            cameraManager.UpdateScreenVision();

            if (CelesteBotInteropModule.Settings.TrainingEnabled && !WaitingForRespawn)
            {

                GameState gameState = new GameState(cameraManager.CameraVision, player, Episode);
                CelesteBotInteropModule.GameStateManager.AddObservation(gameState);
            }
            if (Episode.NumFrames % 60 == 0)
            {
                cameraManager.UpdateScreenVision();
                //CelesteBotManager.Log(Vision2D.ToString());
            }
            //Look();
            //Think();


            /*need to incorporate y here, maybe dist to goal here as well*/
            // Compare to distance to fitness target
            if (player.Speed.Length() == 0 || (player.BottomCenter - Target).Length() >= (MaxPlayerPos - Target).Length() && !player.JustRespawned)
            {
                if (!timer.IsRunning)
                {
                    timer.Start();
                }
            }
            else
            {
                timer.Reset(); // Resets TimeWhileStuck if it starts moving again!
            }


            Lifespan++;
            AverageSpeed += player.Speed.LengthSquared() / (float)Lifespan;
            AverageStamina += player.Stamina / (float)Lifespan;
            // Needs to be replaced with minimum distance position, instead
            if ((player.BottomCenter - Target).Length() < (MaxPlayerPos - Target).Length())
            {
                MaxPlayerPos = player.BottomCenter;
            }
            //LastPlayerPos = player.BottomCenter;
        }
        public void SetupVision()
        {
            try
            {
                //TileFinder.TilesOffset = Celeste.Celeste.Scene.Entities.FindFirst<SolidTiles>().Center; // Thanks KDT#7539!
                TileFinder.SetupOffset();
            }
            catch (NullReferenceException)
            {
                // The Scene hasn't been created yet.
            }
            VisionSetup = true;
        }
        

        // Updates controller inputs based on neural network output

        internal void KillPlayer()
        {
            player.Die(Vector2.Zero, true);
            SetDead();
        }

        public void Dispose()
        {
            ReplayActions = null;
        }

    }


}
