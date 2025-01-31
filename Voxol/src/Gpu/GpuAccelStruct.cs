using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuAccelStruct : GpuResource, IDescriptor {
    public readonly AccelerationStructureKHR Handle;
    public readonly GpuBuffer Buffer;

    public GpuAccelStruct(GpuContext ctx, AccelerationStructureKHR handle, GpuBuffer buffer) : base(ctx) {
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

    public override void Dispose() {
        Ctx.OnDestroyResource(this);
        
        unsafe {
            Ctx.AccelStructApi.DestroyAccelerationStructure(Ctx.Device, Handle, null);
        }

        Buffer.Dispose();
        
        GC.SuppressFinalize(this);
    }

    public static implicit operator AccelerationStructureKHR(GpuAccelStruct accelStruct) => accelStruct.Handle;
}