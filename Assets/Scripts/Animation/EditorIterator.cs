using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace AI4Animation
{
    public abstract class EditorIterator : EditorWindow
    {
        public static EditorWindow Window;
        protected MotionEditor Editor = null;
        private Task ProcessingTask;
        [NonSerialized] public bool Canceled = false;
        public int BatchSize = 100;
        protected bool NeedsEditor = true;
            
        [NonSerialized] public int Count = 0;
        [NonSerialized] public int Total = 0;
        [NonSerialized] private bool FirstDetectFinish = false;
        
        public abstract void DerivedOnGUI();
        public abstract Task Process();
        public virtual void DerivedCancel() {}
        
        private MotionEditor GetEditor()
        {
            if (Editor == null)
            {
                Editor = GameObjectExtensions.Find<MotionEditor>(true);
            }

            return Editor;
        }

        protected bool CheckAndResetCancel()
        {
            if (Canceled)
            {
                Canceled = false;
                return true;
            }

            return false;
        }

        void OnGUI()
        {
            if (NeedsEditor)
            {
                if (GetEditor() == null)
                {
                    EditorGUILayout.LabelField("No editor available in scene.");
                    return;
                }

                Editor = (MotionEditor)EditorGUILayout.ObjectField("Editor", Editor, typeof(MotionEditor), true);
            }

            BatchSize = EditorGUILayout.IntField("Batch Size", BatchSize);

            DerivedOnGUI();
            
            if (ProcessingTask is { IsCompleted: true })
            {
                if (ProcessingTask.IsFaulted)
                {
                    EditorGUILayout.LabelField("Error: " + ProcessingTask.Exception.Message);
                    if (FirstDetectFinish)
                        Debug.LogError(ProcessingTask.Exception);
                }
                else
                    EditorGUILayout.LabelField("Finished.");

                FirstDetectFinish = false;
            }
            
            if(ProcessingTask == null || ProcessingTask.IsCompleted) {
                if(Utility.GUIButton("Process", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Count = 0;
                    FirstDetectFinish = true;
                    ProcessingTask = Process();
                }
            } else {
                EditorGUILayout.LabelField($"Processing {Count}/{Total}...");
                if(Utility.GUIButton("Stop", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Canceled = true;
                    DerivedCancel();
                }
            }
        }
    }
}