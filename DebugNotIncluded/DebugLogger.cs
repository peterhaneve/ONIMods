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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Provides log functions for this mod.
	/// </summary>
	public static class DebugLogger {
		/// <summary>
		/// The header prepended to each log message from this mod.
		/// </summary>
		public const string HEADER = "[DebugNotIncluded] ";

		/// <summary>
		/// The handler for Unity exception messages.
		/// </summary>
		internal static WrapLogHandler Handler { get; private set; }

		/// <summary>
		/// Inserts the calling method into the debug log output.
		/// </summary>
		/// <param name="message">The original message.</param>
		/// <param name="caller">The stack trace when inside the patch.</param>
		/// <returns>The new message.</returns>
		internal static string AddCallingLocation(string message, StackTrace caller) {
			int n = caller.FrameCount;
			for (int i = 0; i < n; i++) {
				Type type;
				var frame = caller.GetFrame(i);
				var method = frame.GetMethod();
				// Valid method?
				if (method != null && (type = method.DeclaringType) != null) {
					string typeName = type.Name, methodName = method.Name;
					if (type == typeof(DebugLogger)) break;
					// Do not add info to messages from this mod
					if ((typeName != nameof(PUtil) && type != typeof(DebugUtil)) ||
							!methodName.StartsWith("Log", StringComparison.Ordinal)) {
						var declaring = type.DeclaringType;
						bool isGenerated = typeName.Length > 0 && typeName[0] == '<';
						if (methodName == ".ctor")
							methodName = "new";
						else if (isGenerated)
							// Method captured as anonymous class
							methodName = methodName.StripCompilerGenerated(typeName);
						else {
							// Strip the patch suffix
							int index = methodName.IndexOf("_Patch", StringComparison.
								Ordinal);
							if (index > 0)
								methodName = methodName.Substring(0, index);
						}
						if (methodName.Length > 0 && methodName[0] == '<')
							// Non-captured method as anonymous method
							methodName = methodName.StripCompilerGenerated(methodName);
						// Remove compiler generated delegate classes
						if (isGenerated && declaring != null)
							typeName = declaring.Name;
						// Found the caller
						message = string.Format("[{0}] [{2}.{3}|{1:D}] ", GetTimeStamp(),
							Thread.CurrentThread.ManagedThreadId, typeName, methodName);
						break;
					}
				}
			}
			return message;
		}

		/// <summary>
		/// Adds stack frame information to the crash log.
		/// </summary>
		/// <param name="method">The method referenced in the stack frame.</param>
		/// <param name="frame">The stack frame where the crash occurred.</param>
		/// <param name="message">The location where the message will be stored.</param>
		private static void AddStackFrame(MethodBase method, StackFrame frame,
				StringBuilder message) {
			// Try to give as much debug info as possible
			int line = frame.GetFileLineNumber(), chr = frame.GetFileColumnNumber();
			message.Append("  at ");
			method = DebugUtils.GetOriginalMethod(method);
			DebugUtils.AppendMethod(message, method);
			if (line > 0 || chr > 0)
				message.AppendFormat(" ({0:D}, {1:D})", line, chr);
			else
				message.AppendFormat(" [{0:D}]", frame.GetILOffset());
			// The blame game
			var type = method.DeclaringType;
			if (type == null || type.IsBaseGameType())
				message.Append(" <Klei>");
			else {
				var asm = type.Assembly;
				ModDebugInfo mod;
				if (asm == typeof(string).Assembly)
					message.Append(" <mscorlib>");
				else if ((mod = ModDebugRegistry.Instance.OwnerOfType(type)) != null) {
					message.Append(" <");
					message.Append(mod.ModName ?? "unknown");
					message.Append(">");
				} else if (asm.FullName.IndexOf("Unity", StringComparison.Ordinal) >= 0)
					message.Append(" <Unity>");
			}
			message.AppendLine();
			DebugUtils.GetPatchInfo(method, message);
		}

		/// <summary>
		/// Logs the exception using the default handler.
		/// </summary>
		/// <param name="e">The exception thrown.</param>
		/// <param name="context">The context of the exception.</param>
		internal static void BaseLogException(Exception e, UnityEngine.Object context) {
			if (Handler == null)
				UnityEngine.Debug.LogException(e, context);
			else
				Handler.Wrapped.LogException(e, context);
		}

		/// <summary>
		/// Dumps the current stack trace to the log.
		/// </summary>
		public static void DumpStack() {
			var message = new StringBuilder(1024);
			message.AppendLine("Stack trace:");
			// Better stack traces!
			GetStackTraceLog(new StackTrace(1), message);
			LogWarning(message.ToString());
			message.Clear();
		}

		/// <summary>
		/// Gets the log message for the specified exception.
		/// </summary>
		/// <param name="e">The exception, which must not be null.</param>
		/// <returns>The log message for this exception.</returns>
		private static string GetExceptionLog(Exception e) {
			// Better breakdown of the stack trace
			var message = new StringBuilder(8192);
			if (e is ReflectionTypeLoadException loadException) {
				message.AppendLine("Exception(s) when loading types:");
				foreach (var cause in loadException.LoaderExceptions)
					if (cause != null)
						message.Append(GetExceptionLog(cause));
			} else {
				var stackTrace = new StackTrace(e);
				message.AppendFormat("{0}: {1}", e.GetType().Name, e.Message ??
					"<no message>");
				message.AppendLine();
				ModLoadHandler.CrashingMod = stackTrace.GetFirstModOnCallStack();
				GetStackTraceLog(stackTrace, message);
				// Log the root cause
				var cause = e.GetBaseException();
				if (cause != e) {
					message.AppendLine("Root cause exception:");
					message.Append(GetExceptionLog(cause));
				}
			}
			return message.ToString();
		}

		/// <summary>
		/// Gets the log message for the specified stack trace.
		/// </summary>
		/// <param name="stackTrace">The stack trace, which must not be null.</param>
		/// <param name="message">The location where the message will be stored.</param>
		internal static void GetStackTraceLog(StackTrace stackTrace, StringBuilder message) {
			int n = stackTrace.FrameCount;
			for (int i = 0; i < n; i++) {
				var frame = stackTrace.GetFrame(i);
				if (frame != null) {
					var method = frame.GetMethod();
					if (method == null)
						method = HarmonyLib.Harmony.GetMethodFromStackframe(frame);
					if (method != null)
						AddStackFrame(method, frame, message);
				}
			}
		}

		/// <summary>
		/// Gets the current time stamp in the format the ONI log usually uses.
		/// </summary>
		/// <returns>The UTC time formatted as a time stamp.</returns>
		internal static string GetTimeStamp() {
			return System.DateTime.UtcNow.ToString("HH:mm:ss.fff");
		}

		/// <summary>
		/// Wraps the default debug log handler, providing better debug support of exceptions.
		/// </summary>
		internal static void InstallExceptionLogger() {
			var logger = UnityEngine.Debug.unityLogger;
			if (logger != null) {
				logger.logHandler = Handler = new WrapLogHandler(logger.logHandler);
#if DEBUG
				LogDebug("Installed exception handler for Debug.LogException");
#endif
			} else
				Handler = null;
		}
		
		/// <summary>
		/// Logs a debug message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogDebug(string message) {
			Debug.Log(HEADER + message);
		}

		/// <summary>
		/// Logs a debug message with format arguments.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The format string arguments,</param>
		public static void LogDebug(string message, params object[] args) {
			Debug.LogFormat(HEADER + message, args);
		}

		/// <summary>
		/// Logs an error message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogError(string message) {
			// Avoid duplicate messages by replicating the Debug log statement
			UnityEngine.Debug.LogErrorFormat("[{0}] [{1}] [ERROR] {2}{3}", GetTimeStamp(),
				Thread.CurrentThread.ManagedThreadId, HEADER, message);
		}

		/// <summary>
		/// Logs an error message with format arguments.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The format string arguments,</param>
		public static void LogError(string message, params object[] args) {
			LogError(string.Format(message, args));
		}

		/// <summary>
		/// Logs an exception with a detailed breakdown.
		/// </summary>
		/// <param name="e">The exception to log.</param>
		public static void LogException(Exception e) {
			// Unwrap target invocation exceptions
			while (e is TargetInvocationException tie)
				e = tie.InnerException;
			if (e == null)
				LogError("<null>");
			else
				try {
					LogError(GetExceptionLog(e));
				} catch {
					// Ensure it gets logged at all costs
					BaseLogException(e, null);
					throw;
				}
		}

		/// <summary>
		/// Logs an exception with a detailed breakdown. This overload is used in KMonoBehavior
		/// transpilers.
		/// </summary>
		/// <param name="e">The exception to log.</param>
		internal static void LogKMonoException(Exception e) {
			var cause = e.InnerException ?? e;
			try {
				LogError((e.Message ?? "Error in KMonoBehaviour:") + Environment.NewLine +
					GetExceptionLog(cause));
			} catch {
				// Ensure it gets logged at all costs
				BaseLogException(cause, null);
				throw;
			}
		}

		/// <summary>
		/// Logs a warning message.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogWarning(string message) {
			Debug.LogWarning(HEADER + message);
		}

		/// <summary>
		/// Logs a warning message with format arguments.
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="args">The format string arguments,</param>
		public static void LogWarning(string message, params object[] args) {
			Debug.LogWarningFormat(HEADER + message, args);
		}

		/// <summary>
		/// Logs a failed assertion that is about to occur.
		/// </summary>
		internal static void OnAssertFailed(bool condition) {
			if (!condition) {
				var message = new StringBuilder(1024);
				var trace = new StackTrace(2);
				message.AppendLine("An assert is about to fail:");
				// Better stack traces!
				ModLoadHandler.CrashingMod = trace.GetFirstModOnCallStack();
				GetStackTraceLog(trace, message);
				LogError(message.ToString());
				message.Clear();
			}
		}
	}
}
