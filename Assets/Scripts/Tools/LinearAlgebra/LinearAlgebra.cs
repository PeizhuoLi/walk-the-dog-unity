using System;
using NumSharp;
using MathNet.Numerics.LinearAlgebra;
using NumSharp.Generic;


public static class MatrixExtensions
{
    public static Matrix<T> ToMatrix<T> (this NDArray<T> array) where T : unmanaged, IEquatable<T>, IFormattable
    {
        var shape = array.shape;
        if (shape.Length != 2) throw new Exception("Array must be 2D");
        return Matrix<T>.Build.DenseOfRowMajor(shape[0], shape[1], array.ToArray<T>());
    }
    
    public static Matrix<float> ToMatrix (this NDArray array)
    {
        return array.AsGeneric<float>().ToMatrix();
    }

    public static NDArray<T> ToNDArray<T>(this Matrix<T> mat) where T : unmanaged, IEquatable<T>, IFormattable
    {
        var data = mat.ToArray();
        return np.array<T>(data).AsOrMakeGeneric<T>();
    }
}


namespace LinAlg
{
    public static class LinearAlgebra
    {
        // Currently all linera algebra operation is done with Float32
        public class PCA
        {
            private int n_componets;
            private NDArray projection;
            private NDArray basePoints;
            private NDArray mean, std;

            public void fit(NDArray data_raw)
            {
                int n_dim = data_raw.shape[^1];
                var data = data_raw.Clone();
                var n_samples = data.shape[0];
                mean = data.mean(0, keepdims: true);
                std = data.std(0, keepdims: true);
                data -= mean;
                data /= std;

                var covMat = np.matmul(data.T, data) / (n_samples - 1);
                var cov = covMat.ToMatrix();

                var eig = cov.Evd();
                var eigenVectors = eig.EigenVectors;

                projection = eigenVectors.ToNDArray().T;
                projection = projection[$":, :{n_componets}"];
            }
            
            public PCA(int n)
            {
                n_componets = n;
            }
            
            public NDArray transform(NDArray data)
            {
                data = (data - mean) / std;
                return np.matmul(data, projection);
            }
        }
        
    }
}