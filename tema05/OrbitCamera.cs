using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Camera orbită: se rotește în jurul unui punct țintă (Target), la o anumită distanță,
/// controlată prin azimut și elevație (coordonate sferice).
/// </summary>
public class OrbitCamera : Camera
{
    public Vector3 Target { get; set; }
    public float Distance { get; private set; }
    public float Azimuth { get; private set; }     // grade, în jurul axei Y
    public float Elevation { get; private set; }   // grade, unghiul față de planul orizontal

    public const float MaxElevation = 89.0f;
    public float MinDistance = 1.5f;
    public float MaxDistance = 40.0f;
    public float MouseSensitivity = 0.25f;
    public float KeyRotateSpeed = 90.0f;           // grade pe secundă pentru săgeți
    public float ZoomStep = 1.0f;
    public float MoveSpeed = 5.0f;                 // viteza deplasării țintei (bonus W A S D)

    private static readonly Vector3 DefaultTarget = new(0.0f, 1.0f, -2.0f);
    private const float DefaultDistance = 10.0f;
    private const float DefaultAzimuth = 90.0f;    // poziționată spre +Z, privind spre -Z
    private const float DefaultElevation = 20.0f;

    public OrbitCamera()
    {
        Reset();
    }

    public override string Name => "Orbită";

    public override Vector3 Position
    {
        get
        {
            float azRad = MathHelper.DegreesToRadians(Azimuth);
            float elRad = MathHelper.DegreesToRadians(Elevation);

            Vector3 offset = new(
                MathF.Cos(elRad) * MathF.Cos(azRad),
                MathF.Sin(elRad),
                MathF.Cos(elRad) * MathF.Sin(azRad)
            );

            return Target + Distance * offset;
        }
    }

    public override Vector3 Front => Vector3.Normalize(Target - Position);

    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(Position, Target, Vector3.UnitY);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // 1. Rotația cu mouse-ul (clic stânga ținut apăsat)
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;
        }

        // 2. Rotația cu tastele săgeți
        if (keyboard.IsKeyDown(Keys.Left)) Azimuth += KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth -= KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Up)) Elevation += KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Down)) Elevation -= KeyRotateSpeed * dt;

        Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);

        // 3. Rotița mouse-ului modifică distanța (nu FOV-ul)
        if (mouse.ScrollDelta.Y != 0)
        {
            Distance = Math.Clamp(Distance - mouse.ScrollDelta.Y * ZoomStep, MinDistance, MaxDistance);
        }

        // 4. Bonus 2: W A S D mută ținta în planul orizontal XZ
        Vector3 flatFront = new(Front.X, 0.0f, Front.Z);
        if (flatFront.LengthSquared > 1e-6f)
        {
            flatFront = Vector3.Normalize(flatFront);
            Vector3 flatRight = Vector3.Normalize(Vector3.Cross(flatFront, Vector3.UnitY));

            Vector3 move = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) move += flatFront;
            if (keyboard.IsKeyDown(Keys.S)) move -= flatFront;
            if (keyboard.IsKeyDown(Keys.D)) move += flatRight;
            if (keyboard.IsKeyDown(Keys.A)) move -= flatRight;

            if (move != Vector3.Zero)
            {
                Target += Vector3.Normalize(move) * MoveSpeed * dt;
            }
        }
    }

    /// <summary>
    /// Bonus 1 (FPS -> Orbită): poziționează ținta la o distanță fixă în fața camerei FPS
    /// și calculează azimutul și elevația astfel încât imaginea să nu sară.
    /// </summary>
    public void SyncFrom(Vector3 fpsPosition, Vector3 fpsFront, float distance = 5.0f)
    {
        Vector3 f = Vector3.Normalize(fpsFront);
        Target = fpsPosition + f * distance;
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);

        Vector3 o = -f; // vectorul unitar țintă -> cameră
        float elRad = MathF.Asin(Math.Clamp(o.Y, -1.0f, 1.0f));
        float azRad = MathF.Atan2(o.Z, o.X);

        Elevation = Math.Clamp(MathHelper.RadiansToDegrees(elRad), -MaxElevation, MaxElevation);
        Azimuth = MathHelper.RadiansToDegrees(azRad);
    }

    public override void Reset()
    {
        Target = DefaultTarget;
        Distance = DefaultDistance;
        Azimuth = DefaultAzimuth;
        Elevation = DefaultElevation;
        FieldOfView = 60.0f;
    }

    public override IEnumerable<string> Describe()
    {
        Vector3 pos = Position;
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut / elev. : {NormalizeAngle(Azimuth),7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({pos.X,6:F2}, {pos.Y,6:F2}, {pos.Z,6:F2})";
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }
}