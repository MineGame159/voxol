using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Voxol.Gpu.Spirv;

public static class SpirvParser {
    public static SpirvInfo Parse(byte[] bytes) {
        // Parse
        
        var reader = new Reader(bytes);
        reader.SkipHeader();

        var entryPoints = new List<SpirvInfo.EntryPoint>();
        var spirvBindings = new List<SpirvBinding>();
        var producedBy = new Dictionary<uint, OpUnknown>();

        ref SpirvBinding GetSpirvBinding(uint id) {
            for (var i = 0; i < spirvBindings.Count; i++) {
                ref var binding = ref CollectionsMarshal.AsSpan(spirvBindings)[i];
                if (binding.Id == id) return ref binding;
            }
            
            spirvBindings.Add(new SpirvBinding(id));
            return ref CollectionsMarshal.AsSpan(spirvBindings)[^1];
        }

        while (reader.HasMore) {
            var op = reader.Next();

            switch (op.Type) {
                case OpEntryPoint.Id:
                    var entryPoint = new OpEntryPoint(op);
                    entryPoints.Add(new SpirvInfo.EntryPoint(entryPoint.ExecutionModel.Vk(), entryPoint.Name));
                    break;
                
                case OpDecorate.Id:
                    var decorate = new OpDecorate(op);

                    switch (decorate.Decoration) {
                        case SpirvDecoration.DescriptorSet:
                            GetSpirvBinding(decorate.Target).Set = decorate.Value;
                            break;
                        
                        case SpirvDecoration.Binding:
                            GetSpirvBinding(decorate.Target).Index = decorate.Value;
                            break;
                    }
                    
                    break;
                
                case OpVariable.Id:
                    producedBy[new OpVariable(op).Result] = op;
                    break;
                
                case OpTypeImage.Id:
                    producedBy[new OpTypeImage(op).Result] = op;
                    break;
                
                case OpTypeSampledImage.Id:
                    producedBy[new OpTypeSampledImage(op).Result] = op;
                    break;
                
                case OpTypeRuntimeArray.Id:
                    producedBy[new OpTypeRuntimeArray(op).Result] = op;
                    break;
                
                case OpTypePointer.Id:
                    producedBy[new OpTypePointer(op).Result] = op;
                    break;
                
                case OpTypeAccelerationStructure.Id:
                    producedBy[new OpTypeAccelerationStructure(op).Result] = op;
                    break;
            }
        }
        
        // Analyze

        var bindings = new List<SpirvInfo.Binding>();

        foreach (var binding in spirvBindings) {
            if (binding.Set == null) Error("binding doesn't have Descriptor Set index");
            if (binding.Index == null) Error("binding doesn't have Binding index");

            var type = GetBindingType(producedBy, binding);
            bindings.Add(new SpirvInfo.Binding(binding.Set!.Value, binding.Index!.Value, type));
        }
        
        return new SpirvInfo(entryPoints.ToArray(), bindings.ToArray());
    }

    private static DescriptorType GetBindingType(Dictionary<uint, OpUnknown> producedBy, SpirvBinding binding) {
        if (producedBy[binding.Id].Type != OpVariable.Id) Error("OpDecorate isn't OpVariable");
        var var = new OpVariable(producedBy[binding.Id]);
        
        if (producedBy[var.ResultType].Type != OpTypePointer.Id) Error("OpVariable doesn't have OpTypePointer");
        var ptr = new OpTypePointer(producedBy[var.ResultType]);

        var type = ptr.StorageClass switch {
            SpirvStorageClass.Uniform => DescriptorType.UniformBuffer,
            SpirvStorageClass.StorageBuffer => DescriptorType.StorageBuffer,
            _ => default(DescriptorType?)
        };

        if (type == null) {
            switch (producedBy[ptr.Type].Type) {
                case OpTypeSampledImage.Id: {
                    var sampledImg = new OpTypeSampledImage(producedBy[ptr.Type]);

                    if (producedBy[sampledImg.Type].Type == OpTypeImage.Id) {
                        var img = new OpTypeImage(producedBy[sampledImg.Type]);

                        if (img.Sampled == 1) {
                            type = DescriptorType.CombinedImageSampler;
                        }
                    }

                    break;
                }
                case OpTypeImage.Id: {
                    var img = new OpTypeImage(producedBy[ptr.Type]);

                    if (img.Sampled == 2) {
                        type = DescriptorType.StorageImage;
                    }

                    break;
                }
                case OpTypeAccelerationStructure.Id: {
                    type = DescriptorType.AccelerationStructureKhr;
                    break;
                }
            }
        }

        if (type != null) {
            return type.Value;
        }

        Error("invalid descriptor type");
        return default;
    }

    private static void Error(string msg) {
        throw new Exception($"Invalid SPIRV shader, {msg}");
    }

    private struct SpirvBinding {
        public readonly uint Id;
        
        public uint? Set;
        public uint? Index;
        
        public SpirvBinding(uint id) {
            Id = id;
        }
    }
    
    private unsafe class Reader {
        private readonly uint* words;
        private readonly uint count;

        private uint pos;

        public Reader(byte[] bytes) {
            words = (uint*) Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(bytes));
            count = (uint) (bytes.Length / sizeof(uint));
        }

        private uint Remaining => count - pos;
        public bool HasMore => Remaining > 0;

        public void SkipHeader() {
            pos = 0;
            if (Remaining < 4) Error("not enough input");

            if (words[0] != 0x07230203) Error("invalid magic number");
            pos = 5;
        }

        public OpUnknown Next() {
            var op = new OpUnknown(&words[pos]);

            if (pos + op.WordCount > count) Error("not enough input");
            pos += op.WordCount;

            return op;
        }
    }
}