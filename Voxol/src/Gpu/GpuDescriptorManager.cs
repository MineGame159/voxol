using System.Diagnostics.CodeAnalysis;
using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuDescriptorManager {
    private readonly GpuContext ctx;

    private readonly DescriptorPool pool;

    private readonly MultiKeyDictionary<DescriptorType?, DescriptorSetLayout> layouts = new();
    private readonly MultiKeyDictionary<IDescriptor?, DescriptorSet> sets = new();

    public GpuDescriptorManager(GpuContext ctx) {
        this.ctx = ctx;

        Span<DescriptorPoolSize> poolSizes = [
            new(DescriptorType.UniformBuffer, 100),
            new(DescriptorType.StorageBuffer, 100),
            new(DescriptorType.StorageImage, 100),
            new(DescriptorType.CombinedImageSampler, 100),
            new(DescriptorType.AccelerationStructureKhr, 100)
        ];

        unsafe {
            Utils.Wrap(this.ctx.Vk.CreateDescriptorPool(this.ctx.Device, new DescriptorPoolCreateInfo(
                flags: DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                maxSets: 100,
                poolSizeCount: (uint) poolSizes.Length,
                pPoolSizes: Utils.AsPtr(poolSizes)
            ), null, out pool), "Failed to create a Descriptor Pool");
        }
    }

    internal void OnDestroyResource(GpuResource resource) {
        if (resource is IDescriptor descriptor) {
            sets.Remove(descriptors => descriptors.Contains(descriptor));
        }
    }

    public unsafe DescriptorSetLayout GetLayout(ReadOnlySpan<DescriptorType?> types) {
        if (!layouts.TryGetValue(types, out var layout)) {
            Span<DescriptorSetLayoutBinding> bindings = stackalloc DescriptorSetLayoutBinding[Utils.NonNullCount(types)];
            var i = 0;

            foreach (var type in types) {
                if (type == null)
                    continue;

                bindings[i] = new DescriptorSetLayoutBinding(
                    binding: (uint) i,
                    descriptorType: type.Value,
                    descriptorCount: 1,
                    stageFlags: ShaderStageFlags.All
                );

                i++;
            }

            Utils.Wrap(ctx.Vk.CreateDescriptorSetLayout(ctx.Device, new DescriptorSetLayoutCreateInfo(
                bindingCount: (uint) bindings.Length,
                pBindings: Utils.AsPtr(bindings)
            ), null, out layout), "Failed to create a Descriptor Set Layout");

            layouts[types] = layout;
        }

        return layout;
    }

    public DescriptorSetLayout GetLayout(ReadOnlySpan<IDescriptor?> descriptors) {
        Span<DescriptorType?> types = stackalloc DescriptorType?[descriptors.Length];

        for (var i = 0; i < types.Length; i++) {
            types[i] = descriptors[i]?.DescriptorType;
        }

        return GetLayout(types);
    }

    [SuppressMessage("ReSharper", "StackAllocInsideLoop")]
    public unsafe DescriptorSet GetSet(ReadOnlySpan<IDescriptor?> descriptors) {
        if (!sets.TryGetValue(descriptors, out var set)) {
            var layout = GetLayout(descriptors);

            Utils.Wrap(ctx.Vk.AllocateDescriptorSets(ctx.Device, new DescriptorSetAllocateInfo(
                descriptorPool: pool,
                descriptorSetCount: 1,
                pSetLayouts: &layout
            ), out set), "Failed to allocate a Descriptor Set");

            Span<WriteDescriptorSet> writes = stackalloc WriteDescriptorSet[Utils.NonNullCount(descriptors)];
            var i = 0;

            foreach (var descriptor in descriptors) {
                if (descriptor == null)
                    continue;

                ref var write = ref writes[i];

                write = new WriteDescriptorSet(
                    dstSet: set,
                    dstBinding: (uint) i,
                    descriptorCount: 1,
                    descriptorType: descriptor.DescriptorType
                );

                switch (descriptors[i++]) {
                    case GpuBuffer buffer: {
                        var info = stackalloc DescriptorBufferInfo[1];
                        *info = new DescriptorBufferInfo(
                            buffer: buffer,
                            range: Vk.WholeSize
                        );

                        write.PBufferInfo = info;
                        break;
                    }
                    case GpuImage image: {
                        var info = stackalloc DescriptorImageInfo[1];
                        *info = new DescriptorImageInfo(
                            imageView: image,
                            imageLayout: ImageLayout.General
                        );

                        write.PImageInfo = info;
                        break;
                    }
                    case GpuSamplerImage samplerImage: {
                        var info = stackalloc DescriptorImageInfo[1];
                        *info = new DescriptorImageInfo(
                            imageView: samplerImage.Image,
                            imageLayout: ImageLayout.ShaderReadOnlyOptimal,
                            sampler: samplerImage.Sampler
                        );

                        write.PImageInfo = info;
                        break;
                    }
                    case GpuAccelStruct accelStruct: {
                        var handle = stackalloc AccelerationStructureKHR[1];
                        *handle = accelStruct.Handle;

                        var info = stackalloc WriteDescriptorSetAccelerationStructureKHR[1];
                        *info = new WriteDescriptorSetAccelerationStructureKHR(
                            accelerationStructureCount: 1,
                            pAccelerationStructures: handle
                        );

                        write.PNext = info;
                        break;
                    }
                }
            }

            ctx.Vk.UpdateDescriptorSets(ctx.Device, (uint) writes.Length, Utils.AsPtr(writes), 0, null);

            sets[descriptors] = set;
        }

        return set;
    }
}