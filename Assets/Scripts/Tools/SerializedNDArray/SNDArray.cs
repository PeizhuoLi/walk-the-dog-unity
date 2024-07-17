using System;
using System.Collections.Generic;
using NumSharp;


namespace SerializedNDArray
{
    [Serializable]
    public class SNDArray<T> where T : unmanaged
    {
        public T[] data;
        public int[] shape;

        public T this[params int[] index]
        {
            set => data[GetIndex(index)] = value;
            get => data[GetIndex(index)];
        }
        
        public int Length => data.Length;

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)data).GetEnumerator();
        }

        public int GetIndex(params int[] index)
        {
            if (index.Length != shape.Length) throw new Exception("Index length does not match shape length");
            int idx = 0;
            for (int i = 0; i < index.Length; i++)
            {
                if (index[i] >= shape[i]) throw new Exception("Index out of range");
                idx *= shape[i];
                idx += index[i];
            }
            return idx;
        }

        public int GetSliceStartingIndex(params int[] index)
        {
            if (index.Length >= shape.Length) throw new Exception("Slice index length does not match shape length");
            int idx = 0;
            for (int i = 0; i < index.Length; i++)
            {
                if (index[i] >= shape[i]) throw new Exception("Slice index out of range");
                idx *= shape[i];
                idx += index[i];
            }
            for (int i = index.Length; i < shape.Length; i++) idx *= shape[i];
            return idx;
        }

        public int Size()
        {
            int size = 1;
            foreach (var v in shape) size *= v;
            return size;
        }
        
        public SNDArray(params int[] _shape)
        {
            shape = _shape;
            data = new T[Size()];
        }

        public SNDArray()
        {
            shape = new int[1];
            data = null;
        }

        public Span<T> AsSpan()
        {
            return data.AsSpan();
        }
        
        public static implicit operator Span<T> (SNDArray<T> array)
        {
            return array.data;
        }

        public void Squeeze()
        {
            int ndim = 0;
            int []newShape = new int[shape.Length];
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i] != 1) newShape[ndim++] = shape[i];
            }

            if (ndim == 0)
            {
                ndim = 1;
                newShape[0] = 1;
            }
            
            shape = new int[ndim];
            for (int i = 0; i < ndim; i++) shape[i] = newShape[i];
        }

        public void UnsqueezeLast()
        {
            int ndim = shape.Length + 1;
            int []newShape = new int[ndim];
            for (int i = 0; i < shape.Length; i++) newShape[i] = shape[i];
            newShape[ndim - 1] = 1;
            shape = newShape;
        }

        public SNDArray(NDArray array)
        {
            data = array.ToArray<T>();
            shape = array.shape;
        }

        public NDArray ToNDArray()
        {
            var array = np.array(data);
            array.reshape(shape);
            return array;
        }
        
        public int GetSlice(ref T[] buffer, params int[] index)
        {
            int size = 1;
            for (int i = index.Length; i < shape.Length; i++) size *= shape[i];
            if (buffer == null || buffer.Length < size) buffer = new T[size];
            int startingIndex = GetSliceStartingIndex(index);
            for (int i = 0; i < size; i++)
                buffer[i] = data[startingIndex + i];
            return size;
        }

        private int GetSliceSize(params int[] index)
        {
            int size = 1;
            for (int i = index.Length; i < shape.Length; i++) size *= shape[i];
            return size;
        }

        public Span<T> GetSliceSpan(params int[] index)
        {
            int startingIndex = GetSliceStartingIndex(index);
            int size = GetSliceSize(index);
            return new Span<T>(data, startingIndex, size);
        }
        
        public int GetSlice(Span<T> buffer, params int[] index)
        {
            int size = GetSliceSize(index);
            int startingIndex = GetSliceStartingIndex(index);
            for (int i = 0; i < size; i++)
                buffer[i] = data[startingIndex + i];
            return size;
        }
        
        public T[] GetSlice(params int[] index)
        {
            T[] buffer = null;
            GetSlice(ref buffer, index);
            return buffer;
        }

        public void SetSlice(T[] value, params int[] index)
        {
            int size = 1;
            for (int i = index.Length; i < shape.Length; i++) size *= shape[i];
            if (value.Length != size) throw new Exception("Slice size does not match");
            int startingIndex = GetSliceStartingIndex(index);
            for (int i = 0; i < size; i++) 
                data[startingIndex + i] = value[i];
        }
    }
}