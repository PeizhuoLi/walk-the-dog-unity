#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AI4Animation
{
    public class DataExporterAsync : BatchProcessorAsync
    {
        private List<List<float>> ToSave;
        private string ExportingPath = GetExportPath();
        private string ExportingMode = "PhaseManifold,Velocities,Positions,Rotations";
        private BinaryWriter BinWriter;
        private StreamWriter DescriptionWriter;
        private StreamWriter SequenceWriter;
        private int ColumnCount = -1;
        private int NumSequences;
        private int NumFrames;
        private FeatureExtractor.CHANNEL_TYPE[] ChannelTypes;
        private string PhaseTag = string.Empty;
        private MotionEditor Editor;
        private List<int> ColumnList;
        private string[] BoneNames;
        private int[] BoneMapping;
        private bool UseButterworth = false;
        private bool UseButterworthVelocityOnly = false;
        private int ItemBatchSize = 100;

        private string SequenceInfo;
        
        [MenuItem ("AI4Animation/Tools/Data Exporter (Async)")]
        static void Init() {
            Window = EditorWindow.GetWindow(typeof(DataExporterAsync));
            Scroll = Vector2.zero;
        }

        DataExporterAsync()
        {
            BatchSize = 5;
        }
        
        public MotionEditor GetEditor() {
            if(Editor == null) {
                Editor = GameObjectExtensions.Find<MotionEditor>(true);
            }
            return Editor;
        }
        
        public static string GetExportPath() {
            string path = Application.dataPath;
            path = path.Substring(0, path.LastIndexOf("/"));
            path = path.Substring(0, path.LastIndexOf("/"));
            path += "/DeepLearning/Dataset";
            return path;
        }
        
        public override bool CanProcess()
        {
            return true;
        }

        public override void DerivedInspector()
        {
            Editor = EditorGUILayout.ObjectField("Editor", Editor, typeof(MotionEditor), true) as MotionEditor;
            ExportingPath = EditorGUILayout.TextField("Exporting Path", ExportingPath);
            ExportingMode = EditorGUILayout.TextField("Exporting Mode", ExportingMode);
            PhaseTag = EditorGUILayout.TextField("Phase Tag", PhaseTag);
            BatchSize = EditorGUILayout.IntField("Batch Size", BatchSize);
            ItemBatchSize = EditorGUILayout.IntField("Item Batch Size", ItemBatchSize);
            UseButterworth = EditorGUILayout.Toggle("Use Butterworth", UseButterworth);
            UseButterworthVelocityOnly = EditorGUILayout.Toggle("Use Butterworth Velocity Only", UseButterworthVelocityOnly);
            
            if(Utility.GUIButton("Refresh", UltiDraw.DarkGrey, UltiDraw.White)) {
                LoadItems(GetEditor().Assets.ToArray());
            }
        }

        public override void DerivedStart()
        {
            ToSave = new List<List<float>>();
            SequenceInfo = string.Empty;
            Directory.CreateDirectory(ExportingPath);
            if (BinWriter != null)
                BinWriter.Close();
            BinWriter = new BinaryWriter(File.Open(ExportingPath + "/Data.bin", FileMode.Create));
            if (DescriptionWriter != null)
                DescriptionWriter.Close();
            DescriptionWriter = new StreamWriter(File.Open(ExportingPath + "/Description.txt", FileMode.Create));
            DescriptionWriter.WriteLine(ExportingMode);
            if (SequenceWriter != null)
                SequenceWriter.Close();
            SequenceWriter = new StreamWriter(File.Open(ExportingPath + "/Sequences.txt", FileMode.Create));
            
            ColumnCount = -1;
            NumFrames = 0;
            NumSequences = 0;
            
            var channelToExport = ExportingMode.Split(',');
            ChannelTypes = new FeatureExtractor.CHANNEL_TYPE[channelToExport.Length];
            for (int i = 0; i < channelToExport.Length; i++)
            {
                ChannelTypes[i] = Enum.Parse<FeatureExtractor.CHANNEL_TYPE>(channelToExport[i]);
            }

            BoneNames = null;
            BoneMapping = null;
        }

        public void FlushWriters()
        {
            foreach (var l in ToSave)
            {
                foreach (var v in l)
                {
                    BinWriter.Write(v);
                }
            }

            ToSave = new List<List<float>>();
            SequenceWriter.Write(SequenceInfo);
            SequenceInfo = string.Empty;
        }
        
        private string GetSequenceInfo(int sequence, int frame, bool mirrored, MotionAsset asset) {
            //Sequence - Frame - Mirroring - Name - GUID
            var toWrite = new List<string>
            {
                sequence.ToString(), frame.ToString(), mirrored ? "Mirrored" : "Standard",
                asset.name, Utility.GetAssetGUID(asset)
            };
            return string.Join(' ', toWrite);
        }

        public override async Task DerivedProcess(Item item)
        {
            var motionAsset = MotionAsset.Retrieve(item.ID);
            var actor = GetEditor().GetSession().GetActor();
            var featureExtractor = new FeatureExtractor
            {
                Asset = motionAsset
            };
            
            Debug.Log($"Processing #{item.Index} {motionAsset.name}");

            if (BoneNames == null)
                BoneNames = actor.GetBoneNames();
            if (BoneMapping == null)
                BoneMapping = motionAsset.Source.GetBoneIndices(BoneNames);

            if (!motionAsset.Export)
                return;
            
            for (int s = 0; s < motionAsset.Sequences.Length; s++)
            {
                for (int itMirror = 0; itMirror < 2; itMirror++)
                {
                    var mirrored = itMirror == 1;
                    NumSequences += 1;
                    for (int i = motionAsset.Sequences[s].Start; i <= motionAsset.Sequences[s].End; i++)
                    {
                        int localColumnCount = 0;
                        var columnList = new List<int>();
                        var oneRow = new List<float>();
                        NumFrames += 1;
                        
                        foreach (var channelType in ChannelTypes)
                        {
                            float[] newBuffer = null;
                            featureExtractor.Get(mirrored, channelType, PhaseTag, motionAsset.Frames[i - 1], actor,
                                ref newBuffer);
                            if (newBuffer == null)
                            {
                                Debug.Log("Something wrong...");
                            }

                            localColumnCount += newBuffer.Length;
                            oneRow.AddRange(newBuffer.ToList());
                            columnList.Add(newBuffer.Length);
                        }

                        if (ColumnCount == -1)
                        {
                            ColumnCount = localColumnCount;
                            ColumnList = columnList;
                        }
                        else
                        {
                            if (ColumnCount != localColumnCount)
                            {
                                Debug.LogError("Column count mismatch");
                            }
                        }

                        ToSave.Add(oneRow);
                        SequenceInfo += GetSequenceInfo(NumSequences, i, mirrored, motionAsset) + "\n";
                        
                        if (NumFrames % ItemBatchSize == 0)
                            await Task.Yield();
                    }

                    if (UseButterworth)
                    {
                        var buffer = new float[ToSave.Count];
                        for (int j = 0; j < ColumnCount; j++)
                        {
                            for (int i = 0; i < ToSave.Count; i++) buffer[i] = ToSave[i][j];
                            float[] res = Utility.Butterworth(buffer, motionAsset.GetDeltaTime(), 3.25f);
                            for (int i = 0; i < ToSave.Count; i++) ToSave[i][j] = res[i];
                        }
                    }

                    if (UseButterworthVelocityOnly)
                    {
                        var buffer = new float[ToSave.Count];
                        for (int j = 0; j < ColumnCount / 2; j++)
                        {
                            for (int i = 0; i < ToSave.Count; i++) buffer[i] = ToSave[i][j];
                            float[] res = Utility.Butterworth(buffer, motionAsset.GetDeltaTime(), 3.25f);
                            for (int i = 0; i < ToSave.Count; i++) ToSave[i][j] = res[i];
                        }
                    }
                    
                    FlushWriters();
                }
            }
            
            EditorUtility.SetDirty(motionAsset); 
            // Save Precomputed values for faster data exporting.
        }

        public override void DerivedFinish()
        {
            FlushWriters();
            BinWriter.Close();
            BinWriter = null;
            DescriptionWriter.WriteLine(string.Join(',', ColumnList));
            DescriptionWriter.WriteLine(NumFrames);
            DescriptionWriter.WriteLine(string.Join(',', BoneNames));
            DescriptionWriter.WriteLine(string.Join(',', BoneMapping));
            DescriptionWriter.WriteLine(Mathf.RoundToInt(Editor.GetSession().Asset.Framerate));
            DescriptionWriter.Close();
            DescriptionWriter = null;
            SequenceWriter.Close();
            SequenceWriter = null;
        }

        public override void DerivedRefresh()
        {
            
        }

        public override void BatchCallback()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Resources.UnloadUnusedAssets();
        }

        public override void DerivedInspector(Item item)
        {
            
        }

        public override string GetID(Item item)
        {
            return Utility.GetAssetName(item.ID);
        }
    }
}
#endif