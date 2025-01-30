namespace Obj2Voxel;

public enum Error {
    Ok = 0,
    NoInput = 1,
    NoOutput = 2,
    NoResolution = 3,
    IoErrorOnOpenInputFile = 4,
    IoErrorOnOpenOutputFile = 5,
    IoErrorDuringVoxelWrite = 6,
    DoubleVoxelization = 7
}