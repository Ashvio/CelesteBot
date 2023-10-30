using Celeste.Mod;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CelesteBot_2023
{
    public class PythonManager
    {
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
        public static void Initialize()
        {
            // Configure PythonNET interoperability using a python virtual env.
            // Store your Python DLL as an environment variable "PYTHONNET_PYDLL" before running.
            CelesteBotManager.Log( "PYTHON Initializing");
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            using (Py.GIL())
            {
                dynamic queue = Py.Import("queue"); // import the queue module
                dynamic rl_client = Py.Import("python_rl.rl_client.celestebot_client");


                // Create a BlockingCollection as a shared queue
                dynamic py_queue = queue.Queue();

                dynamic python_celeste_client = rl_client.CelesteClient(py_queue);
                Thread ActionConsumerThread = new Thread(() => ActionQueueConsumer(py_queue));
                ActionConsumerThread.Start();
                Thread TestThread = new Thread(() => Test(python_celeste_client));
                TestThread.Start();
                // Create two tasks: one for adding items, one for testing python
                //Task producer = Task.Factory.StartNew(() => AddItems(queue));
                //Task tester = Task.Factory.StartNew(() => Test(python_celeste_client));

                // Wait for both tasks to complete
                //Task.WaitAll(producer, tester);


                //string pythonFile = @"rl_client\celestebot_client.py";

                //var scope = Py.CreateScope();
                //string code = File.ReadAllText(pythonFile);
                //var scriptCompiled = PythonEngine.Compile(code, pythonFile); // Compile the code/file
                //scope.Execute(scriptCompiled); // Execute the compiled python so we can start calling it.
                //PyObject test = scope.Get("test"); // Lets get an instance of the class in python
                //PyObject pythongReturn = test.Invoke(new PyString("PYTHON TEST sayHello")); // Call the sayHello function on the exampleclass object
                //var result = pythongReturn.AsManagedObject(typeof(string)) as string;
                //Logger.Log(CelesteBotInteropModule.ModLogKey, result);
            }
        }
        // This method adds items to the queue by calling a Python function
        static void ActionQueueConsumer(dynamic py_action_queue)
        {
            // Consumes Actions sent from Python client


            // Loop through the Python queue and add each item to the BlockingCollection
            while (true)
            {
                CelesteBotManager.Log("Attempting queue get");
                // Get an item from the Python queue
                int[] actions = (int[])py_action_queue.get();

                // Convert it to a C# Item object
                CelesteBotInteropModule.ActionManager.PythonAddAction(actions);

            }

        }
        static void ObservationQueueProducer(dynamic python_celeste_client)
        {
            // Sends Observations of Game State to Python client


            // Loop through the Python queue and add each item to the BlockingCollection
            while (true)
            {
                //CelesteBotManager.Log("Attempting Observation queue get");
                // Convert it to a C# Item object
                Observation obs = CelesteBotInteropModule.ObservationManager.PythonGetNextObservation();
                PyObject vision = obs.Vision.ToPython();
                PyObject stamina = obs.Stamina.ToPython();
                PyObject speed = obs.Speed.ToPython();
                PyObject canDash = obs.CanDash.ToPython();
                python_celeste_client.ext_add_observation(vision, speed, canDash, stamina );
            }

        }
        static void Test(dynamic celeste_client)
        {
            celeste_client.ext_test();
        }
    }
}
