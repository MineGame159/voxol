using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Voxol;

public static class Utils {
    public static unsafe T* AsPtr<T>(ReadOnlySpan<T> span) where T : unmanaged => (T*) Unsafe.AsPointer(ref Unsafe.AsRef(in span.GetPinnableReference()));
    public static unsafe T* AsPtr<T>(Span<T> span) where T : unmanaged => (T*) Unsafe.AsPointer(ref span.GetPinnableReference());
    public static unsafe T* AsPtr<T>(T[] array) where T : unmanaged => (T*) Unsafe.AsPointer(ref array.AsSpan().GetPinnableReference());

    public static ulong SizeOf<T>() => (ulong) Unsafe.SizeOf<T>();

    public static T Align<T>(T offset, T alignment) where T : IBinaryInteger<T> => (offset + (alignment - T.One)) & ~(alignment - T.One);

    public static int NonNullCount<T>(ReadOnlySpan<T?> span) {
        var count = 0;

        foreach (var item in span) {
            if (item != null)
                count++;
        }
        
        return count;
    }
    
    public static float DegToRad(float deg) => deg * (MathF.PI / 180);
    public static float RadToDeg(float rad) => rad * (180 / MathF.PI);

    public static void Wrap(Result result, string message) {
        if (result != Result.Success)
            throw new Exception(message + ": " + result);
    }

    public static byte[] ReadAllBytes(Stream stream) {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        
        return ms.ToArray();
    }

    public static string FormatBytes(ulong bytes) {
        if (bytes / 1024.0 < 1) return $"{bytes} b";
        if (bytes / 1024.0 / 1024.0 < 1) return $"{bytes / 1024.0:F1} kB";
        if (bytes / 1024.0 / 1024.0 / 1024.0 < 1) return $"{bytes / 1024.0 / 1024.0:F1} mB";
        
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} gB";
    }

    public static unsafe QueueIndices GetQueueIndices(Vk vk, PhysicalDevice physicalDevice) {
        var count = 0u;
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref count, null);

        Span<QueueFamilyProperties> families = stackalloc QueueFamilyProperties[(int) count];
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref count, AsPtr(families));

        uint? graphics = null;

        for (var i = 0; i < count; i++) {
            var props = families[i];

            if (props.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                graphics = (uint) i;
        }

        return new QueueIndices(graphics);
    }

    public readonly record struct QueueIndices(uint? Graphics) {
        public bool Valid => Graphics.HasValue;
    }
}

public static class EnumerableMethods {
    public static T? FirstNullable<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : struct {
        return source
            .Where(predicate)
            .Select(T? (item) => item)
            .FirstOrDefault();
    }
}

// ReSharper disable once InconsistentNaming
public static class TransformMatrixKHRMethods {
    public static unsafe void Load(ref this TransformMatrixKHR transform, Matrix4x4 matrix) {
        if (matrix.M14 != 0 || matrix.M24 != 0 || matrix.M34 != 0 || Math.Abs(matrix.M44 - 1) > 0.001)
            throw new Exception("Matrix needs to have the 4th row empty");

        for (var i = 0; i < 12; i++) {
            transform.Matrix[i] = matrix[i % 4, i / 4];
        }
    }
}