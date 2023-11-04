using Python.Runtime;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static CelesteBot_2023.CelesteBotInteropModule;

namespace CelesteBot_2023
{
    public class PythonManager
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
        [Unload]
        public static void Unload()
        {
            //trainingLoop.Interrupt();
            //rewardQueueProducer.Interrupt();
            //actionConsumerThread.Interrupt();
            //observationProducerThread.Interrupt();
            PythonEngine.Shutdown();
        }

        [Initialize]
        public static void Initialize()
        {
            // Configure PythonNET interoperability using a python virtual env.
            // Store your Python DLL as an environment variable "PYTHONNET_PYDLL" before running.
            CelesteBotManager.Log("PYTHON Initializing");
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            
            using (Py.GIL())
            {
                dynamic queue = Py.Import("queue"); // import the queue module
                dynamic rl_client = Py.Import("python_rl.rl_client.celestebot_client");


                // Create a BlockingCollection as a shared queue
                dynamic py_queue = queue.Queue();

                dynamic python_celeste_client = rl_client.CelesteClient(py_queue);
                actionConsumerThread = new Thread(() => ActionQueueConsumer(py_queue));
                actionConsumerThread.Name = "ActionConsumer";
                observationProducerThread = new Thread(() => ObservationQueueProducer(python_celeste_client));
                observationProducerThread.Name = "ObservationProducer";
                trainingLoop = new Thread(() => RunTrainingLoop(python_celeste_client));
                trainingLoop.Name = "TrainingLoop";

                rewardQueueProducer = new Thread(() => RewardQueueProducer(python_celeste_client));
                rewardQueueProducer.Name = "RewardProducer";
                AIPlayerLoop.Vision2D = new PyList();
                for (int i = 0; i < CelesteBotManager.VISION_2D_X_SIZE; i++)
                {
                    PyList sublist = new PyList();
                    for (int j = 0; j < CelesteBotManager.VISION_2D_Y_SIZE; j++)
                    {
                        sublist.Append(new PyInt(0));
                    }
                    AIPlayerLoop.Vision2D.Append(sublist);
                }
            }
            actionConsumerThread.Start();
            observationProducerThread.Start();
            trainingLoop.Start();
            rewardQueueProducer.Start();
            CelesteBotManager.Log("Python Finished Initializing");

        }
        static void RunTrainingLoop(dynamic py_celeste_client)
        {
            CelesteBotManager.Log("Py Training Loop Initializing");


            py_celeste_client.start_training();

        }
        // This method adds items to the queue by calling a Python function
        static void ActionQueueConsumer(dynamic py_action_queue)
        {
            // Consumes Actions sent from Python client
            CelesteBotManager.Log("Py Action consumer Loop Initializing");


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
                ActionManager.PythonAddAction(actions);

            }

        }
        static void RewardQueueProducer(dynamic python_celeste_client)
        {
            // Sends Rewards to Python client
            CelesteBotManager.Log("Py Reward Producer Loop Initializing");

            while(true)
            {
                double reward = CelesteBotInteropModule.GameStateManager.PythonGetNextReward();

                using (Py.GIL())
                {
                    python_celeste_client.ext_add_reward(reward);
                }
            }
        }
        static void ObservationQueueProducer(dynamic python_celeste_client)
        {
            // Sends Observations of Game State to Python client
            CelesteBotManager.Log("Py Observation Producer Loop Initializing");


            // Loop through the Python queue and add each item to the BlockingCollection
            while (true)
            {
                //CelesteBotManager.Log("Attempting Observation queue get");
                // Convert it to a C# Item object
                GameState obs = CelesteBotInteropModule.GameStateManager.PythonGetNextObservation();
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

                    python_celeste_client.ext_add_observation(obs.Vision, speed, canDash, stamina, deathFlag, finishedLevel, target, position, screenPosition, isClimbing, onGround);

                }
            }

        }
        static void Test(dynamic celeste_client)
        {
            celeste_client.ext_test();
        }
        static PyList ToPyList(int[] ints)
        {
            PyList list = new PyList();
            foreach (int i in ints)
            {
                list.Append(new PyInt(i));
            }
            return list;
        }
        static PyList ToPyList(float[] floats)
        {
            PyList list = new PyList();
            foreach (float f in floats)
            {
                list.Append(new PyFloat(f));

            }
            return list;

        }
    }
}
