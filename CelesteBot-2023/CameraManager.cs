using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CelesteBot_2023
{
    internal class CameraManager
    {


        public static float[] CameraPosition
        {
            get
            {
                Level level;
                try
                {
                    level = (Level)Monocle.Engine.Scene;
                }
                catch (InvalidCastException)
                {
                    // This means we tried to cast a LevelExit to a Level. It basically means we are dead.
                    //Dead = true;
                    // Wait for the timer to expire before actually resetting
                    return null;
                }
                return new float[] { level.Camera.X, level.Camera.Y };

            }
        }

    }
}
