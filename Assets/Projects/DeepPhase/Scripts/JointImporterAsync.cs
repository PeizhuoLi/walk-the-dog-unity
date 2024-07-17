#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading.Tasks;

namespace AI4Animation {
	public class JointImporterAsync : EditorIterator {
		public string ManifoldPath = string.Empty;
        public string Tag = string.Empty;
        public int PresetSequence = 0;

        private string[] PresetSequenceList;
        private Dictionary<string, string> SequenceDictionary = new();
        private Dictionary<string, string> OnnxPathMapping = new();
        private Dictionary<string, string> VQPathMapping = new();
        private Dictionary<string, string[]> CharacterNamesMapping = new();
        private Dictionary<string, string[]> DatasetNamesMapping = new();
        private string DatasetPrefix = "";
        [NonSerialized] private EditorIterator SubProcess = null;

        JointImporterAsync()
        {
	        BatchSize = 1000;
	        NeedsEditor = false;

	        var datasetNames0 = new[] { "Dog-gen2", "Human-loco-gen2" };
	        var datasetNames1 = new[] { "Clown-clean", "Ogre-clean" };
	        var characterNames0 = new[] { "dog", "human" };
	        var characterNames1 = new[] { "clown", "ogre" };
	        
	        DatasetNamesMapping["Dog and Human"] = datasetNames0;
	        DatasetNamesMapping["Mocha"] = datasetNames1;
	        
	        CharacterNamesMapping["Dog and Human"] = characterNames0;
	        CharacterNamesMapping["Mocha"] = characterNames1;

	        PresetSequenceList = new[] {"Dog and Human", "Mocha"};

	        ManifoldPath = "~/Downloads/";
	        
	        SequenceDictionary["Ogre-clean"] = "Mocha-ogre-clean/Sequences.txt";
	        SequenceDictionary["Clown-clean"] = "Mocha-clown-clean/Sequences.txt";
	        SequenceDictionary["Dog-gen2"] = "Dataset-dog-gen2/Sequences.txt";
	        SequenceDictionary["Human-loco-gen2"] = "Dataset-human-loco-gen2/Sequences.txt";

	        OnnxPathMapping["dog"] = "Assets/Projects/DeepPhase/Demos/Quadruped/ONNX/";
	        OnnxPathMapping["human"] = "Assets/Projects/DeepPhase/Demos/Biped/ONNX/";
	        OnnxPathMapping["clown"] = "Assets/Projects/DeepPhase/Demos/Mocha/ONNX/";
	        OnnxPathMapping["ogre"] = "Assets/Projects/DeepPhase/Demos/Mocha/ONNX/";

	        VQPathMapping["Dog and Human"] = "Assets/Projects/DeepPhase/Demos/Retargeting/VQs/";
	        VQPathMapping["Mocha"] = "Assets/Projects/DeepPhase/Demos/Mocha/VQs/";
        }

        [MenuItem ("AI4Animation/Importer/Joint (Async) %#i")]
		static void Init() {
			Window = EditorWindow.GetWindow(typeof(JointImporterAsync));
		}
		
        public void OnInspectorUpdate() {
            Repaint();
        }

        public override void DerivedCancel()
        {
	        if (SubProcess != null)
	        {
		        SubProcess.Canceled = true;
		        SubProcess.DerivedCancel();
	        }
        }

        async Task ImportVQPhase(string sequencePath, string manifoldPath, string tag)
        {
	        if (Canceled) return;
	        var importer = CreateInstance<VQPhaseImporterAsync>();
	        importer.SequencePath = sequencePath;
	        importer.ManifoldPath = manifoldPath;
	        importer.Tag = tag;
	        await Task.Yield();
	        var task = importer.Process();
	        SubProcess = importer;
	        await task;
	        if (!task.IsCompletedSuccessfully) 
		        Debug.LogError(task.Exception);
	        SubProcess = null;
	        DestroyImmediate(importer);
        }

        public override void DerivedOnGUI()
        {
	        PresetSequence = EditorGUILayout.Popup("Preset Sequence", PresetSequence, PresetSequenceList);
	        ManifoldPath = EditorGUILayout.TextField("Phase Path", ManifoldPath);
	        DatasetPrefix = EditorGUILayout.TextField("Dataset Prefix", DatasetPrefix);
	        Tag = EditorGUILayout.TextField("Tag", Tag);
	        if (SubProcess != null)
            {
	            EditorGUILayout.LabelField($"Sub Process: {SubProcess.Count}/{SubProcess.Total}...");
            }
        }

        public override async Task Process()
        {
	        var presetString = PresetSequenceList[PresetSequence];
	        
	        if (!ManifoldPath.EndsWith('/')) ManifoldPath += '/';
	        if (Tag == string.Empty)
	        {
		        Tag = ManifoldPath[^8..^1];
		        Tag = Tag.Replace('/', '-');
	        }

	        var datasetNames = DatasetNamesMapping[presetString];
	        var characterNames = CharacterNamesMapping[presetString];
	        Total = datasetNames.Length + 2;
	        Count = 1;
	        for (int i = 0; i < datasetNames.Length; i++)
	        {
		        if (Canceled) break;
		        await ImportVQPhase($"{DatasetPrefix}/{SequenceDictionary[datasetNames[i]]}", ManifoldPath + $"Manifolds_{i}_final.npz", Tag);
		        Count++;
	        }

	        int localCount = 0;
	        for (int i = 0; i < characterNames.Length; i++)
	        {
		        if (Canceled) break;
		        var files = Directory.GetFiles(ManifoldPath);
		        foreach (var file in files)
		        {
			        if (file.EndsWith(".onnx") && file.Contains(characterNames[i]) && (localCount & (1 << i)) == 0)
			        {
				        var targetFilename = file.Split('/')[^1];
				        if (!targetFilename.Contains(Tag)) targetFilename = Tag + "-" + targetFilename;
				        File.Copy(file, OnnxPathMapping[characterNames[i]] + targetFilename, true);
				        localCount |= 1 << i;
			        }
		        }
		        Count++;
	        }
	        
	        if (CheckAndResetCancel()) return;
	        
	        File.Copy(ManifoldPath + "VQ.npz", VQPathMapping[presetString] + Tag + ".npz", true);
	        Count++;
	        
	        if (localCount != (1 << characterNames.Length) - 1) 
		        Debug.LogError($"Could not finish copying ONNX files, exist with count {localCount}");
        }
	}
}
#endif