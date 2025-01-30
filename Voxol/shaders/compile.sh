compile() {
    slangc -entry $1 -target spirv -O0 -fvk-use-entrypoint-name -o $2.spv shader.slang
}

compile RayGen rayGen
compile Miss miss
compile Intersection intersection
compile ClosestHit closestHit

slangc -target spirv -O0 -fvk-use-entrypoint-name -o imgui.spv imgui.slang
