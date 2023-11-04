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
    public class AIPlayerLoop {
        public static PyList Vision2D;

        public Player player
        {
            get
            {
                return Monocle.Engine.Scene?.Tracker?.GetEntity<Player>(); 
            }
            
        }

        public float Fitness = -1;
        public double LastEpisodeReward { get { return TrainingEpisodeState.LastEpisodeReward; } }

        public float UnadjustedFitness;
        public ArrayList ReplayActions = new ArrayList();
        public int Lifespan = 0;
        public bool WaitingForRespawn = false;
        public bool Replay = false;

        public string Name;


        public Vector2 Target = Vector2.Zero;
        public int TargetsPassed = 0;

        public bool VisionSetup = false;
        public List<double> Rewards;

        public bool DeathFlag { get; private set; }
        public object LastAction { get; internal set; }
        public CameraManager cameraManager;
        public TrainingEpisodeState Episode;

        public AIPlayerLoop()
        {
            Episode = new TrainingEpisodeState(this);
            cameraManager = new(this);
            WaitingForRespawn = false;
        }

        public bool ProcessGameState(bool PlayerDied, bool PlayerFinishedLevel)
        {
            if (WaitingForRespawn)
            {

                if (!player.Dead)
                {
                    // waiting for respawn
                    WaitingForRespawn = false;
                }
                else
                {
                }
            }
            // player just died, so set it to dead and record observation
            else if (player.Dead)
            {
                WaitingForRespawn = true;
            }
            cameraManager.UpdateScreenVision();
            GameState gameState = new(cameraManager.CameraVision, player, Episode, PlayerDied, PlayerFinishedLevel);
            CelesteBotInteropModule.GameStateManager.AddObservation(gameState);
            return true;
            
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
        }

    }


}
