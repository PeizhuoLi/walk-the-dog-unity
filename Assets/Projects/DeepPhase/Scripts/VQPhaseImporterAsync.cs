#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NumSharp;

namespace AI4Animation {
	public class VQPhaseImporterAsync : EditorIterator {
        public string SequencePath = string.Empty;
		public string ManifoldPath = string.Empty;
        public string Tag = string.Empty;
        public int PresetSequence = 0;

        private string[] PresetSequenceList;
        private Dictionary<string, string> SequenceDictionary = new();
        private string DatasetPrefix = "";

        VQPhaseImporterAsync()
        {
	        BatchSize = 1000;

	        SequenceDictionary["Ogre-clean"] = $"Datasets/Mocha-ogre-clean/Sequences.txt";
	        SequenceDictionary["Clown-clean"] = $"Datasets/Mocha-clown-clean/Sequences.txt";
	        SequenceDictionary["Dog-gen2"] = $"Datasets/Dataset-dog-gen2/Sequences.txt";
	        SequenceDictionary["Human-loco-gen2"] = $"Datasets/Dataset-human-loco-gen2/Sequences.txt";
	        SequenceDictionary["Customize"] = "";
	        PresetSequenceList = SequenceDictionary.Keys.ToArray();
        }

        [MenuItem ("AI4Animation/Importer/VQ Phase Importer (Async)")]
		static void Init() {
			Window = EditorWindow.GetWindow(typeof(VQPhaseImporterAsync));
		}
		
        public void OnInspectorUpdate() {
            Repaint();
        }

        public override void DerivedOnGUI()
        {
	        DatasetPrefix = EditorGUILayout.TextField("Dataset Prefix", DatasetPrefix);
	        PresetSequence = EditorGUILayout.Popup("Preset Sequence", PresetSequence, PresetSequenceList);
	        if (PresetSequenceList[PresetSequence] != "Customize")
		        SequencePath = $"{DatasetPrefix}/{SequenceDictionary[PresetSequenceList[PresetSequence]]}";
	        SequencePath = EditorGUILayout.TextField("Sequence Path", SequencePath);
	        ManifoldPath = EditorGUILayout.TextField("Phase Path", ManifoldPath);
	        Tag = EditorGUILayout.TextField("Tag", Tag);
        }

        public override async Task Process() {
            StreamReader sequenceFile = new StreamReader(SequencePath);
            await Task.Yield();

            VQPhaseModule module = null;
            string currentGUID = string.Empty;

            var npz = new NumSharpExtensions.NpzLoader(ManifoldPath);

            var manifold = npz["manifold"];
            var phase = npz["phase"];
            var index = npz["index"].astype(np.int32);
            var state = npz["state"];
            var state_ori = npz["state_ori"];
            var manifold_ori = npz["manifold_ori"];

            if (index.ndim == 1) index = index[Slice.All, Slice.NewAxis];
            if (phase.ndim == 1) phase = phase[Slice.All, Slice.NewAxis];
            
            npz.Dispose();
            
            Count = 0;
            Total = manifold.shape[0];
            while(!sequenceFile.EndOfStream)
            {
	            if (CheckAndResetCancel()) break;
	            
                string sLine = sequenceFile.ReadLine();
            
                string[] tags = FileUtility.LineToArray(sLine, ' ');
                string fileGUID = tags[4];
                bool fileMirrored = tags[2] != "Standard";
                int fileFrame = tags[1].ToInt() - 1;
                
                if(currentGUID != fileGUID) {
	                if (module != null)
						EditorUtility.SetDirty(module);
	                
                    var asset = MotionAsset.Retrieve(fileGUID);
                    asset.RemoveModule<VQPhaseModule>(Tag);
                    module = asset.AddModule<VQPhaseModule>(Tag);
                    module.CreateArray(manifold.shape[1], state.shape[1], phase.shape[1], index.shape[1]);
                    currentGUID = fileGUID;
                    
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                if (!fileMirrored)
                {
	                module.RegularPhaseManifold.SetSlice(manifold[Count].ToArray<float>(), fileFrame);
	                module.RegularState.SetSlice(state[Count].ToArray<float>(), fileFrame);
	                module.RegularPhase.SetSlice(phase[Count].ToArray<float>(), fileFrame); 
	                module.RegularEmbedIndex.SetSlice(index[Count].ToArray<int>(), fileFrame);
	                module.RegularStateOri.SetSlice(state_ori[Count].ToArray<float>(), fileFrame);
	                module.RegularPhaseManifoldOri.SetSlice(manifold_ori[Count].ToArray<float>(), fileFrame);
                }
                else
                {
	                module.MirroredPhaseManifold.SetSlice( manifold[Count].ToArray<float>(), fileFrame);
					module.MirroredState.SetSlice(state[Count].ToArray<float>(), fileFrame);
					module.MirroredPhase.SetSlice(phase[Count].ToArray<float>(), fileFrame);
					module.MirroredEmbedIndex.SetSlice(index[Count].ToArray<int>(), fileFrame);
					module.MirroredStateOri.SetSlice(state_ori[Count].ToArray<float>(), fileFrame);
					module.MirroredPhaseManifoldOri.SetSlice(manifold_ori[Count].ToArray<float>(), fileFrame);
                }
            
                Count += 1;
                if(Count % BatchSize == 0) {
	                await Task.Yield();
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Resources.UnloadUnusedAssets();
        }
	}
}
#endif