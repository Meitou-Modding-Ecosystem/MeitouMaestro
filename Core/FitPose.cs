using System.Numerics;

namespace Maestro.Core;

/// <summary>The Transport schema-3 rest pose, in model units. Seat and facing are local to the saddle.</summary>
public readonly record struct FitPose(Matrix4x4 Saddle, Vector3 Pelvis, Matrix4x4 Facing)
{
    public static FitPose At(Vector3 bone, Vector3 offset, Vector3 pitchYawRoll, Vector3 seat, float facing)
    {
        const float radians = MathF.PI / 180;
        var r = pitchYawRoll * radians;
        var saddle = Matrix4x4.CreateFromYawPitchRoll(r.Y, r.X, r.Z) * Matrix4x4.CreateTranslation(bone + offset);
        return new(saddle, Vector3.Transform(seat, saddle), Matrix4x4.CreateRotationY(facing * radians) * saddle);
    }
}
