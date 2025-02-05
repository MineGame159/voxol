using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuAccelStruct : GpuResource, IDescriptor {
    public readonly AccelerationStructureKHR Handle;
    public readonly GpuBuffer Buffer;

    public GpuAccelStruct(GpuContext ctx, AccelerationStructureKHR handle, GpuBuffer buffer) : base(ctx) {
        Handle = handle;
        Buffer = buffer;
    }

    public ulong DeviceAddress {
        get {
            unsafe {
                return Ctx.AccelStructApi.GetAccelerationStructureDeviceAddress(
                    Ctx.Device,
                    new AccelerationStructureDeviceAddressInfoKHR(
                        accelerationStructure: Handle
                    )
                );
            }
        }
    }

    public override void Dispose() {
        Ctx.OnDestroyResource(this);

        unsafe {
            Ctx.AccelStructApi.DestroyAccelerationStructure(Ctx.Device, Handle, null);
        }

        Buffer.Dispose();

        GC.SuppressFinalize(this);
    }

    // IDescriptor

    public DescriptorType DescriptorType => DescriptorType.AccelerationStructureKhr;

    public bool DescriptorEquals(IDescriptor other) {
        return Equals(other);
    }

    public int DescriptorHashCode() {
        return GetHashCode();
    }

    // Operators

    public static implicit operator AccelerationStructureKHR(GpuAccelStruct accelStruct) => accelStruct.Handle;
}