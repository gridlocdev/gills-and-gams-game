using System.Numerics;
using Raylib_cs;

namespace FishLegs;

// Small 3D drawing toolkit on top of raylib immediate-mode shapes.
static class Draw
{
    public static Shader Lit;
    static int viewPosLoc;

    const string Vs = @"#version 330
in vec3 vertexPosition;
in vec4 vertexColor;
uniform mat4 mvp;
out vec3 fragPos;
out vec4 fragColor;
void main() {
    fragPos = vertexPosition;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}";

    // Faceted lighting from screen-space derivatives: raylib's immediate shapes have no normals,
    // but their vertices arrive in world space, so we can reconstruct a flat normal per pixel.
    const string Fs = @"#version 330
in vec3 fragPos;
in vec4 fragColor;
uniform vec3 viewPos;
out vec4 finalColor;
void main() {
    vec3 c = cross(dFdx(fragPos), dFdy(fragPos));
    float l = length(c);
    vec3 n = l > 1e-9 ? c / l : vec3(0.0, 1.0, 0.0);
    vec3 v = viewPos - fragPos;
    if (dot(n, v) < 0.0) n = -n;
    vec3 sun = normalize(vec3(0.45, 1.0, 0.35));
    float diff = max(dot(n, sun), 0.0);
    float hemi = 0.5 + 0.5 * n.y;
    vec3 col = fragColor.rgb * (0.36 + 0.24 * hemi + 0.6 * diff);
    float rim = pow(1.0 - max(dot(n, normalize(v)), 0.0), 3.0) * 0.12;
    col += vec3(rim);
    float fog = clamp((length(v) - 45.0) / 80.0, 0.0, 0.55);
    col = mix(col, vec3(0.62, 0.82, 0.95), fog);
    finalColor = vec4(col, fragColor.a);
}";

    public static void Init()
    {
        Lit = Raylib.LoadShaderFromMemory(Vs, Fs);
        viewPosLoc = Raylib.GetShaderLocation(Lit, "viewPos");
    }

    public static void SetView(Vector3 camPos) =>
        Raylib.SetShaderValue(Lit, viewPosLoc, camPos, ShaderUniformDataType.Vec3);

    public static void Box(Vector3 center, Vector3 size, float yawDeg, Color c)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(center.X, center.Y, center.Z);
        Rlgl.Rotatef(yawDeg, 0, 1, 0);
        Raylib.DrawCubeV(Vector3.Zero, size, c);
        Rlgl.PopMatrix();
    }

    public static void Ellipsoid(Vector3 radii, Color c, int rings = 10, int slices = 14)
    {
        Rlgl.PushMatrix();
        Rlgl.Scalef(radii.X, radii.Y, radii.Z);
        Raylib.DrawSphereEx(Vector3.Zero, 1, rings, slices, c);
        Rlgl.PopMatrix();
    }

    public static void Ellipsoid(Vector3 at, Vector3 radii, Color c, int rings = 10, int slices = 14)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(at.X, at.Y, at.Z);
        Ellipsoid(radii, c, rings, slices);
        Rlgl.PopMatrix();
    }

    public static void Limb(Vector3 a, Vector3 b, float ra, float rb, Color c, int sides = 8) =>
        Raylib.DrawCylinderEx(a, b, ra, rb, sides, c);

    public static void Tri2(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        Raylib.DrawTriangle3D(a, b, c, col);
        Raylib.DrawTriangle3D(c, b, a, col);
    }

    // Flat disc on the ground, used for blob shadows and field markings.
    public static void Disc(Vector3 center, float radius, Color c, int segs = 20)
    {
        for (int i = 0; i < segs; i++)
        {
            float a0 = MathF.Tau * i / segs, a1 = MathF.Tau * (i + 1) / segs;
            var p0 = center + new Vector3(MathF.Cos(a0) * radius, 0, MathF.Sin(a0) * radius);
            var p1 = center + new Vector3(MathF.Cos(a1) * radius, 0, MathF.Sin(a1) * radius);
            Raylib.DrawTriangle3D(center, p1, p0, c);
        }
    }

    public static void Ring(Vector3 center, float r0, float r1, Color c, int segs = 40)
    {
        for (int i = 0; i < segs; i++)
        {
            float a0 = MathF.Tau * i / segs, a1 = MathF.Tau * (i + 1) / segs;
            var d0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0));
            var d1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1));
            Raylib.DrawTriangle3D(center + d0 * r0, center + d1 * r1, center + d0 * r1, c);
            Raylib.DrawTriangle3D(center + d0 * r0, center + d1 * r0, center + d1 * r1, c);
        }
    }

    public static void Shadow(Vector3 groundPos, float radius, float strength)
    {
        if (strength <= 0.01f) return;
        Disc(new Vector3(groundPos.X, 0.03f, groundPos.Z), radius, new Color(0, 0, 0, (int)(110 * strength)), 16);
    }
}
