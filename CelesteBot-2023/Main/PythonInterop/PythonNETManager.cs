using Python.Runtime;
using System;
using System.IO;
using System.Threading;

namespace CelesteBot_2023
{
    /* This file handles all data interconnects between the Python RLLib client and the C#/.NET mod using PythonNET.
     * 
     */

    public class PythonNETManager
    {
        static Thread actionConsumerThread;
        static Thread observationProducerThread;
        static Thread trainingLoop;
        static Thread rewardQueueProducer;
        public static void Setup()
        {
            // virtual env setup
            Runtime.PythonDLL = @"C:\Users\Ashvin\AppData\Local\Programs\Python\Python38\python38.dll";
            // var pathToVirtualEnv = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
            string pathToVirtualEnv = @"C:\Users\Ashvin\AppData\Local\Programs\Python\Python38";
            var path = Environment.GetEnvironmentVariable("PATH").TrimEnd(';');
            path = string.IsNullOrEmpty(path) ? pathToVirtualEnv : path + ";" + pathToVirtualEnv;
            // set path
            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
            // python home
            Environment.SetEnvironmentVariable("PYTHONHOME", pathToVirtualEnv, EnvironmentVariableTarget.Process);
            // python path
            string libSitePackages = Path.Combine(pathToVirtualEnv, "Lib", "site-packages");
            string libPackages = Path.Combine(pathToVirtualEnv, "Lib");
            Environment.SetEnvironmentVariable("PYTHONPATH", $@"{libSitePackages};{libPackages}", EnvironmentVariableTarget.Process);
        }
        [CelesteBotMain.Unload]
        public static void Unload()
        {
            //trainingLoop.Interrupt();
            //rewardQueueProducer.Interrupt();
            //actionConsumerThread.Interrupt();
            //observationProducerThread.Interrupt();
            PythonEngine.Shutdown();
        }

        [CelesteBotMain.Initialize]
        public static void Initialize()
        {
            // Configure PythonNET interoperability using a python virtual env.
            // Store your Python DLL as an environment variable "PYTHONNET_PYDLL" before running.
            CelesteBotMain.Log("PYTHON Initializing");
            Setup();
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            
            using (Py.GIL())
            {
                dynamic queue = Py.Import("queue"); // import the queue module
                dynamic rl_client = Py.Import("python_rl.rl_client.celestebot_client");


                // Create a BlockingCollection as a shared queue
                dynamic py_queue = queue.Queue();

                dynamic python_celeste_client = rl_client.CelesteClient(py_queue);
                actionConsumerThread = new Thread(() => ActionQueueConsumer(py_queue))
                {
                    Name = "ActionConsumer"
                };
                observationProducerThread = new Thread(() => ObservationQueueProducer(python_celeste_client))
                {
                    Name = "ObservationProducer"
                };
                trainingLoop = new Thread(() => RunTrainingLoop(python_celeste_client))
                {
                    Name = "TrainingLoop"
                };

                rewardQueueProducer = new Thread(() => RewardQueueProducer(python_celeste_client))
                {
                    Name = "RewardProducer"
                };
                CelesteBotRunner.Vision2D = new PyList();
                for (int i = 0; i < CelesteBotMain.VISION_2D_X_SIZE; i++)
                {
                    PyList subList = new();
                    for (int j = 0; j < CelesteBotMain.VISION_2D_Y_SIZE; j++)
                    {
                        subList.Append(new PyInt(0));
                    }
                    CelesteBotRunner.Vision2D.Append(subList);
                }
            }
            actionConsumerThread.Start();
            observationProducerThread.Start();
            trainingLoop.Start();
            rewardQueueProducer.Start();
            CelesteBotMain.Log("Python Finished Initializing");

        }
        static void RunTrainingLoop(dynamic py_celeste_client)
        {
            CelesteBotMain.Log("Py Training Loop Initializing");


            py_celeste_client.start_training();

        }
        // This method adds items to the queue by calling a Python function
        static void ActionQueueConsumer(dynamic py_action_queue)
        {
            // Consumes Actions sent from Python client
            CelesteBotMain.Log("Py Action consumer Loop Initializing");


            // Loop through the Python queue and add each item to the BlockingCollection
            while (true)
            {
                //CelesteBotManager.Log("Attempting queue get");
                // Get an item from the Python queue
                int[] actions;

                dynamic output = py_action_queue.get();
                using (Py.GIL())
                {
                    actions = (int[])output;
                }

                // Convert it to a C# Item object
                CelesteBotRunner.ActionManager.PythonAddAction(actions);

            }

        }
        static void RewardQueueProducer(dynamic python_celeste_client)
        {
            // Sends Rewards to Python client
            CelesteBotMain.Log("Py Reward Producer Loop Initializing");

            while(true)
            {
                double reward = CelesteBotRunner.GameStateManager.PythonGetNextReward();

                using (Py.GIL())
                {
                    python_celeste_client.ext_add_reward(reward);
                }
            }
        }
        static void ObservationQueueProducer(dynamic python_celeste_client)
        {
            // Sends Observations of Game State to Python client
            CelesteBotMain.Log("Py Observation Producer Loop Initializing");


            // Loop through the Python queue and add each item to the BlockingCollection
            while (true)
            {
                //CelesteBotManager.Log("Attempting Observation queue get");
                // Convert it to a C# Item object
                GameState obs = CelesteBotRunner.GameStateManager.PythonGetNextObservation();
                using (Py.GIL())
                {
                    PyObject stamina = obs.Stamina.ToPython();
                    PyList speed = ToPyList(obs.Speed);
                    PyList target = ToPyList(obs.Target);
                    PyList position = ToPyList(obs.Position);
                    PyList screenPosition = ToPyList(obs.ScreenPosition);

                    PyObject canDash = obs.CanDash.ToPython();
                    //PyObject reward = obs.Reward.ToPython();
                    PyObject deathFlag = obs.DeathFlag.ToPython();
                    PyObject finishedLevel = obs.FinishedLevel.ToPython();
                    PyObject isClimbing = obs.IsClimbing.ToPython();
                    PyObject onGround = obs.OnGround.ToPython();
                    //def ext_add_observation(self, vision, speed_x_y, can_dash, stamina, death_flag, finished_level, target, position, screen_position, is_climbing, on_ground):
                    python_celeste_client.ext_add_observation(obs.Vision, speed, canDash, stamina, deathFlag, finishedLevel, target, position, screenPosition, isClimbing, onGround);

                }
            }

        }
        static void Test(dynamic celeste_client)
        {
            celeste_client.ext_test();
        }
        static PyList ToPyList(int[] intList)
        {
            PyList list = new();
            foreach (int i in intList)
            {
                list.Append(new PyInt(i));
            }
            return list;
        }
        static PyList ToPyList(float[] floats)
        {
            PyList list = new();
            foreach (float f in floats)
            {
                list.Append(new PyFloat(f));

            }
            return list;

        }
    }
}
