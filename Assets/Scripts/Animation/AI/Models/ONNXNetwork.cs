using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Sentis;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AI4Animation {
    [Serializable]
    public class ONNXNetwork : NeuralNetwork {

        public ModelAsset Model = null;
        public BackendType Backend = BackendType.CPU;

        public class ONNXInference : Inference
        {

            private Dictionary<string, Tensor> X = null;
            private TensorFloat Y = null;
            public Model Model = null;
            public IWorker Engine = null;
            
            public string[] InputNames => Model.inputs.Select(x => x.name).ToArray();
            public List<string> OutputNames => Model.outputs;
            public bool RequiresNoise => Model.inputs.Any(x => x.name == "noise");

            public ONNXInference(ModelAsset model, BackendType backend) {
                if(model == null) {
                    Debug.Log("No model has been assigned.");
                    return;
                }
                Model = ModelLoader.Load(model);
                Engine = WorkerFactory.CreateWorker(backend, Model);
                X = new Dictionary<string, Tensor>();
                FeedPivots = new Dictionary<string, int>();
                foreach (var input in Model.inputs)
                {
                    if (input.shape.IsFullyKnown())
                        X[input.name] = TensorFloat.Zeros(input.shape.ToTensorShape());
                    else
                        X[input.name] = null;
                    FeedPivots[input.name] = 0;
                }
                Y = null;
            }
            public override void Dispose() {
                if(X != null) {
                    foreach (var x in X)
                    {
                        x.Value.Dispose();
                    }
                }
                if(Engine != null) {
                    Engine.Dispose();
                }
            }

            public override void ResetXShape(TensorShape inputShape, string name)
            {
                if (X[name] != null)
                    X[name].Dispose();
                X[name] = TensorFloat.Zeros(inputShape);
            }

            public override int GetFeedSize(string name="input")
            {
                return X[name].shape.length;
            }

            public TensorShape GetFeedShape(string name)
            {
                return X[name].shape;
            }

            public override int GetReadSize() {
                if(Y == null) {
                    Debug.Log("Run inference first to obtain output read size.");
                    return 0;
                }
                return Y.shape.length;
            }

            public override void Feed(float value, string name) {
                ((TensorFloat)X[name])[FeedPivots[name]] = value;
            }

            public override float Read()
            {
                throw new NotImplementedException("Not implemented for Sentis backend.");
                // if(Y == null) {
                //     Debug.Log("Run inference first to obtain output values.");
                //     return 0f;
                // }
                // return Y[ReadPivot];
            }

            public void MakeReadable()
            {
                foreach (var key in X.Keys)
                {
                    X[key].MakeReadable();
                }
            }

            public override void Run() { 
                //Single Input
                Engine.Execute(X);
                foreach (var name in FeedPivots.Keys.ToArray())
                    FeedPivots[name] = 0;
                ReadPivot = 0;
                MakeReadable();
            }
        }

        public TensorFloat[] GetOutputs() {
            if(GetSession() != null) {
                ONNXInference session = (ONNXInference)GetSession();
                TensorFloat[] outputs = new TensorFloat[session.Model.outputs.Count];
                for(int i=0; i<outputs.Length; i++) {
                    outputs[i] = session.Engine.PeekOutput(session.Model.outputs[i]) as TensorFloat;
                    outputs[i].MakeReadable();
                }
                return outputs;
            }
            return null;
        }

        public TensorFloat GetOutput(string name) {
            if(GetSession() != null) {
                ONNXInference session = (ONNXInference)GetSession();
                if(session.Model.outputs.Contains(name))
                {
                    var output = session.Engine.PeekOutput(name) as TensorFloat;
                    output.MakeReadable();
                    return output;
                } else {
                    Debug.Log("Output with name " + name + " is invalid.");
                }
            }
            return null;
        }

        protected override Inference BuildInference()
        {
            return new ONNXInference(Model, Backend);
        }

        #if UNITY_EDITOR
        public override void Inspect() {
            Model = EditorGUILayout.ObjectField("Model", Model, typeof(ModelAsset), true) as ModelAsset;
            Backend = (BackendType)EditorGUILayout.EnumPopup("Backend", Backend);
        }
        #endif

    }
}
