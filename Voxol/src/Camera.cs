using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Input;

namespace Voxol;

public class Camera {
    public Vector3 Pos;
    public float Yaw, Pitch;

    public Camera(Vector3 pos, float yaw, float pitch) {
        Pos = pos;
        Yaw = yaw;
        Pitch = pitch;
    }

    public Vector3 GetDirection(bool applyPitch) {
        if (applyPitch) {
            return Vector3.Normalize(new Vector3(
                MathF.Cos(Utils.DegToRad(Yaw)) * MathF.Cos(Utils.DegToRad(Pitch)),
                MathF.Sin(Utils.DegToRad(Pitch)),
                MathF.Sin(Utils.DegToRad(Yaw)) * MathF.Cos(Utils.DegToRad(Pitch))
            ));
        }
        
        return Vector3.Normalize(new Vector3(
            MathF.Cos(Utils.DegToRad(Yaw)),
            0,
            MathF.Sin(Utils.DegToRad(Yaw)) 
        ));
    }
    
    public void Move(float delta) {
        // Rotation
        if (Input.IsKeyDown(Key.Right)) Yaw += 90 * delta;
        if (Input.IsKeyDown(Key.Left)) Yaw -= 90 * delta;
        if (Input.IsKeyDown(Key.Up)) Pitch += 90 * delta;
        if (Input.IsKeyDown(Key.Down)) Pitch -= 90 * delta;

        Pitch = Math.Clamp(Pitch, -89.95f, 89.95f);
        
        // Movement
        var speed = 20 * delta;
        if (Input.IsKeyDown(Key.ControlLeft)) speed *= 15;

        var forward = GetDirection(false);
        var right = Vector3.Normalize(Vector3.Cross(forward, -Vector3.UnitY));

        forward *= speed;
        right *= speed;

        if (Input.IsKeyDown(Key.W)) Pos += forward;
        if (Input.IsKeyDown(Key.S)) Pos -= forward;
        if (Input.IsKeyDown(Key.D)) Pos -= right;
        if (Input.IsKeyDown(Key.A)) Pos += right;
        if (Input.IsKeyDown(Key.Space)) Pos.Y += speed;
        if (Input.IsKeyDown(Key.ShiftLeft)) Pos.Y -= speed;
    }

    public CameraData GetData(uint width, uint height) {
        return new CameraData(Pos, Pos + GetDirection(true), 70, (float) width / height);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct CameraData {
    public readonly Vector4 Origin;
    public readonly Vector4 LowerLeftCorner;
    public readonly Vector4 Horizontal;
    public readonly Vector4 Vertical;
    
    public CameraData(Vector3 pos, Vector3 lookAt, float fov, float aspectRatio) {
        var theta = Utils.DegToRad(fov);
        var h = MathF.Tan(theta / 2);
        var viewportHeight = 2 * h;
        var viewportWidth = aspectRatio * viewportHeight;

        var w = Vector3.Normalize(pos - lookAt);
        var u = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, w));
        var v = Vector3.Cross(w, u);

        Origin = new Vector4(pos, 0);
        Horizontal = new Vector4(viewportWidth * u, 0);
        Vertical = new Vector4(viewportHeight * v, 0);
        LowerLeftCorner = new Vector4(pos - viewportWidth * u / 2 - viewportHeight * v / 2 - w, 0);
    }
}