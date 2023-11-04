using Celeste;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CelesteBot_2023.CelesteBotInteropModule;

namespace CelesteBot_2023
{
    public class TargetFinder
    {
        const string PATH = @"levels.txt";
        private static string[] levelNames;
        private static Vector2 targetSpawnPoints;
        [Initialize]
        public static void Initialize()
        {
            levelNames = System.IO.File.ReadAllLines(PATH);

        }

        public static void LoadLevelsForCurrentMap(Level currentLevel)
        {
            MapData map = currentLevel.Session.MapData;
            foreach (string levelName in levelNames)
            {
                LevelData levelData = map.Get(levelName);
                if (levelData != null)
                {
                    Vector2? spawnPoint = levelData.DefaultSpawn;
                    CelesteBotManager.Log("Found level spawn point: " + spawnPoint.ToString() + "for level " + levelName);

                    //Vector2 bounds = new Vector2(levelData.Bounds.Left, levelData.Bounds.Bottom)
                    //levelData.Spawns.ClosestTo(bounds)
                }
            }
        }

    }
}
