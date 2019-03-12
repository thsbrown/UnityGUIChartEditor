﻿/**
 * Copyright (c) 2019 Alessandro Tironi
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLRDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USER OR
 * OTHER DEALINGS IN THE SOFTWARE.
 */

using System.Collections.Generic;
using UnityEngine;

namespace Syrus.Plugins.ChartEditor
{
	public static class GUIChartEditor
	{
		static GUIChartEditor()
		{
			if (sprites == null)
				sprites = new GUIChartEditorSprites();
		}

		/// <summary>
		/// The current instance of the drawed chart.
		/// </summary>
		internal static ChartInstance CurrentChart { get; private set; }

		/// <summary>
		/// A generic function y = f(x).
		/// </summary>
		/// <param name="x">The function parameter (X coordinate on the chart).</param>
		/// <returns>The Y value computed as y = f(x).</returns>
		public delegate float ChartFunction(float x);

		/// <summary>
		/// Collects all the required sprites.
		/// </summary>
		internal static GUIChartEditorSprites sprites = null;

		/// <summary>
		/// Starts drawing a new chart.
		/// </summary>
		/// <param name="layoutRect">The <see cref="Rect"/> used as layout.</param>
		/// <param name="backgroundColor">The color used for the background.</param>
		/// <param name="options">A set of options for customizing the chart.</param>
		public static void BeginChart(Rect layoutRect, Color backgroundColor, params ChartOption[] options)
		{
			// Creates new instance of a chart.
			CurrentChart = new ChartInstance(layoutRect, backgroundColor);

			// Default material
			CurrentChart.material = new Material(Shader.Find("Hidden/Internal-Colored"));
			
			// Applies options.
			var optionsList = new List<ChartOption>(options);
			optionsList.Sort(new ChartOptionComparer());
			foreach (var option in optionsList)
				option.ApplyOption();
		}

		/// <summary>
		/// Draws a rectangular clip used as background for a chart in the current inspector
		/// position.
		/// </summary>
		/// <param name="minWidth">The rect minimum width.</param>
		/// <param name="maxWidth">The rect maximum width.</param>
		/// <param name="minHeight">The rect minimum height.</param>
		/// <param name="maxHeight">The rect maximum height.</param>
		/// <param name="backgroundColor">The color of the background.</param>
		/// <param name="options">A set of options to customize the chart.</param>
		public static void BeginChart(float minWidth, float maxWidth, float minHeight, float maxHeight,
			Color backgroundColor, params ChartOption[] options)
		{
			Rect layoutRect = GUILayoutUtility.GetRect(minWidth, maxWidth, minHeight, maxHeight);
			BeginChart(layoutRect, backgroundColor, options);
		}

		/// <summary>
		/// Pushes a new line chart in the current drawed chart.
		/// </summary>
		/// <param name="points">The set of points that form the graph.</param>
		/// <param name="lineColor">The color of the line.</param>
		public static void PushLineChart(Vector2[] points, Color lineColor)
		{
			ChartInstance.Line newLine;
			newLine.lineColor = lineColor;
			if (points.Length <= 1)
				return;
			newLine.points = new Vector2[points.Length + points.Length - 2];
			int j = 0;
			for (int i = 0; i < points.Length - 1; i++)
			{
				Vector2 p = CurrentChart.coordinatesProcessor(points[i].x, points[i].y);
				Vector2 p1 = CurrentChart.coordinatesProcessor(points[i + 1].x, points[i + 1].y);
				newLine.points[j++] = p;
				newLine.points[j++] = p1;
			}
			CurrentChart.lineQueue.Enqueue(newLine);
		}

		/// <summary>
		/// Pushes a point in the current drawed chart.
		/// </summary>
		/// <param name="points">The set of points that form the graph.</param>
		/// <param name="lineColor">The color of the line.</param>
		public static void PushPoint(Vector2 point, Color pointColor)
		{
			ChartInstance.Point p;
			p.pointColor = pointColor;
			p.point = CurrentChart.coordinatesProcessor(point.x, point.y);
			CurrentChart.pointQueue.Enqueue(p);
		}

		/// <summary>
		/// Draws a label representing a float number in the provided position.
		/// </summary>
		/// <param name="value">The number to draw on the chart.</param>
		/// <param name="x">The X position of the label (in user space).</param>
		/// <param name="y">The Y position of the label (in user space).</param>
		/// <param name="floatFormat">The format to apply when converting float to string.</param>
		public static void PushValueLabel(float value, float x, float y, string floatFormat = "0.00")
		{
			Vector2 coords = CurrentChart.coordinatesProcessor(x, y);
			string textFloat = value.ToString(floatFormat).Replace(',', '.');
			var requiredTextures = sprites.GetTextures(textFloat, (int)coords.x, (int)coords.y);
			foreach (var tex in requiredTextures)
				CurrentChart.textureQueue.Enqueue(tex);
		}

		/// <summary>
		/// Plots a function defined by a lambda inside the provided interval.
		/// </summary>
		/// <param name="function">The function to plot.</param>
		/// <param name="min">The left bound of the interval.</param>
		/// <param name="max">The right bound of the interval.</param>
		/// <param name="functionColor">The color of the plot.</param>
		/// <param name="step">The sample step. Default will compute f(x) on each pixel.</param>
		public static void PushFunction(ChartFunction function, float min, float max,
			Color functionColor, float step = -1f)
		{
			if (min < CurrentChart.minX)
				min = CurrentChart.minX;
			if (max > CurrentChart.maxX)
				max = CurrentChart.maxX;

			// Computes the default step value.
			if (step < 0)
				step = CurrentChart.userDefinedRect.width / CurrentChart.pixelSizeRect.width;

			List<Vector2> samples = new List<Vector2>();
			for (float x = min; x < max; x += step)
			{
				float y = function(x);
				if (y > CurrentChart.maxY)
				{
					// If the function exits the layout rect from above then find
					// the intersection point and break loop.
					float xPrev = x - step;
					float yPrev = function(xPrev);
					float xMiddle = (x - xPrev) * ((xPrev / (x - xPrev)) + 
						((CurrentChart.maxY - yPrev) / (y - yPrev)));
					samples.Add(new Vector2(xMiddle, function(xMiddle)));
					break;
				}
				if (y < CurrentChart.minY)
				{
					// If the function exits the layout rect from below then
					// find the next point. If f(next) > minY then find the intersection
					// with current point and draw the half-segment. Otherwise continue.
					float xNext = x + step;
					float yNext = function(xNext);
					if (xNext < max && yNext >= CurrentChart.minY)
					{
						float xMiddle = (xNext - x) * ((x / (xNext - x)) +
							((CurrentChart.minY - y) / (yNext - y)));
						samples.Add(new Vector2(xMiddle, function(xMiddle)));
						x += step;
					}
				}
				else
					samples.Add(new Vector2(x, y));
			}
			PushLineChart(samples.ToArray(), functionColor);
		}

		/// <summary>
		/// Terminates the graph drawing process.
		/// </summary>
		public static void EndChart()
		{
			if (CurrentChart.outputTexture != null || Event.current.type == EventType.Repaint)
			{
				if (CurrentChart.outputTexture == null)
					GUI.BeginClip(CurrentChart.pixelSizeRect);
				GL.PushMatrix();
				if (CurrentChart.outputTexture != null)
					GL.LoadPixelMatrix(0, CurrentChart.outputTexture.width, 
						CurrentChart.outputTexture.height, 0);

				// Clear the current render buffer.
				GL.Clear(true, false, Color.black);
				CurrentChart.material.SetPass(0);

				// Draws the background.
				GL.Begin(GL.QUADS);
				GL.Color(Color.black);
				GL.Vertex3(0, 0, 0);
				GL.Vertex3(CurrentChart.pixelSizeRect.width, 0, 0);
				GL.Vertex3(CurrentChart.pixelSizeRect.width, CurrentChart.pixelSizeRect.height, 0);
				GL.Vertex3(0, CurrentChart.pixelSizeRect.height, 0);
				GL.End();

				// Draws the axes if needed.
				if (CurrentChart.showAxes)
				{
					ChartInstance.Line x, y;
					(x.lineColor, y.lineColor) = (CurrentChart.axesColor, CurrentChart.axesColor);
					Vector2[] xPoints = new Vector2[]
					{
						new Vector2(CurrentChart.minX, 0f),
						new Vector2(CurrentChart.maxX, 0f)
					};
					Vector2[] yPoints = new Vector2[]
					{
						new Vector2(0f, CurrentChart.minY),
						new Vector2(0f, CurrentChart.maxY)
					};
					PushLineChart(xPoints, CurrentChart.axesColor);
					PushLineChart(yPoints, CurrentChart.axesColor);
				}

				// Draws the points.
				GL.Begin(GL.QUADS);
				while (CurrentChart.pointQueue.Count > 0)
				{
					ChartInstance.Point p = CurrentChart.pointQueue.Dequeue();
					GL.Color(p.pointColor);
					GL.Vertex3(p.point.x - 2, p.point.y - 2, 0);
					GL.Vertex3(p.point.x + 2, p.point.y - 2, 0);
					GL.Vertex3(p.point.x + 2, p.point.y + 2, 0);
					GL.Vertex3(p.point.x - 2, p.point.y + 2, 0);
				}
				GL.End();

				// Draws the lines.
				GL.Begin(GL.LINES);
				while (CurrentChart.lineQueue.Count > 0)
				{
					ChartInstance.Line l = CurrentChart.lineQueue.Dequeue();
					GL.Color(l.lineColor);
					foreach (Vector2 p in l.points)
						GL.Vertex3(p.x, p.y, 0);
				}
				GL.End();

				// Draws textures.
				Material guiTexMat = new Material(Shader.Find("Hidden/Internal-GUITexture"));
				while (CurrentChart.textureQueue.Count > 0)
				{
					GL.Begin(GL.QUADS);
					var tex = CurrentChart.textureQueue.Dequeue();
					guiTexMat.SetTexture("_MainTex", tex.texture);
					guiTexMat.SetPass(0);
					GL.TexCoord2(0, 1);
					GL.Vertex3(tex.rect.x, tex.rect.y, 0);
					GL.TexCoord2(0, 0);
					GL.Vertex3(tex.rect.x, tex.rect.y + tex.rect.height, 0);
					GL.TexCoord2(1, 0);
					GL.Vertex3(tex.rect.x + tex.rect.width, tex.rect.y + tex.rect.height, 0);
					GL.TexCoord2(1, 1);
					GL.Vertex3(tex.rect.x + tex.rect.width, tex.rect.y, 0);
					GL.End();
				}

				GL.PopMatrix();
				if (CurrentChart.outputTexture == null)
					GUI.EndClip();

				// Draw graph on the output texture if needed.
				if (CurrentChart.outputTexture != null)
				{
					RenderTexture rt = RenderTexture.active;
					CurrentChart.outputTexture.ReadPixels(new Rect(0, 0,
						CurrentChart.outputTexture.width, CurrentChart.outputTexture.height), 0, 0);
					CurrentChart.outputTexture.Apply(false); // do not apply mipmaps
					CurrentChart.outputTexture.Compress(true); // compress with high quality
					RenderTexture.active = null;
					RenderTexture.ReleaseTemporary(rt);
				}
			}
		}
	}
}