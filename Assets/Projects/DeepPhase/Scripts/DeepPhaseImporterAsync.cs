#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Threading.Tasks;

namespace AI4Animation {
	public class DeepPhaseImporterAsync : EditorWindow {

		public static EditorWindow Window;
		public static Vector2 Scroll;

        public string SequencePath = string.Empty;
        public string PhasePath = string.Empty;
        public string Tag = string.Empty;

		private Task ProcessingTask;
		private bool Processing = false;
		private int Count = 0;
        private int BatchSize = 1000;

		[MenuItem ("AI4Animation/Importer/Deep Phase Importer (Async)")]
		static void Init() {
			Window = EditorWindow.GetWindow(typeof(DeepPhaseImporterAsync));
			Scroll = Vector3.zero;
		}
		
        public void OnInspectorUpdate() {
            Repaint();
        }

		void OnGUI() {
			Scroll = EditorGUILayout.BeginScrollView(Scroll);

			Utility.SetGUIColor(UltiDraw.Black);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Utility.SetGUIColor(UltiDraw.Grey);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();

					Utility.SetGUIColor(UltiDraw.Mustard);
					using(new EditorGUILayout.VerticalScope ("Box")) {
						Utility.ResetGUIColor();
						EditorGUILayout.LabelField(this.GetType().ToString());
					}
					
                    SequencePath = EditorGUILayout.TextField("Sequence Path", SequencePath);
                    PhasePath = EditorGUILayout.TextField("Phase Path", PhasePath);
                    Tag = EditorGUILayout.TextField("Tag", Tag);
                    
                    if (!Processing && ProcessingTask is { IsCompleted: true })
                    {
	                    if (ProcessingTask.IsFaulted)
		                    EditorGUILayout.LabelField("Error: " + ProcessingTask.Exception.Message);
	                    else
		                    EditorGUILayout.LabelField("Finished");
                    }

                    if(!Processing) {
                        if(Utility.GUIButton("Process", UltiDraw.DarkGrey, UltiDraw.White))
                        {
	                        Processing = true;
	                        ProcessingTask = Process();
                        }
                    } else {
                        EditorGUILayout.LabelField("Read " + Count + " lines...");
                        if(Utility.GUIButton("Stop", UltiDraw.DarkGrey, UltiDraw.White))
                        {
	                        Processing = false;
                        }
                    }
				}
			}

			EditorGUILayout.EndScrollView();
		}

        public async Task Process() {
            StreamReader phaseFile = new StreamReader(PhasePath);
            StreamReader sequenceFile = new StreamReader(SequencePath);
            await Task.Yield();

            DeepPhaseModule module = null;
            string currentGUID = string.Empty;

            Count = 0;
            while(ProcessingTask != null && !sequenceFile.EndOfStream)
            {
	            if (!Processing) break;
	            
                string sLine = sequenceFile.ReadLine();
                string pLine = phaseFile.ReadLine();

                string[] tags = FileUtility.LineToArray(sLine, ' ');
                float[] features = FileUtility.LineToFloat(pLine, ' ');
                string fileGUID = tags[4];
                bool fileMirrored = tags[2] == "Standard" ? false : true;
                int fileFrame = tags[1].ToInt();
                int channels = features.Length / 4;
                if(currentGUID != fileGUID) {
                    var asset = MotionAsset.Retrieve(fileGUID);
                    asset.RemoveModule<DeepPhaseModule>(Tag);
                    module = asset.AddModule<DeepPhaseModule>(Tag);
                    module.CreateChannels(channels);
                    currentGUID = fileGUID;
                }

                for(int i=0; i<channels; i++) {
                    float phaseValue = Mathf.Repeat(features[0*channels+i], 1f);
                    float frequency = features[1*channels+i];
                    float amplitude = features[2*channels+i];
                    float offset = features[3*channels+i];
                    if(fileMirrored) {
                        module.Channels[i].MirroredPhaseValues[fileFrame-1] = phaseValue;
                        module.Channels[i].MirroredFrequencies[fileFrame-1] = frequency;
                        module.Channels[i].MirroredAmplitudes[fileFrame-1] = amplitude;
                        module.Channels[i].MirroredOffsets[fileFrame-1] = offset;
                    } else {
                        module.Channels[i].RegularPhaseValues[fileFrame-1] = phaseValue;
                        module.Channels[i].RegularFrequencies[fileFrame-1] = frequency;
                        module.Channels[i].RegularAmplitudes[fileFrame-1] = amplitude;
                        module.Channels[i].RegularOffsets[fileFrame-1] = offset;
                    }
                }

                Count += 1;
                if(Count % BatchSize == 0) {
	                await Task.Yield();
                }
            }

            Processing = false;
        }
	}
}
#endif