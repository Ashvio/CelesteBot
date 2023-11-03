using Celeste;
using CelesteBot_2023;
using Microsoft.Xna.Framework;
using Monocle;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CelesteBot_2023
{
    public class CameraManager
    {
        CelestePlayer c_player;

        public PyList CameraVision;
        public const int NUM_PIXELS_PER_TILE = 8;
        public const int TILES_SCREEN_WIDTH = 40;
        
        public Player player { get => c_player.player; }

        public CameraManager(CelestePlayer player)
        {
            using (Py.GIL())
            {
                CameraVision = new PyList();
                for (int i = 0; i < TILES_SCREEN_WIDTH; i++)
                {
                    PyList list = new PyList();
                    for (int j = 0; j < TILES_SCREEN_WIDTH; j++)
                    {
                        PyList sublist = new PyList();
                        sublist.Append(new PyInt(0));
                        list.Append(sublist);
                    }
                    CameraVision.Append(list);
                }
            }
            this.c_player = player;
            
        }   
        internal void UpdateScreenVision()
        {
            //int underYIndex = visionY / 2 + 1;
            //int underXIndex = visionX / 2;
            
            // TODO: Get Screen coordinates/position of madeline
            // level.Camera:CameraToScreen(player.Position) 
            //(you might also have to add / subtract level.Camera.Position to player.Position) 
            // 8 pixels per tile
            float xMax = CameraViewPortWidth;
            float yMax = CameraViewPortHeight;
            //CelesteBotManager.Log(level.Camera.ToString());
            //Logger.Log(CelesteBotInteropModule.ModLogKey, "Tile Under Player: (" + tileUnder.X + ", " + tileUnder.Y + ")");
            //Logger.Log(CelesteBotInteropModule.ModLogKey, "(X,Y) Under Player: (" + player.X + ", " + (player.Y + 4) + ")");
            // 1 = Air, 2 = Wall, 4 = Entity

            //MTexture[,] tiles = TileFinder.GetSplicedTileArray(visionX, visionY);
            TileFinder.UpdateGrid();
            TileFinder.CacheEntities();
            Vector2 playerTile = TileFinder.GetTileXY(new Vector2(player.X, player.Y - 4));
            for (int i = 0; i < xMax; i += NUM_PIXELS_PER_TILE)
            {
                for (int j = 0; j < yMax; j += NUM_PIXELS_PER_TILE)
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
                    Vector2 tile = TileFinder.GetTileXY(new Vector2(camera.X + i, camera.Y + j));
                    int obj;
                    if (playerTile == tile)
                    {
                        obj = (int) Entity.Madeline;
                    }
                    else
                    {
                        obj = (int)TileFinder.GetEntityAtTile(tile);
                    }
                    using (Py.GIL())
                    {
                        PyList list = new PyList();
                        list.Append(new PyInt(obj));
                        //dynamic np = Py.Import("numpy");
                        CameraVision[j / NUM_PIXELS_PER_TILE][i / NUM_PIXELS_PER_TILE] = list;
                    }
                }
                //using (Py.GIL()) 
                //    CelesteBotManager.Log(CameraVision[i / NUM_PIXELS_PER_TILE].ToString());
            }
        }
        internal static Level level
        {
            get
            {
                Level level;
                try
                {
                    level = (Level)Engine.Scene;
                }
                catch (InvalidCastException)
                {
                    // This means we tried to cast a LevelExit to a Level. It basically means we are dead.
                    //Dead = true;
                    return null;
                }
                return level;
            }
        }
        internal static Camera camera
        {
            get
            {
                try
                {
                    return level.Camera;
                }
                catch (InvalidCastException)
                {
                    return null;
                }
            }
        }
        public static float[] CameraPosition
        {
            get
            {
                try
                {
                    return new float[] { camera.X, camera.Y };
                }
                catch (NullReferenceException)
                {
                    return new float[] { -1, -1 };
                }

            }
        }
        public static float CameraViewPortWidth
        {
            get
            {
                try
                {
                    return camera.Viewport.Width;
                }
                catch (NullReferenceException)
                {
                    return -1;
                }

            }
        }
        public static float CameraViewPortHeight
        {
            get
            {
                try
                {
                    return camera.Viewport.Height;
                }
                catch (NullReferenceException)
                {
                    return -1;
                }

            }
        }

    }
}
