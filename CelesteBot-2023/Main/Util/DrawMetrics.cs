using Celeste;
using CelesteBot_2023.SimplifiedGraphics;
using Microsoft.Xna.Framework;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static CelesteBot_2023.CelesteBotMain;

namespace CelesteBot_2023
{
    public class DrawMetrics
	{
		public const int SCREEN_WIDTH = 960;
		public const int SCREEN_HEIGHT = 540;
		private static List<Color> Colors;

		[Initialize]
        public static void Initialize()
		{
            Colors = GetColorList();

        }
        public static void Draw(CelesteBotRunner AIPlayer)
		{
			DrawMetricStrings(AIPlayer);
			try
			{
				if (BotState != State.None)
					DrawCameraVision(AIPlayer.cameraManager.CameraVision);
			}
            catch (NullReferenceException)
			{
                // Do nothing
            }
		}
		public static void DrawMetricStrings(CelesteBotRunner AIPlayer)
		{
            string[] metrics = new string[8];
            metrics[0] = "Latest Episode Reward: " + AIPlayer.LastEpisodeReward.ToString("F2");
            metrics[1] = "Current Episode Reward: " + AIPlayer.Episode.TotalReward.ToString("F2");
			metrics[2] = "Last Action Reward" + AIPlayer.Episode.LastActionReward.ToString("F2");
            metrics[3] = "Distance from target: " + AIPlayer.Episode.LastDistanceFromTarget.ToString("F2");
            if (AIPlayer.LatestAction != null)
                metrics[4] = "Last Action: " + AIPlayer.LatestAction.ToString();
			
			metrics[5] = "Action Retrieval Status " + ActionRetrievalStatus;
            metrics[6] = "Death Count: " + AIPlayer.DeathCounter;
			List < Vector2 > targets = TargetFinder.GetLightBeamTargets();
			string targetString = "";
			foreach (Vector2 target in targets)
			{
				targetString += target.ToString() + ", ";
			}
            metrics[7] = "Beam Targets: " + targetString;

            float width = 600;
            float height = 15;
            float x = SCREEN_WIDTH  - width;
			float y = 0;
			Monocle.Draw.Rect(x, y, width, height * metrics.Length, Color.Black * 0.3f);
			for (int i = 0; i < metrics.Length && y < SCREEN_HEIGHT; i++, y+=height)
			{
				if (metrics[i] == null)
                    continue;
				if (metrics[i].Contains("\n"))
				{
					y += height;
				}
				ActiveFont.Draw(metrics[i], new Vector2(x + 3, y), Vector2.Zero, new Vector2(0.3f, 0.3f), Color.White);
			}
        }

		public static List<Color> GetColorList()
		{
			var list = new List<Color>();
			List<PropertyInfo> fields = Color.Blue.GetType()
				.GetProperties(BindingFlags.Static | BindingFlags.Public)
				.Where(f => f.PropertyType == typeof(Color))
				.ToList();
			foreach (var item in fields)
			{
				list.Add(item.GetPropertyValue<Color>(item.Name));
			}
			return list;
		}
		public static void DrawCameraVision(PyList cameraVision)
		{
            // There are 40 x 22.5 tiles on the screen. Draw an ActiveFont for each with the value of cameraVision[i][j]
            _ = Color.Transparent;
            float y = 0;
			float width = 8;	
			float height = 8;
			int numColors = Colors.Count;
			using (Py.GIL())
			{
				for (int i = 0; i < cameraVision.Length(); i++, y += height)
				{
                    float x = 0;
                    for (int j = 0; j < cameraVision[i].Length(); j++, x += width)
					{
                        Color color = Color.Transparent;
                        color = cameraVision[i][j][0].As<int>() switch
                        {
                            (int)Entity.Unset => Color.Transparent,
                            (int)Entity.Air => Color.Blue * 0.5f,
                            (int)Entity.Tile => Color.Red * 0.5f,
                            (int)Entity.Madeline => Color.Pink * 0.5f,
                            (int)Entity.Target => Color.Yellow * 0.5f,
                            _ => Colors[cameraVision[i][j][0].As<int>() % numColors] * 0.5f,
                        };
                        Monocle.Draw.Rect(x, y, width, height, color);
					}
				}
			}
		}
    }
}