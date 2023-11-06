using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using static CelesteBot_2023.CelesteBotInteropModule;
/*
* The selected code block belongs to the CelesteBotManager class and is found in the CelesteBotManager.cs file.
This class contains various member variables that hold the configurations for the program as well as some utility 
methods namely Initialize, Draw, UpdateQTable.
The CelesteBotManager class contains parameters that configure the way the NEAT algorithm functions when training,
such as how weights are mutated (with the parameter WEIGHT_MUTATION_CHANCE), the chance for a new connection to be added 
(ADD_CONNECTION_CHANCE), and the chance for a new node to be added (ADD_NODE_CHANCE). Other parameters include learning 
rate, gamma, and epsilon for the Q-learning algorithm, and some fitness parameters for the training process.
The Initialize method loads the configuration settings that can be modified by the user, and sets them in their respective 
variables. The Draw method is responsible for rendering visual elements like graphs and the player's neural network. 
Finally, the UpdateQTable method performs the necessary calculations on the current state and action, thus updating the 
Q Table for the player.
* 
*/
namespace CelesteBot_2023
{
    public class CelesteBotManager
    {
        public static float ACTION_THRESHOLD = 0.55f; // The value that must be surpassed for the output to be accepted
        public static float RE_RANDOMIZE_WEIGHT_CHANCE = 0.2f; // The chance for the weight to be re-randomized
        public static double WEIGHT_MUTATION_CHANCE = 0.65f; // The chance for a weight to be mutated
        public static double ADD_CONNECTION_CHANCE = 0.55f; // The chance for a new connection to be added
        public static double ADD_NODE_CHANCE = 0.15f; // The chance for a new node to be added

        public static double WEIGHT_MAXIMUM = 3; // Max magnitude a weight can be (+- this number)

        public static readonly int VISION_2D_X_SIZE = 20; // X Size of the Vision array
        public static readonly int VISION_2D_Y_SIZE = 20; // Y Size of the Vision array
        public static int TILE_2D_X_CACHE_SIZE = 1000;
        public static int TILE_2D_Y_CACHE_SIZE = 1000;
        public static int ENTITY_CACHE_UPDATE_FRAMES = 10;
        public static int FAST_MODE_MULTIPLIER = 10;

        public static float UPDATE_TARGET_THRESHOLD = 25; // Pixels in distance between the fitness target and the current position before considering it "reached"

        public static Vector2 TEXT_OFFSET = new Vector2(7, 7);
        // Graphing Parameters
        public static ArrayList SavedBestFitnesses = new ArrayList();

        // POPULATION PARAMETERS
        //public static int POPULATION_SIZE = 50;

        public static double PLAYER_DEATH_TIME_BEFORE_RESET = 1; // How many seconds after a player dies should the next player be created and the last one deleted

        // Paths/Prefixes

        public static bool Cutscene = false;

        public static bool IsWorker { get; set; }
        // Q Learning Settings

        public static Thread PythonThread;
        //private static Assembly ResolvePython(object sender, ResolveEventArgs args)
        //{
        //    // Forbid non handled dll's
        //    if (!args.Name.Contains("Python.Runtime"))
        //    {
        //        return null;
        //    }
        //    string LIBS_PATH = @"Lib\pythonnet\runtime\site-packages";
        //    string ASSEMBLY_FILE = "Python.Runtime.dll";
        //    string pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME");
        //    string targetPath = Path.Combine(pythonHome, LIBS_PATH, ASSEMBLY_FILE);

        //    try
        //    {
        //        return Assembly.LoadFile(targetPath);
        //    }
        //    catch (Exception)
        //    {
        //        return null;
        //    }
        //}

        public static void Log(string message, LogLevel level = LogLevel.Verbose)
        {
            Logger.Log(level, ModLogKey, message);
        }
        [Initialize]
        public static void Initialize()
        {
            Logger.Log(ModLogKey, "CELESTEBOT Initializing");

            PythonManager.Setup();
            string currentDir = Directory.GetCurrentDirectory();
            if (currentDir.Contains("CelesteWorkers"))
            {
                IsWorker = true;
                Logger.SetLogLevel(ModLogKey, LogLevel.Info);
                CelesteBotInteropModule.Settings.TrainingEnabled = true;
            }
            else
            {
                IsWorker = false;
                Logger.SetLogLevel(ModLogKey, LogLevel.Verbose);

            }

            //PythonThread = new Thread(new ThreadStart(PythonManager.Initialize));
            //PythonThread.Start();
            ACTION_THRESHOLD = (float)(Convert.ToDouble(CelesteBotInteropModule.Settings.ActionThreshold) / 100.0); // The value that must be surpassed for the output to be accepted


            TILE_2D_X_CACHE_SIZE = CelesteBotInteropModule.Settings.XMaxCacheSize; // X Size of max cache size
            TILE_2D_Y_CACHE_SIZE = CelesteBotInteropModule.Settings.YMaxCacheSize; // Y Size of max cache size
            ENTITY_CACHE_UPDATE_FRAMES = CelesteBotInteropModule.Settings.EntityCacheUpdateFrames; // Frames between updating entity cache
            FAST_MODE_MULTIPLIER = CelesteBotInteropModule.Settings.FastModeMultiplier; // speed multiplier for fast mode


            UPDATE_TARGET_THRESHOLD = CelesteBotInteropModule.Settings.UpdateTargetThreshold;

            TEXT_OFFSET = new Vector2(7, 7);
            // Graphing Parameters
            SavedBestFitnesses = new ArrayList();
            PLAYER_DEATH_TIME_BEFORE_RESET = 2; // How many seconds after a player dies should the next player be created and the last one deleted
            Log("Finished Initializing CelesteBot");
        }

        public static bool CompleteCutsceneSkip(InputPlayer inputPlayer)
        {
            InputData thisFrame = new InputData();
            // If the last frame contains an escape, make this frame contain a menu down
            if (inputPlayer.Data.ESC)
            {
                thisFrame.MenuDown = true;
            }
            // If the last frame contains a menu down, make this frame contain a menu confirm
            else if (inputPlayer.Data.MenuDown)
            {
                thisFrame.MenuConfirm = true;
            }
            else
            {
                // This means we are done with handling a cutscene.
                // Just make sure we are playing again!
                return false;
            }
            Logger.Log(CelesteBotInteropModule.ModLogKey, "Completing Cutscene Skip with inputs: " + thisFrame + " and Cutscene: " + Cutscene);
            inputPlayer.UpdateData(thisFrame);
            return true;
        }
        public static bool CheckForCutsceneSkip(InputPlayer inputPlayer)
        {
            // three inputs need to be done to successfully skip a cutscene.
            // esc --> menu down --> menu confirm
            try
            {
                if (Cutscene)
                {
                    Cutscene = CompleteCutsceneSkip(inputPlayer);
                    Logger.Log(ModLogKey, "Confirmed a cutscene skip!");
                    Logger.Log(CelesteBotInteropModule.ModLogKey, "After Cutscene skip: " + Cutscene);
                    return true; // even if it returned false last time, still skip
                }
                try
                {
                    Level level = (Level)Engine.Scene;

                    if (level.InCutscene)
                    {
                        Logger.Log(ModLogKey, "Confirmed a cutscene skip!");

                        Logger.Log(CelesteBotInteropModule.ModLogKey, "Entered Cutscene! With Cutscene: " + Cutscene);
                        Cutscene = true;
                        InputData newFrame = new InputData();
                        newFrame.ESC = true;
                        inputPlayer.UpdateData(newFrame);
                        return true;
                    }
                }
                catch (InvalidCastException)
                {
                    // Game still hasn't finished loading...
                }
            }
            catch (NullReferenceException)
            {
                // Level or Player hasn't been setup yet. Just continue on for now.
            }
            return false;
        }
        public static bool CompleteRestart(InputPlayer inputPlayer)
        {
            if (inputPlayer.LastData.QuickRestart)
            {
                InputData temp = new InputData();
                temp.MenuConfirm = true;
                inputPlayer.UpdateData(temp);
                Logger.Log(ModLogKey, "Restarting!");
                return true;
            }
            return false;
        }





     
        public static void DrawDetails(AIPlayerLoop p)
        {
            Monocle.Draw.Rect(0f, 90f, 600f, 30f, Color.Black * 0.8f);
            ActiveFont.Draw("(X: " + p.player.BottomCenter.X + ", Y: " + p.player.BottomCenter.Y + "), (Vx: " + p.player.Speed.X + ", Vy: " + p.player.Speed.Y + "), Dashes: " + p.player.Dashes + ", Stamina: " + p.player.Stamina, new Vector2(3, 90), Vector2.Zero, new Vector2(0.4f, 0.4f), Color.White);
        }
        public static void DrawBestFitness()
        {

        }
        public static void DrawAppendMode()
        {
            try
            {
                Player player = Engine.Scene.Tracker.GetEntity<Player>();
                Level level = (Level)Engine.Scene;

                Monocle.Draw.Rect(0f, 60f, 600f, 30f, Color.Black * 0.8f);
                ActiveFont.Draw(level.Session.MapData.Filename + "_" + level.Session.Level + ": [" + player.BottomCenter.X + ", " + player.BottomCenter.Y + ", " + player.Speed.X + ", " + player.Speed.Y + "]", new Vector2(3, 60), Vector2.Zero, new Vector2(0.45f, 0.45f), Color.White);
            }
            catch (Exception)
            {
                // Pass
            }
        }
    }
}