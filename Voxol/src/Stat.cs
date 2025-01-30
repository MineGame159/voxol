using System.Numerics;

namespace Voxol;

public class Stat<T> where T : INumber<T>, IMinMaxValue<T> {
    private readonly T[] values;
    
    private int size;
    private int i;

    public int Size => values.Length;

    public Stat(int size) {
        this.values = new T[size];
    }

    public T Avg {
        get {
            var sum = T.Zero;

            for (var i = 0; i < size; i++) {
                sum += values[i];
            }

            return sum / T.CreateSaturating(size);
        }
    }

    public T Min {
        get {
            var min = T.MaxValue;

            for (var i = 0; i < size; i++) {
                min = T.Min(min, values[i]);
            }

            return min;
        }
    }

    public T Max {
        get {
            var min = T.MinValue;

            for (var i = 0; i < size; i++) {
                min = T.Max(min, values[i]);
            }

            return min;
        }
    }

    public void Add(T value) {
        values[i] = value;

        if (++size >= values.Length)
            size = values.Length;

        if (++i >= values.Length)
            i = 0;
    }

    public void GetHistoricData<TResult>(Span<TResult> data, Func<T, TResult> converter) {
        if (size == 0) {
            data.Clear();
            return;
        }
        
        var j = i - 1;
        
        for (var i = Size - 1; i >= 0; i--) {
            if (j < 0)
                j = Size - 1;
            
            data[i] = converter(values[j--]);
        }
    }
}