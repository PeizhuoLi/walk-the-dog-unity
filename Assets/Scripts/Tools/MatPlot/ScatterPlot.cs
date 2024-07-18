using System;
using System.Collections.Generic;
using System.Linq;
using Accord.Statistics.Models.Markov.Learning;
using NumSharp;
using NumSharp.Generic;
using UnityEngine;
using UnityEngine.Assertions;


namespace MatPlot
{
    public class ScatterPlot
    {
        public UltiDraw.GUIRect Window;
        private const int bufferSize = 20;
        private Vector2[] points;
        private Vector3[] points3D;
        private Vector2[] additionalVertexInfo;
        private int[] indices;
        private Color[] colors;
        private int NumTotalPoints;
        private int NumBasePoints;
        [NonSerialized] private Mesh mesh;
        [NonSerialized] public Material pointMaterial;
        [NonSerialized] private Shader pointShader;

        public void Plot(NDArray p, Color[] c)
        {
            if (points == null)
            {
                Init(1000);
            }

            if (p.ndim == 1)
                p = p[np.newaxis];

            for (int i = 0; i < p.shape[0]; i++)
            {
                points[i + NumTotalPoints] = new Vector2(p[i, 0], p[i, 1]);
                colors[i + NumTotalPoints] = c[i];
            }

            NumTotalPoints += p.shape[0];
        }

        public Mesh GetMesh()
        {
            if (mesh == null)
            {
                mesh = new Mesh();
                points3D = new Vector3[points.Length];
                indices = new int[points.Length];
                for (var i = 0; i < points.Length; i++)
                    indices[i] = i;
                pointShader = Resources.Load<Shader>("Shaders/ScreenSpacePoints");
                if (pointShader == null)
                {
                    Debug.LogError("Shader not found: Shaders/ScreenSpacePoints");
                }
                pointMaterial = new Material(pointShader);
            }
            return mesh;
        }

        public void SetBasePoints()
        {
            NumBasePoints = NumTotalPoints;
        }

        public void Init(int size)
        {
            points = new Vector2[size + bufferSize];
            colors = new Color[size + bufferSize];
            additionalVertexInfo = new Vector2[size + bufferSize];
            Clear();
        }

        public int GetNumPoints()
        {
            return NumTotalPoints;
        }

        void Clear()
        {
            NumTotalPoints = NumBasePoints;
        }

        public void Plot(NDArray p, Color c)
        {
            Plot(p, Enumerable.Repeat(c, p.shape[0]).ToArray());
        }

        (Vector2, Vector2) Prepare2DNormalization(Vector2[] data)
        {
            var range = new Vector2(Window.W / 2, Window.H / 2);
            var mean = data.Mean();
            var scaling = new Vector2(0f, 0f);
            scaling = data.Aggregate(scaling, (current, t) => Vector2.Max(current, (t - mean).Positive()));
            scaling /= range;
            scaling *= 1.1f;
            return (mean, scaling);
        }

        public void Show(float size)
        {
            if (points == null) return;

            var (mean, scaling) = Prepare2DNormalization(points[..NumBasePoints]);

            for (var i = 0; i < NumTotalPoints; i++)
            {
                Vector2 p = points[i];
                p -= mean;
                p /= scaling;
                p += Window.GetCenter();
                UltiDraw.GUICircle(p, size, colors[i]);
            }
            
            Clear();
        }

        public void ShowWithMesh(float size)
        {
            if (points == null) return;
            
            var mesh = GetMesh();
            var (mean, scaling) = Prepare2DNormalization(points);

            for (var i = 0; i < NumTotalPoints; i++)
            {
                Vector2 p = points[i];
                p -= mean;
                p /= scaling;
                p += Window.GetCenter();
                points3D[i] = new Vector3(p.x, p.y, 0f);
                indices[i] = i;
                if (i < NumBasePoints)
                    additionalVertexInfo[i].x = 1f;
                else
                    additionalVertexInfo[i].x = 5f;
            }
            mesh.Clear();
            mesh.vertices = points3D[..NumTotalPoints];
            mesh.colors = colors[..NumTotalPoints];
            mesh.uv = additionalVertexInfo[..NumTotalPoints];
            mesh.SetIndices(indices[..NumTotalPoints], MeshTopology.Points, 0);
            pointMaterial.SetFloat("_PointSize", size);

            Clear();
        }
        
        public void OnPreCullCallBack(Camera cam)
        {
            Transform camTransform = cam.transform;
            float distToCenter = (cam.farClipPlane - cam.nearClipPlane) / 2.0f;
            Vector3 center = camTransform.position + camTransform.forward * distToCenter;
            float extremeBound = 500.0f;
            GetMesh().bounds = new Bounds (center, new Vector3(1f, 1f, 1f) * extremeBound);
        }

        public void InitializeGameObject(GameObject gameObject)
        {
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = GetMesh();
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = pointMaterial;
            meshRenderer.sortingOrder = 32762;
        }
    }
}