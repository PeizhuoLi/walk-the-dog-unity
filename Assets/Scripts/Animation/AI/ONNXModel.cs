using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

[Serializable]
public class ONNXModel {
    public UnityEngine.Object Model;
    public string RuntimePath;
    // public bool GPU = false;

    private Inference Session = null;

    public void CreateSession() {
        #if UNITY_EDITOR
        if(Model != null) {
            Session = new Inference(AssetDatabase.GetAssetPath(Model));
        }
        #else
        Session = new Inference(Application.streamingAssetsPath + "/" + RuntimePath);
        #endif
    }

    public void CloseSession() {
        if(GetSession() != null) {
            Session.Instance.Dispose();
            Session = null;
        }
    }

    public Inference GetSession() {
        return Session;
    }

    public class Inference {
        public InferenceSession Instance;

        private Dictionary<string, List<float>> Inputs = new Dictionary<string, List<float>>();
        private Dictionary<string, List<float>> Outputs = null;

        private float Time = 0f;

        public Inference(string modelPath) {
            Debug.Log("Loading Model: " + modelPath);
            // if(gpu) {
            //     Instance = new InferenceSession(modelPath, SessionOptions.MakeSessionOptionWithCudaProvider();
            // } else {
                Instance = new InferenceSession(modelPath);
            // }
            // Debug.Log("Inputs: " + GetInputNames().Format());
            // Debug.Log("Outputs: " + GetOutputNames().Format());
        }

        public void Reset() {
            if(Inputs != null) {
                Inputs.Clear();
            }
            if(Outputs != null) {
                Outputs.Clear();
            }
        }

        public float GetTime() {
            return Time;
        }

        public List<float> GetInput(string name) {
            return Inputs[name];
        }

        public List<float> GetOutput(string name) {
            return Outputs[name];
        }

        public bool HasOutput(string name) {
            return Outputs.ContainsKey(name);
        }

        public string[] GetInputNames() {
            return new List<string>(Instance.InputNames).ToArray();
        }

        public string[] GetOutputNames() {
            return new List<string>(Instance.OutputNames).ToArray();
        }

        public void Feed(float value, string name="X") {
            if(!Inputs.ContainsKey(name)) {
                Inputs.Add(name, new List<float>());
            }
            Inputs[name].Add(value);
        }

        public void Feed(float[] values, string name="X") {
            foreach(float value in values) {
                Feed(value, name);
            }
        }

        public void Feed(int[] values, string name="X") {
            foreach(int value in values) {
                Feed(value, name);
            }
        }

        public void Feed(Vector3 vector, string name="X") {
            Feed(vector.x, name);
            Feed(vector.y, name);
            Feed(vector.z, name);
        }

        public void Feed(Vector2 vector, string name="X") {
            Feed(vector.x, name);
            Feed(vector.y, name);
        }

        public void FeedXY(Vector3 vector, string name="X") {
            Feed(vector.x, name);
            Feed(vector.y, name);
        }

        public void FeedXZ(Vector3 vector, string name="X") {
            Feed(vector.x, name);
            Feed(vector.z, name);
        }

        public void FeedYZ(Vector3 vector, string name="X") {
            Feed(vector.y, name);
            Feed(vector.z, name);
        }

        public void Feed(Quaternion vector, string name="X") {
            Feed(vector.x, name);
            Feed(vector.y, name);
            Feed(vector.z, name);
            Feed(vector.w, name);
        }

        public float Read(string name="Y") {
            if(!Outputs.ContainsKey(name)) {
                return 0f;
            }
            List<float> values = Outputs[name];
            float value = values[0];
            values.RemoveAt(0);
            return value;
        }

        public float[] ReadAll(string name="Y") {
            List<float> output = GetOutput(name);
            float[] values = output.ToArray();
            output.Clear();
            return values;
        }

        public float Read(float min, float max, string name="Y") {
            if(!Outputs.ContainsKey(name)) {
                return 0f;
            }
            List<float> values = Outputs[name];
            float value = values[0];
            values.RemoveAt(0);
            return Mathf.Clamp(value, min, max);
        }

        public float[] Read(int count, string name="Y") {
            float[] values = new float[count];
            for(int i=0; i<count; i++) {
                values[i] = Read(name);
            }
            return values;
        }

        public float[] Read(int count, float min, float max, string name="Y") {
            float[] values = new float[count];
            for(int i=0; i<count; i++) {
                values[i] = Read(min, max, name);
            }
            return values;
        }

        public float ReadBinary(string name="Y") {
            if(!Outputs.ContainsKey(name)) {
                return 0f;
            }
            List<float> values = Outputs[name];
            float value = values[0];
            values.RemoveAt(0);
            return value > 0.5f ? 1f : 0f;
        }

        public float[] ReadBinary(int count, string name="Y") {
            float[] values = new float[count];
            for(int i=0; i<count; i++) {
                values[i] = ReadBinary(name);
            }
            return values;
        }

        public Vector3 ReadVector3(string name="Y") {
            return new Vector3(Read(name), Read(name), Read(name));
        }

        public Vector3 ReadVector2(string name="Y") {
            return new Vector2(Read(name), Read(name));
        }

        public Vector3 ReadXY(string name="Y") {
            return new Vector3(Read(name), Read(name), 0f);
        }

        public Vector3 ReadXZ(string name="Y") {
            return new Vector3(Read(name), 0f, Read(name));
        }

        public Vector3 ReadYZ(string name="Y") {
            return new Vector3(0f, Read(name), Read(name));
        }

        public Quaternion ReadRotation2D(string name="Y") {
            Vector3 forward = ReadXZ(name).normalized;
            if(forward.magnitude == 0f) {
                forward = Vector3.forward;
            }
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        public Quaternion ReadRotation3D(string name="Y") {
            Vector3 forward = ReadVector3(name).normalized;
            Vector3 up = ReadVector3(name).normalized;
            if(forward.magnitude == 0f) {
                forward = Vector3.forward;
            }
            if(up.magnitude == 0f) {
                up = Vector3.up;
            }
            return Quaternion.LookRotation(forward, up);
        }

        public Matrix4x4 ReadMatrix2D(string name="Y") {
            return Matrix4x4.TRS(ReadXZ(name), ReadRotation2D(name), Vector3.one);
        }

        public Matrix4x4 ReadMatrix3D(string name="Y") {
            return Matrix4x4.TRS(ReadVector3(name), ReadRotation3D(name), Vector3.one);
        }

        public Matrix4x4 ReadRootDelta(string name="Y") {
            Vector3 offset = ReadVector3(name);
            return Matrix4x4.TRS(new Vector3(offset.x, 0f, offset.z), Quaternion.AngleAxis(offset.y, Vector3.up), Vector3.one);
        }

        public void Run() {
            DateTime timestamp = Utility.GetTimestamp();

            //Check Missed Read Data
            if(Outputs != null) {
                foreach(KeyValuePair<string, List<float>> entry in Outputs) {
                    if(entry.Value.Count > 0) {
                        Debug.Log("Output " + entry.Key + " had " + entry.Value.Count + " features remaining.");
                    }
                }
            }

            //Construct Inputs
            List<NamedOnnxValue> inputs = new List<NamedOnnxValue>();
            foreach(KeyValuePair<string, List<float>> entry in Inputs) {
                if(entry.Key == "X") {
                    Tensor<float> tensor = new DenseTensor<float>(new[]{1, entry.Value.Count});
                    for(int i=0; i<tensor.Length; i++) {
                        tensor[0,i] = entry.Value[i];
                    }
                    inputs.Add(NamedOnnxValue.CreateFromTensor(entry.Key, tensor));
                }
                if(entry.Key == "K") {
                    Tensor<float> tensor = new DenseTensor<float>(new[]{entry.Value.Count});
                    for(int i=0; i<tensor.Length; i++) {
                        tensor[i] = entry.Value[i];
                    }
                    inputs.Add(NamedOnnxValue.CreateFromTensor(entry.Key, tensor));
                }
            }

            //Run Inference
            List<DisposableNamedOnnxValue> predictions;
            try{
                predictions = Instance.Run(inputs) as List<DisposableNamedOnnxValue>;
                //Collect Outputs
                Outputs = new Dictionary<string, List<float>>();
                foreach(DisposableNamedOnnxValue prediction in predictions) {
                    Outputs.Add(prediction.Name, new List<float>(prediction.AsTensor<float>()));
                }
            } catch(Exception e) {
                Debug.Log(e);
            }

            //Reset Inference
            Inputs.Clear();

            Time = (float)Utility.GetElapsedTime(timestamp);
        }

        public float[] Run(float[] input, string name="X'") {
            Tensor<float> x = new DenseTensor<float>(new[]{1, input.Length});
            for(int i=0; i<x.Length; i++) {
                x[0,i] = input[i];
            }
            List<NamedOnnxValue> values = new List<NamedOnnxValue>();
            values.Add(NamedOnnxValue.CreateFromTensor(name, x));
            List<DisposableNamedOnnxValue> results = Instance.Run(values) as List<DisposableNamedOnnxValue>;
            Tensor<float> result = results.First().AsTensor<float>();
            float[] output = new float[result.Length];
            for(int i=0; i<result.Length; i++) {
                output[i] = result[0,i];
            }
            return output;
        }

        public List<float[]> Run(List<float[]> inputs, params string[] names) {
            List<NamedOnnxValue> values = new List<NamedOnnxValue>();

            for(int k=0; k<inputs.Count; k++) {
                Tensor<float> x = new DenseTensor<float>(new[]{1, inputs[k].Length});
                for(int i=0; i<x.Length; i++) {
                    x[0,i] = inputs[k][i];
                }
                values.Add(NamedOnnxValue.CreateFromTensor(names[k], x));
            }

            List<DisposableNamedOnnxValue> predictions = Instance.Run(values) as List<DisposableNamedOnnxValue>;

            List<float[]> results = new List<float[]>();
            foreach(DisposableNamedOnnxValue prediction in predictions) {
                Tensor<float> result = prediction.AsTensor<float>();
                float[] output = new float[result.Length];
                for(int i=0; i<result.Length; i++) {
                    output[i] = result.GetValue(i);
                }
                results.Add(output);
            }

            return results;
        }

        public List<float[]> Run(List<float[]> inputs, List<int[]> shapes, params string[] names) {
            List<NamedOnnxValue> values = new List<NamedOnnxValue>();

            for(int k=0; k<inputs.Count; k++) {
                Tensor<float> x = new DenseTensor<float>(new[]{1, inputs[k].Length});
                for(int i=0; i<x.Length; i++) {
                    x[0,i] = inputs[k][i];
                }
                values.Add(NamedOnnxValue.CreateFromTensor(names[k], x.Reshape(shapes[k])));
            }

            List<DisposableNamedOnnxValue> predictions = Instance.Run(values) as List<DisposableNamedOnnxValue>;

            List<float[]> results = new List<float[]>();
            foreach(DisposableNamedOnnxValue prediction in predictions) {
                Tensor<float> result = prediction.AsTensor<float>();
                float[] output = new float[result.Length];
                for(int i=0; i<result.Length; i++) {
                    output[i] = result.GetValue(i);
                }
                results.Add(output);
            }

            return results;
        }
    }

    #if UNITY_EDITOR
    public bool Inspector(string id=null) {
        EditorGUI.BeginChangeCheck();
        Utility.SetGUIColor(UltiDraw.White);
        using(new EditorGUILayout.VerticalScope ("Box")) {
            Utility.ResetGUIColor();
            if(id != null) {
                EditorGUILayout.LabelField(id);
            }
            Model = EditorGUILayout.ObjectField("Model", Model, typeof(UnityEngine.Object), true, GUILayout.Width(EditorGUIUtility.currentViewWidth - 30f)) as UnityEngine.Object;
            RuntimePath = EditorGUILayout.TextField("Runtime Path", RuntimePath);
        }
        return EditorGUI.EndChangeCheck();
    }
    #endif
}
