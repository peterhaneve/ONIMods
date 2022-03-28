/*
 * Copyright 2022 Peter Han
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 * BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using PeterHan.PLib.Core;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Extension methods make life easier!
	/// </summary>
	public static class ExtensionMethods {
		/// <summary>
		/// Appends the time slice unit (like "/s") to the string buffer. Allocates less than
		/// a string concatenation.
		/// </summary>
		/// <param name="buffer">The string builder to append.</param>
		/// <param name="timeSlice">The time slice unit to use.</param>
		/// <returns>The string builder.</returns>
		public static StringBuilder AppendTimeSlice(this StringBuilder buffer,
				GameUtil.TimeSlice timeSlice) {
			switch (timeSlice) {
			case GameUtil.TimeSlice.PerSecond:
				buffer.Append(STRINGS.UI.UNITSUFFIXES.PERSECOND);
				break;
			case GameUtil.TimeSlice.PerCycle:
				buffer.Append(STRINGS.UI.UNITSUFFIXES.PERCYCLE);
				break;
			}
			return buffer;
		}

		/// <summary>
		/// Creates a GameObject to render meshes using a MeshRenderer.
		/// </summary>
		/// <param name="targetMesh">The mesh to be rendered.</param>
		/// <param name="name">The object's name.</param>
		/// <param name="layer">The layer on which the mesh will be rendered.</param>
		/// <returns>The game object to use for rendering.</returns>
		public static GameObject CreateMeshRenderer(this Mesh targetMesh, string name,
				int layer) {
			if (targetMesh == null)
				throw new ArgumentNullException(nameof(targetMesh));
			var go = new GameObject(name ?? "Mesh Renderer", typeof(MeshRenderer), typeof(
					MeshFilter)) {
				layer = layer
			};
			// Set up the mesh with the right material
			var renderer = go.GetComponent<MeshRenderer>();
			renderer.allowOcclusionWhenDynamic = false;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			renderer.receiveShadows = false;
			renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			// Set the mesh to render
			var filter = go.GetComponent<MeshFilter>();
			filter.sharedMesh = targetMesh;
			return go;
		}

		/// <summary>
		/// Generates a getter for a type that is not known at compile time. The getter will
		/// be emitted as a non-type checked function that accepts an object and blindly
		/// attempts to retrieve the field type. Use with caution!
		/// 
		/// Value types will be copied when using this method.
		/// </summary>
		/// <typeparam name="D">The field type to return.</typeparam>
		/// <param name="type">The containing type of the field.</param>
		/// <param name="fieldName">The field name.</param>
		/// <returns>A delegate that can access that field.</returns>
		public static Func<object, D> GenerateGetter<D>(this Type type, string fieldName) {
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrEmpty(fieldName))
				throw new ArgumentNullException(nameof(fieldName));
			var field = type.GetField(fieldName, PPatchTools.BASE_FLAGS | BindingFlags.
				Instance | BindingFlags.Static);
			if (field == null)
				throw new ArgumentException("No such field: {0}.{1}".F(type.FullName,
					fieldName));
			if (!typeof(D).IsAssignableFrom(field.FieldType))
				throw new ArgumentException("Field type {0} does not match desired {1}".F(
					field.FieldType.FullName, typeof(D).FullName));
			var getter = new DynamicMethod(fieldName + "_GetDelegate", typeof(D), new Type[] {
				typeof(object)
			}, true);
			var generator = getter.GetILGenerator();
			// Getter will load the first argument and use ldfld/ldsfld
			if (field.IsStatic)
				generator.Emit(OpCodes.Ldsfld, field);
			else {
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Ldfld, field);
			}
			generator.Emit(OpCodes.Ret);
#if DEBUG
			PUtil.LogDebug("Created delegate for field {0}.{1} with type {2}".
				F(type.FullName, fieldName, typeof(D).FullName));
#endif
			return getter.CreateDelegate(typeof(Func<object, D>)) as Func<object, D>;
		}

		/// <summary>
		/// Generates a setter for a type that is not known at compile time. The setter will
		/// be emitted as a non-type checked function that accepts an object and blindly
		/// attempts to set the field type. Use with caution!
		/// 
		/// Value types will be copied when using this method.
		/// </summary>
		/// <typeparam name="D">The field type to modify.</typeparam>
		/// <param name="type">The containing type of the field.</param>
		/// <param name="fieldName">The field name.</param>
		/// <returns>A delegate that can access that field.</returns>
		public static Action<object, D> GenerateSetter<D>(this Type type, string fieldName) {
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrEmpty(fieldName))
				throw new ArgumentNullException(nameof(fieldName));
			var field = type.GetField(fieldName, PPatchTools.BASE_FLAGS | BindingFlags.
				Instance | BindingFlags.Static);
			if (field == null)
				throw new ArgumentException("No such field: {0}.{1}".F(type.FullName,
					fieldName));
			if (!field.FieldType.IsAssignableFrom(typeof(D)))
				throw new ArgumentException("Field type {0} does not match desired {1}".F(
					field.FieldType.FullName, typeof(D).FullName));
			var setter = new DynamicMethod(fieldName + "_SetDelegate", null, new Type[] {
				typeof(object), typeof(D)
			}, true);
			var generator = setter.GetILGenerator();
			// Setter will load the first argument and use stfld/stsfld
			if (field.IsStatic) {
				generator.Emit(OpCodes.Ldarg_1);
				generator.Emit(OpCodes.Stsfld, field);
			} else {
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Ldarg_1);
				generator.Emit(OpCodes.Stfld, field);
			}
			generator.Emit(OpCodes.Ret);
#if DEBUG
			PUtil.LogDebug("Created delegate for field {0}.{1} with type {2}".
				F(type.FullName, fieldName, typeof(D).FullName));
#endif
			return setter.CreateDelegate(typeof(Action<object, D>)) as Action<object, D>;
		}

		/// <summary>
		/// Gets the elapsed time in microseconds.
		/// </summary>
		/// <param name="ticks">The time elapsed in stopwatch ticks.</param>
		/// <returns>The elapsed time in microseconds.</returns>
		public static long TicksToUS(this long ticks) {
			return ticks * 1000000L / System.Diagnostics.Stopwatch.Frequency;
		}

		/// <summary>
		/// Converts a float to a standard string like ONI would, but with less memory used.
		/// </summary>
		/// <param name="f">The value to format.</param>
		/// <returns>The value formatted like ONI wants it for display.</returns>
		public static string ToStandardString(this float f) {
			string result;
			float absF = Mathf.Abs(f);
			if (f == 0f)
				result = "0";
			else if (absF < 1f)
				result = f.ToString("#,##0.#");
			else if (absF < 10f)
				result = f.ToString("#,###.#");
			else
				result = f.ToString("N0");
			return result;
		}
	}
}
