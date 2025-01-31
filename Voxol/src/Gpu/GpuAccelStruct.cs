using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuAccelStruct : IDescriptor, IDisposable {
    public readonly GpuContext Ctx;
    
    public readonly AccelerationStructureKHR Handle;
    public readonly GpuBuffer Buffer;

    public GpuAccelStruct(GpuContext ctx, AccelerationStructureKHR handle, GpuBuffer buffer) {
        Ctx = ctx;
        Handle = handle;
        Buffer = buffer;
    }

    public DescriptorType DescriptorType => DescriptorType.AccelerationStructureKhr;

    public bool Equals(IDescriptor? other) {
        return ReferenceEquals(this, other);
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

    public void Dispose() {
        unsafe {
            Ctx.AccelStructApi.DestroyAccelerationStructure(Ctx.Device, Handle, null);
        }

        Buffer.Dispose();
        
        GC.SuppressFinalize(this);
    }

    public static implicit operator AccelerationStructureKHR(GpuAccelStruct accelStruct) => accelStruct.Handle;
}