using System;
using System.IO;
using System.IO.Compression;
using NumSharp;
using UnityEngine;

public static class NumSharpExtensions
{
    public class NpzLoader
    {
        // Adapted from https://stackoverflow.com/a/75862760
        private readonly ZipArchive Archive;

        public NpzLoader(string path)
        {
            Archive = new ZipArchive(new FileStream(path, FileMode.Open));
        }

        public NDArray this[string index] => Get(index);

        private NDArray Get(string name)
        {
            Stream transStream = Archive.GetEntry($"{name}.npy").Open();
            return np.load(transStream);
        }

        public void Dispose()
        {
            Archive.Dispose();
        }
        
        ~NpzLoader()
        {
            Dispose();
        }
    }
    
    public static Vector2[] ToArrayVector2(this NDArray array)
    {
        if (array.ndim != 2 || array.shape[1] != 2)
            throw new ArgumentException("Array must be 2D with 2 columns");
        var result = new Vector2[array.shape[0]];
        for (int i = 0; i < array.shape[0]; i++)
        {
            result[i] = new Vector2(array[i, 0], array[i, 1]);
        }

        return result;
    }
    
    public static NDArray isclose(NDArray a, NDArray b)
    {
        var delta = a - b;
        var absDelta = np.abs(delta);
        return absDelta < 1e-5;
    }
}