using Celeste;
using Celeste.Mod;
using CelesteBot_2023.SimplifiedGraphics;
using Microsoft.Xna.Framework;
using Monocle;
using Python.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static CelesteBot_2023.CelesteBotInteropModule;

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
        public static void Draw()
		{
			DrawMetricStrings();
			try
			{
				if (CelesteBotInteropModule.BotState != CelesteBotInteropModule.State.None)
					DrawCameraVision(CelesteBotInteropModule.AIPlayer.cameraManager.CameraVision);
			}
            catch (NullReferenceException)
			{
                // Do nothing
            }
		}
		public static void DrawMetricStrings()
		{
            string[] metrics = new string[6];
            metrics[0] = "Latest Episode Reward: " + CelesteBotInteropModule.AIPlayer.LastEpisodeReward.ToString("F2");
            metrics[1] = "Current Episode Reward: " + CelesteBotInteropModule.AIPlayer.Episode.TotalReward.ToString("F2");
			metrics[2] = "Last Action Reward" + CelesteBotInteropModule.AIPlayer.Episode.LastActionReward.ToString("F2");
            metrics[3] = "Distance from target: " + CelesteBotInteropModule.AIPlayer.Episode.LastDistanceFromTarget.ToString("F2");
            if (LatestAction != null)
                metrics[4] = "Last Action: " + CelesteBotInteropModule.LatestAction.ToString();
			metrics[5] = "\nAction Retrieval Status " + CelesteBotInteropModule.ActionRetrievalStatus;
            float width = 600;
            float height = 15;
            float x = SCREEN_WIDTH  - width;
			float y = 0;
			Color color = Color.Black * 0.1f;
			for (int i = 0; i < metrics.Length; i++, y+=30)
			{
                Monocle.Draw.Rect(x, y, width, height, color);
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
			Color color = Color.Transparent;
			float x = 0;
			float y = 0;
			float width = 8;	
			float height = 8;
			int numColors = Colors.Count;
			using (Py.GIL())
			{
				for (int i = 0; i < cameraVision.Length(); i++, y += height)
				{
					x = 0;
					for (int j = 0; j < cameraVision[i].Length(); j++, x += width)
					{
						color = Color.Transparent;
						switch (cameraVision[i][j][0].As<int>())
						{
							case (int)Entity.Unset:
                                color = Color.Transparent;
                                break;	
							case (int)Entity.Air:
								color = Color.Blue * 0.5f;
								break;
							case (int)Entity.Tile:
								color = Color.Red * 0.5f;
								break;
                            case (int)Entity.Madeline:
                                color = Color.Pink * 0.5f; 
                                break;
							default:
								color = Colors[cameraVision[i][j][0].As<int>() % numColors] * 0.5f;
								break;
						}
						Monocle.Draw.Rect(x, y, width, height, color);
					}
				}
			}
		}
    }
}