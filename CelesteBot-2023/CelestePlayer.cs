using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Python.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        public double LastReward = 0;
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
        public TrainingEpisode Episode;
        internal double LastDistanceFromTarget;

        public CelestePlayer()
        {



            
            timer = new Stopwatch();
            deathTimer = new Stopwatch();

            Rewards = new List<double>();
            Episode = new TrainingEpisode(this);
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
            CalculateGameState();

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
            UpdateVision();
            Episode.UpdateTarget();
            CalculateGameState();
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
        // 1 for tile (walls), -1 for entities (moving platforms, etc.)
        // Might add more ex: -2 = dashblox, ... or new Nodes indicating type of entity/tile along with input box
        private void UpdateVision()
        {
            int visionX = CelesteBotManager.VISION_2D_X_SIZE;
            int visionY = CelesteBotManager.VISION_2D_Y_SIZE;
            int underYIndex = visionY / 2 + 1;
            int underXIndex = visionX / 2;
            try
            {
                Level level = (Level)Monocle.Engine.Scene;
            }
            catch (InvalidCastException)
            {
                // This means we tried to cast a LevelExit to a Level. It basically means we are dead.
                //Dead = true;
                // Wait for the timer to expire before actually resetting
                return;
            }

            Vector2 tileUnder = TileFinder.GetTileXY(new Vector2(player.X, player.Y + 4));
            //Logger.Log(CelesteBotInteropModule.ModLogKey, "Tile Under Player: (" + tileUnder.X + ", " + tileUnder.Y + ")");
            //Logger.Log(CelesteBotInteropModule.ModLogKey, "(X,Y) Under Player: (" + player.X + ", " + (player.Y + 4) + ")");
            // 1 = Air, 2 = Wall, 4 = Entity

            //MTexture[,] tiles = TileFinder.GetSplicedTileArray(visionX, visionY);
            TileFinder.UpdateGrid();
            TileFinder.CacheEntities();
            for (int i = 0; i < visionY; i++)
            {
                for (int j = 0; j < visionX; j++)
                {
                    if (TileFinder.tileArray != null)
                    {
                        //if (TileFinder.tileArray[(int)(tileUnder.X - underXIndex + j), (int)(tileUnder.Y - underYIndex + i)] != null)
                        //{
                        //    Logger.Log(CelesteBotInteropModule.ModLogKey, TileFinder.tileArray[(int)(tileUnder.X - underXIndex + j), (int)(tileUnder.Y - underYIndex + i)].ToString());
                        //}
                    }
                    /*int temp = TileFinder.IsSpikeAtTile(new Vector2(tileUnder.X - underXIndex + j, tileUnder.Y - underYIndex + i)) ? 8 : 1;
                    if (temp == 1)
                    {
                        temp = TileFinder.IsWallAtTile(new Vector2(tileUnder.X - underXIndex + j, tileUnder.Y - underYIndex + i)) ? 2 : 1;
                    }
                    if (temp == 1)
                    {
                        temp = TileFinder.IsEntityAtTile(new Vector2(tileUnder.X - underXIndex + j, tileUnder.Y - underYIndex + i)) ? 4 : 1;
                    }*/
                    int obj = (int)TileFinder.GetEntityAtTile(new Vector2(tileUnder.X - underXIndex + j, tileUnder.Y - underYIndex + i));
                    
                    using (Py.GIL())
                    {
                        //dynamic np = Py.Import("numpy");
                        Vision2D[j][i] = new PyInt(obj);
                    }
                }
            }
        }
        // Returns the convolution of the given kernel over the vision2d array with stride stride
        //int[,] Convolve(int[,] vision2d, int[] kernel, int[] stride)
        //{

        //}
        void CalculateGameState()
        {

            // Updates vision array with proper values each frame
            /*
            Inputs: PlayerX, PlayerY, PlayerXSpeed, PlayerYSpeed, <INPUTS FROM VISUALIZATION OF GAME>
            IT IS ALSO POSSIBLE THAT X AND Y ARE UNNEEDED, AS THE VISUALIZATION INPUTS MAY BE ENOUGH
            Outputs: U, D, L, R, Jump, Dash, Climb
            If any of the outputs are above 0.7, apply them when returning controller output
            */
            double reward = Episode.GetReward();
            GameState CurrentGameState = new GameState(Vision2D, player.Speed.X, player.Speed.Y, player.Stamina, player.CanDash, reward, Episode.Died, Episode.FinishedLevel);
            CelesteBotInteropModule.GameStateManager.AddObservation(CurrentGameState);
            if (Episode.FinishedLevel || Episode.Died)
            {
                Episode.ResetEpisode();
            }
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
