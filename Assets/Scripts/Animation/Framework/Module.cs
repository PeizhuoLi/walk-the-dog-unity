using UnityEngine;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AI4Animation {
	public abstract class Module : ScriptableObject {

		#if UNITY_EDITOR
		private static string[] Types = null;
		public static string[] GetTypes() {
			if(Types == null) {
				Types = Utility.GetAllDerivedTypesNames(typeof(Module));
			}
			return Types;
		}

		public MotionAsset Asset;
		public string Tag = string.Empty;
		public bool Precompute = true;
		public bool Callbacks = true;

		//Temporary
		// [NonSerialized] public static HashSet<string> Inspect = new HashSet<string>();
		// [NonSerialized] public static HashSet<string> Visualize = new HashSet<string>();

		void Awake() {
			// ResetPrecomputation();
		}

		void OnEnable() {
			if(Asset != null) {
				ResetPrecomputation();
			}
		}

		public TimeSeries.Component ExtractSeries(TimeSeries global, float timestamp, bool mirrored, params object[] parameters) {
			return DerivedExtractSeries(global, timestamp, mirrored, parameters);
		}

		public Module Initialize(MotionAsset asset, string tag) {
			Asset = asset;
			Tag = tag == null ? string.Empty : tag;
			ResetPrecomputation();
			// Debug.Log("Resetting precomputation in " + Asset.name + " during initialization");
			DerivedInitialize();
			return this;
		}

		public void Load(MotionEditor editor) {
			ResetPrecomputation();
			// Debug.Log("Resetting precomputation in " + Asset.name + " during load");
			DerivedLoad(editor);
		}

		public void Unload(MotionEditor editor) {
			DerivedUnload(editor);
		}

		public virtual void OnTriggerPlay(MotionEditor editor) {

		}

		public void Callback(MotionEditor editor) {
			if(Callbacks) {
				DerivedCallback(editor);
			}
		}

		public void GUI(MotionEditor editor) {
			if(editor.Visualize.Contains(GetID())) {
				TimeSeries.Component series = ExtractSeries(editor.GetTimeSeries(), editor.GetTimestamp(), editor.Mirror, editor);
				if(series != null) {
					series.GUI();
				}
				DerivedGUI(editor);
			}
		}

		public void Draw(MotionEditor editor) {
			if(editor.Visualize.Contains(GetID())) {
				TimeSeries.Component series = ExtractSeries(editor.GetTimeSeries(), editor.GetTimestamp(), editor.Mirror, editor);
				if(series != null) {
					series.Draw();
				}
				DerivedDraw(editor);
			}
		}

		public void Inspector(MotionEditor editor) {
			Utility.SetGUIColor(UltiDraw.DarkGrey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Utility.SetGUIColor(UltiDraw.Mustard);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();
					EditorGUILayout.BeginHorizontal();
					if(EditorGUILayout.Toggle(editor.Inspect.Contains(GetID()), GUILayout.Width(20f))) {
						editor.Inspect.Add(GetID());
					} else {
						editor.Inspect.Remove(GetID());
					}
					EditorGUILayout.LabelField(GetID().ToString());
					GUILayout.FlexibleSpace();
					if(Utility.GUIButton("Visualize", editor.Visualize.Contains(GetID()) ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black, 80f, 20f)) {
						if(editor.Visualize.Contains(GetID())) {
							editor.Visualize.Remove(GetID());
						} else {
							editor.Visualize.Add(GetID());
						}
					}
					if(Utility.GUIButton("Precompute", Precompute ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black, 80f, 20f)) {
						SetPrecomputable(!Precompute);
					}
					if(Utility.GUIButton("Callbacks", Callbacks ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black, 80f, 20f)) {
						Callbacks = !Callbacks;
					}
					if(Utility.GUIButton("X", UltiDraw.DarkRed, UltiDraw.White, 25f, 20f)) {
						Asset.RemoveModule(this);
					}
					EditorGUILayout.EndHorizontal();
				}

				if(editor.Inspect.Contains(GetID())) {
					Utility.SetGUIColor(UltiDraw.LightGrey);
					using(new EditorGUILayout.VerticalScope ("Box")) {
						Utility.ResetGUIColor();
						Tag = EditorGUILayout.TextField("Tag", Tag);
						EditorGUI.BeginChangeCheck();
						DerivedInspector(editor);
						if(EditorGUI.EndChangeCheck()) {
							Asset.ResetPrecomputation();
						}
					}
				}
			}
		}

		public void SetPrecomputable(bool value) {
			if(Precompute != value) {
				Precompute = value;
				ResetPrecomputation();
			}
		}

		public void ResetPrecomputation() {
			DerivedResetPrecomputation();
		}

		public string GetID() {
			return this.GetType().Name + ((Tag == string.Empty) ? string.Empty : (":" + Tag));
		}

		public abstract void DerivedResetPrecomputation();
		public abstract TimeSeries.Component DerivedExtractSeries(TimeSeries global, float timestamp, bool mirrored, params object[] parameters);
		protected abstract void DerivedInitialize();
		protected abstract void DerivedLoad(MotionEditor editor);	
		protected abstract void DerivedUnload(MotionEditor editor);
		protected abstract void DerivedCallback(MotionEditor editor);
		protected abstract void DerivedGUI(MotionEditor editor);
		protected abstract void DerivedDraw(MotionEditor editor);
		protected abstract void DerivedInspector(MotionEditor editor);
		
		//TODO: Precomputables are only created when loading session, but not when retrieving asset alone.
		[Serializable]
		public class Precomputable<T> {
			public int Padding;
			public int Length;

			public Module Module;
			public T[] Standard;
			public T[] Mirrored;
			public bool[] Visited;
			public bool[] MirroredVisited;
			
			public bool CheckValidity (Module module)
			{
				// It only checks some size values, not the actual values.
				if (Visited == null || MirroredVisited == null || Standard == null || Mirrored == null) return false;
				if (Padding != 2 * Mathf.RoundToInt(module.Asset.Framerate)) return false;
				if (Length != module.Asset.Frames.Length + 2 * Padding) return false;
				if (Module != module) return false;
				if (Visited.Length != Length || MirroredVisited.Length != Length) return false;
				return true;
			} 

			public Precomputable(Module module) {
				Padding = 2*Mathf.RoundToInt(module.Asset.Framerate);
				Length = module.Asset.Frames.Length + 2*Padding;

				Module = module;
				Standard = module.Precompute ? new T[Length] : null;
				Mirrored = module.Precompute ? new T[Length] : null;
				Visited = module.Precompute ? new bool[Length] : null;
				MirroredVisited = module.Precompute ? new bool[Length] : null;
			}

			public T Get(float timestamp, bool mirrored, Func<T> function) {
				int index = Mathf.RoundToInt(timestamp * Module.Asset.Framerate) + Padding;
				if(Module.Precompute && index >= 0 && index < Length) {
					if(mirrored && !MirroredVisited[index])
					{
						Mirrored[index] = function();
						MirroredVisited[index] = true;
					}
					if(!mirrored && !Visited[index])
					{
						Standard[index] = function();
						Visited[index] = true;
					}
					if(mirrored) {
						return Mirrored[index];
					}
					if(!mirrored) {
						return Standard[index];
					}
				}
				return function();
			}
		}
		#endif

	}
}