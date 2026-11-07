using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Corruption.Windows;

/// A baby growing inside a softly lit womb, drawn from shapes so it can grow smoothly over the pregnancy: a tiny bean
/// first, then a curled fetus whose head, limbs, cord and features fill in as the days pass.
public static class BabyIllustration
{
    private static readonly Vector4 WombOuter = new(0.42f, 0.10f, 0.33f, 0.55f);
    private static readonly Vector4 WombInner = new(1.00f, 0.62f, 0.76f, 0.70f);
    private static readonly Vector4 Glow = new(1.00f, 0.55f, 0.75f, 1f);
    private static readonly Vector4 Skin = new(1.00f, 0.80f, 0.74f, 1f);
    private static readonly Vector4 SkinShade = new(0.86f, 0.55f, 0.55f, 1f);
    private static readonly Vector4 Cord = new(0.86f, 0.42f, 0.62f, 1f);
    private static readonly Vector4 Heart = new(1.00f, 0.25f, 0.40f, 1f);

    private static readonly string[] Sizes =
    [
        "a poppy seed", "a sesame seed", "a lentil", "a blueberry", "a raspberry", "a grape", "a strawberry",
        "a plum", "a peach", "a lemon", "an avocado", "a mango", "a papaya", "a watermelon",
    ];

    /// What the baby is about the size of, on this day of the 14.
    public static string SizeOn(double days) => Sizes[Math.Clamp((int)Math.Floor(days), 0, Sizes.Length - 1)];

    /// Draws at the cursor and takes up a square of <paramref name="size"/>. <paramref name="progress"/> runs 0 to 1.
    public static void Draw(float size, double progress)
    {
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));

        var t = (float)Math.Clamp(progress, 0, 1);
        var time = (float)ImGui.GetTime();
        var r = size / 2f * 0.92f;
        var c = origin + new Vector2(size / 2f, size / 2f);

        DrawWomb(dl, c, r, time);

        // Grows fast early and slower later, as the size comparisons do.
        var scale = r * Lerp(0.10f, 0.80f, MathF.Pow(t, 0.75f));
        var bob = new Vector2(0, MathF.Sin(time * 1.3f) * r * 0.02f);
        var o = c + bob;
        Vector2 P(float x, float y) => o + new Vector2(x, y) * scale;

        if (t < 0.12f)
        {
            DrawBean(dl, P, scale, time);
            return;
        }

        if (t >= 0.12f) DrawCord(dl, P, c, r, scale, t);
        DrawBody(dl, P, scale, t, time);
    }

    private static void DrawWomb(ImDrawListPtr dl, Vector2 c, float r, float time)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 2f);
        dl.AddCircleFilled(c, r * 1.06f, U32(Glow with { W = 0.10f + 0.06f * pulse }), 64);
        const int steps = 12;
        for (var i = 0; i < steps; i++)
        {
            var f = i / (float)(steps - 1);
            dl.AddCircleFilled(c, r * (1f - f * 0.75f), U32(Vector4.Lerp(WombOuter, WombInner, f)), 64);
        }
        dl.AddCircleFilled(c + new Vector2(-r * 0.32f, -r * 0.38f), r * 0.28f, U32(new Vector4(1, 1, 1, 0.07f)), 32);
        dl.AddCircle(c, r, U32(Glow with { W = 0.75f }), 64, 2.5f);
        dl.AddCircle(c, r * 1.03f, U32(Glow with { W = 0.25f }), 64, 3f);
    }

    private static void DrawBean(ImDrawListPtr dl, Func<float, float, Vector2> p, float s, float time)
    {
        dl.AddCircleFilled(p(0, -0.14f), s * 0.38f, U32(SkinShade), 32);
        dl.AddCircleFilled(p(0.04f, 0.18f), s * 0.32f, U32(SkinShade), 32);
        dl.AddCircleFilled(p(0, -0.14f), s * 0.34f, U32(Skin), 32);
        dl.AddCircleFilled(p(0.04f, 0.18f), s * 0.28f, U32(Skin), 32);
        var beat = 0.5f + 0.5f * MathF.Sin(time * 7f);
        dl.AddCircleFilled(p(-0.02f, 0.02f), s * (0.08f + 0.03f * beat), U32(Heart with { W = 0.55f + 0.35f * beat }), 16);
    }

    private static void DrawCord(ImDrawListPtr dl, Func<float, float, Vector2> p, Vector2 c, float r, float s, float t)
    {
        var belly = p(-0.08f, 0.10f);
        var wall = c + new Vector2(-r * 0.62f, r * 0.70f);
        var thickness = MathF.Max(1.5f, s * 0.05f);
        dl.AddBezierCubic(belly, belly + new Vector2(-r * 0.25f, r * 0.05f), wall + new Vector2(r * 0.25f, -r * 0.25f), wall,
            U32(Cord with { W = Math.Clamp((t - 0.12f) * 6f, 0, 1) }), thickness, 24);
    }

    /// The curled fetus: back to the right, face and limbs tucked to the left.
    private static void DrawBody(ImDrawListPtr dl, Func<float, float, Vector2> p, float s, float t, float time)
    {
        // The spine curls around this point; the body is a chain of circles along it.
        const float cx = 0.08f, cy = 0.05f, spine = 0.33f;
        var headR = Lerp(0.34f, 0.25f, t);
        var head = p(-0.13f, -0.26f);
        var limbs = Math.Clamp((t - 0.28f) * 5f, 0, 1);
        var outline = MathF.Max(1.5f, s * 0.035f);

        // Outline pass, then fill, so overlapping parts read as one silhouette.
        for (var pass = 0; pass < 2; pass++)
        {
            var color = U32(pass == 0 ? SkinShade : Skin);
            var grow = pass == 0 ? outline : 0;

            for (var a = -80f; a <= 105f; a += 7f)
            {
                var rad = a * MathF.PI / 180f;
                var thick = 0.20f + 0.07f * MathF.Exp(-MathF.Pow((a - 15f) / 60f, 2));
                dl.AddCircleFilled(p(cx + spine * MathF.Cos(rad), cy + spine * MathF.Sin(rad)), s * thick + grow, color, 24);
            }
            dl.AddCircleFilled(head, s * headR + grow, color, 32);

            if (limbs > 0)
            {
                var arm = s * 0.085f * limbs + grow * 2;
                Limb(dl, p(0.04f, -0.16f), p(-0.10f, -0.02f), p(-0.24f, -0.10f), arm, color);
                dl.AddCircleFilled(p(-0.24f, -0.10f), s * 0.06f * limbs + grow, color, 16);
                var leg = s * 0.11f * limbs + grow * 2;
                Limb(dl, p(0.02f, 0.30f), p(-0.30f, 0.14f), p(-0.24f, 0.38f), leg, color);
                dl.AddCircleFilled(p(-0.25f, 0.39f), s * 0.07f * limbs + grow, color, 16);
            }
        }

        // Soft shading on the back, then features as they form.
        dl.AddCircleFilled(p(0.30f, 0.02f), s * 0.12f, U32(SkinShade with { W = 0.25f }), 24);
        if (t >= 0.45f)
        {
            var eye = U32(new Vector4(0.45f, 0.22f, 0.28f, Math.Clamp((t - 0.45f) * 6f, 0, 1)));
            dl.AddBezierCubic(head + new Vector2(-0.17f, 0.02f) * s, head + new Vector2(-0.14f, 0.06f) * s,
                head + new Vector2(-0.09f, 0.06f) * s, head + new Vector2(-0.06f, 0.02f) * s, eye, MathF.Max(1.2f, s * 0.02f), 12);
        }
        if (t >= 0.55f) dl.AddCircleFilled(head + new Vector2(0.06f, 0.03f) * s, s * 0.05f, U32(SkinShade with { W = 0.6f }), 16);
        if (t >= 0.65f) dl.AddCircleFilled(head + new Vector2(-0.15f, 0.10f) * s, s * 0.05f, U32(Heart with { W = 0.18f }), 16);

        var beat = 0.5f + 0.5f * MathF.Sin(time * 7f);
        dl.AddCircleFilled(p(-0.02f, -0.04f), s * (0.05f + 0.02f * beat), U32(Heart with { W = 0.20f + 0.25f * beat }), 16);
    }

    private static void Limb(ImDrawListPtr dl, Vector2 from, Vector2 bend, Vector2 to, float thickness, uint color)
    {
        dl.AddLine(from, bend, color, thickness);
        dl.AddLine(bend, to, color, thickness);
        dl.AddCircleFilled(bend, thickness / 2f, color, 16);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static uint U32(Vector4 color) => ImGui.GetColorU32(color);
}
