using UnityEngine;

namespace LethalMon.Throw;

public class FallCurve
{
    internal static readonly Keyframe[] fallCurveKeyframes = { new(0, 0, 2, 2), new(1, 1, 0, 0) };
    internal static readonly Keyframe[] verticalFallCurveKeyframes = { new(0, 0, 0.116908506f, 0.116908506f, 0, 0.27230743f), new(0.49081117f, 1, 4.1146584f, -1.81379f, 0.07234045f, 0.28319725f), new(0.7587703f, 1, 1.4123471f, -1.3678839f, 0.31997186f, 0.56917864f), new(0.9393898f, 1, 0.82654804f, -0.029021755f, 0.53747445f, 1), new(1, 1) };
    internal static readonly Keyframe[] verticalFallCurveNoBounceKeyFrames = { new(0, 0, 0.116908506f, 0.116908506f, 0, 0.27230743f), new(0.69081117f, 1, 0.1146584f, 0.06098772f, 0.07234045f, 0.20768756f), new(0.9393898f, 1, 0.06394797f, -0.029021755f, 0.1980713f, 1), new(1, 1) };
    internal static readonly AnimationCurve fallCurve = new(fallCurveKeyframes);
    internal static readonly AnimationCurve verticalFallCurve = new(verticalFallCurveKeyframes);
    internal static readonly AnimationCurve verticalFallCurveNoBounce = new(verticalFallCurveNoBounceKeyFrames);
}