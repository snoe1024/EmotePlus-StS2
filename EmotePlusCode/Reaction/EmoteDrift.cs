using Godot;
using MegaCrit.Sts2.Core.Random;

namespace EmotePlus.EmotePlusCode.Reaction;

/// <summary>
/// The direction and distance an emote drifts while it appears. The sender picks them (like the vanilla game:
/// within 30 degrees of straight up, 40-60px) and sends them with the emote, so that every viewer, and every
/// place the emote is shown at, drifts the same way.
/// </summary>
public readonly record struct EmoteDrift(byte Angle, byte Distance)
{
    private const float MaxDegrees = 30f;
    private const float MinDistance = 40f;
    private const float MaxDistance = 60f;

    public static EmoteDrift Random()
    {
        return new EmoteDrift((byte)Rng.Chaotic.NextInt(256), (byte)Rng.Chaotic.NextInt(256));
    }

    /// <summary>The offset the emote drifts by.</summary>
    public Vector2 Offset
    {
        get
        {
            var degrees = Mathf.Lerp(-MaxDegrees, MaxDegrees, Angle / 255f);
            var distance = Mathf.Lerp(MinDistance, MaxDistance, Distance / 255f);
            return Vector2.Up.Rotated(Mathf.DegToRad(degrees)) * distance;
        }
    }
}
