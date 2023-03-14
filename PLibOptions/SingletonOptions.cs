/*
 * Copyright 2023 Peter Han
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

namespace PeterHan.PLib.Options {
	/// <summary>
	/// A class which can be used by mods to maintain a singleton of their options. This
	/// class should be the superclass of the mod options class, and &lt;T&gt; should be
	/// the type of the options class to store.
	/// 
	/// This class only initializes the mod options once by default. If the settings can
	/// be updated without restarting the game, update the Instance manually using
	/// IOptions.OnOptionsChanged. If the game has to be restarted anyways, add
	/// [RestartRequired].
	/// </summary>
	/// <typeparam name="T">The mod options class to wrap.</typeparam>
	public abstract class SingletonOptions<T> where T : class, new() {
		/// <summary>
		/// The only instance of the singleton options.
		/// </summary>
		protected static T instance;

		/// <summary>
		/// Retrieves the program options, or lazily initializes them if not yet loaded.
		/// </summary>
		public static T Instance {
			get {
				if (instance == null)
					instance = POptions.ReadSettings<T>() ?? new T();
				return instance;
			}
			protected set {
				if (value != null)
					instance = value;
			}
		}
	}
}
