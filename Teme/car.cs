// Scenă suplimentară: mașină sport (Ferrari) cu deplasare față/spate și sferă care orbitează în jurul ei
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Scenă cu o mașină sport roșie (Ferrari) construită ierarhic:
/// - Caroseria (șasiu coborât, capotă, cabină aerodinamică, eleron, faruri, stopuri);
/// - 4 roți copii ale mașinii, care se rotesc în jurul axei proprii la deplasare;
/// - Un pivot central atașat mașinii care poartă o sferă ce orbitează continuu în jurul ei;
/// - Deplasare înainte/înapoi controlată din taste (W/S sau Săgeți) + mod automat dus-întors (Tween).
/// </summary>
public sealed class FerrariScene : Scene
{
    private const string DriveForward = "accelerează înainte";
    private const string DriveBackward = "merge înapoi";
    private const string ToggleAuto = "mod automat dus-întors";
    private const string ResetPos = "resetează poziția mașinii";

    private const float MaxSpeed = 9.0f;
    private const float Acceleration = 14.0f;
    private const float Drag = 5.0f;
    private const float TrackLimit = 10.0f;
    private const float WheelRadius = 0.36f;

    private SceneObject _carRoot = null!;
    private readonly List<SceneObject> _wheels = new();
    private SceneObject _orbitPivot = null!;
    private SceneObject _orbitSphere = null!;

    private Tween _autoDriveTween = null!;
    private bool _autoMode = true;
    private float _carZ;
    private float _currentSpeed;
    private float _wheelSpinAngle;

    public override string Name => "Ferrari + orbită";

    public override string Status =>
        _autoMode
            ? $"mod automat | z = {_carZ:F1} m"
            : $"manual | v = {_currentSpeed:F1} m/s | z = {_carZ:F1} m";

    public override IEnumerable<string> ActionNames => new[]
    {
        DriveForward, DriveBackward, ToggleAuto, ResetPos,
    };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(8.5f, 4.8f, 10.5f);
        CameraTarget = new Vector3(0.0f, 1.0f, 0.0f);

        // Legăm acțiunile scenei
        input.Bind(DriveForward, Keys.W, Keys.Up);
        input.Bind(DriveBackward, Keys.S, Keys.Down);
        input.Bind(ToggleAuto, Keys.M);
        input.Bind(ResetPos, Keys.R);

        // Forme geometrice
        Mesh grid = AddMesh(Shapes.Grid(14, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh sphere = AddMesh(Shapes.ShadedSphere(1.0f, 24, 16));
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));

        BuildRoad(grid, cube);
        BuildFerrari(cube, cylinder);
        BuildOrbitingSphere(sphere);

        // Tween PingPong pentru deplasare lină înainte (-Z) și înapoi (+Z)
        _autoDriveTween = new Tween(
            new Vector3(0.0f, 0.0f, 6.5f),
            new Vector3(0.0f, 0.0f, -6.5f),
            3.2f,
            Easing.EaseInOut,
            pingPong: true);
    }

    private void BuildRoad(Mesh grid, Mesh cube)
    {
        AddObject("sol", grid);

        // Bandă de asfalt
        SceneObject asphalt = AddObject("asfalt", cube);
        asphalt.Transform.Position = new Vector3(0.0f, 0.005f, 0.0f);
        asphalt.Transform.Scale = new Vector3(4.2f, 0.01f, 24.0f);
        asphalt.Tint = new Color4(0.16f, 0.17f, 0.20f, 1.0f);

        // Marcaje albe discontinue pe mijlocul drumului
        for (float z = -10.0f; z <= 10.0f; z += 2.5f)
        {
            SceneObject stripe = AddObject("marcaj", cube);
            stripe.Transform.Position = new Vector3(0.0f, 0.015f, z);
            stripe.Transform.Scale = new Vector3(0.14f, 0.01f, 1.1f);
            stripe.Tint = new Color4(0.92f, 0.92f, 0.95f, 1.0f);
        }
    }

    private void BuildFerrari(Mesh cube, Mesh cylinder)
    {
        Color4 ferrariRed = new(0.88f, 0.06f, 0.08f, 1.0f);     // Rosso Corsa
        Color4 darkRed = new(0.65f, 0.04f, 0.06f, 1.0f);
        Color4 glassDark = new(0.10f, 0.14f, 0.20f, 1.0f);
        Color4 carbonBlack = new(0.12f, 0.12f, 0.14f, 1.0f);
        Color4 rimSilver = new(0.78f, 0.80f, 0.84f, 1.0f);

        // Rădăcina mașinii (fața mașinii este orientată spre -Z)
        _carRoot = AddObject("ferrari", null);

        // 1. Șasiul principal (lat și jos)
        SceneObject chassis = AddObject("șasiu", cube, _carRoot);
        chassis.Transform.Position = new Vector3(0.0f, 0.45f, 0.0f);
        chassis.Transform.Scale = new Vector3(1.95f, 0.42f, 4.35f);
        chassis.Tint = ferrariRed;

        // 2. Splitter frontal negru (sub bara din față)
        SceneObject frontSplitter = AddObject("splitter față", cube, _carRoot);
        frontSplitter.Transform.Position = new Vector3(0.0f, 0.24f, -2.18f);
        frontSplitter.Transform.Scale = new Vector3(1.98f, 0.08f, 0.45f);
        frontSplitter.Tint = carbonBlack;

        // 3. Capota față ușor înclinată
        SceneObject hood = AddObject("capotă față", cube, _carRoot);
        hood.Transform.Position = new Vector3(0.0f, 0.64f, -1.35f);
        hood.Transform.Rotation = new Vector3(7.0f, 0.0f, 0.0f);
        hood.Transform.Scale = new Vector3(1.82f, 0.16f, 1.45f);
        hood.Tint = ferrariRed;

        // 4. Compartiment motor spate (cu capac vitrat închis la culoare)
        SceneObject engineDeck = AddObject("capotă motor", cube, _carRoot);
        engineDeck.Transform.Position = new Vector3(0.0f, 0.72f, 1.15f);
        engineDeck.Transform.Rotation = new Vector3(-6.0f, 0.0f, 0.0f);
        engineDeck.Transform.Scale = new Vector3(1.76f, 0.20f, 1.55f);
        engineDeck.Tint = darkRed;

        SceneObject engineGlass = AddObject("geam motor", cube, _carRoot);
        engineGlass.Transform.Position = new Vector3(0.0f, 0.82f, 1.10f);
        engineGlass.Transform.Rotation = new Vector3(-6.0f, 0.0f, 0.0f);
        engineGlass.Transform.Scale = new Vector3(1.10f, 0.04f, 1.15f);
        engineGlass.Tint = glassDark;

        // 5. Habitaclul (cabina joasă, poziționată spre mijloc-față)
        SceneObject cabin = AddObject("cabină", cube, _carRoot);
        cabin.Transform.Position = new Vector3(0.0f, 0.90f, -0.10f);
        cabin.Transform.Scale = new Vector3(1.48f, 0.48f, 1.65f);
        cabin.Tint = glassDark;

        // Plafonul roșu deasupra cabinei
        SceneObject roof = AddObject("plafon", cube, _carRoot);
        roof.Transform.Position = new Vector3(0.0f, 1.14f, 0.05f);
        roof.Transform.Scale = new Vector3(1.36f, 0.07f, 1.05f);
        roof.Tint = ferrariRed;

        // 6. Prize de aer laterale (negre, specifice Ferrari)
        foreach (float sideX in new[] { -0.99f, 0.99f })
        {
            SceneObject sideIntake = AddObject("priză aer", cube, _carRoot);
            sideIntake.Transform.Position = new Vector3(sideX, 0.48f, 0.45f);
            sideIntake.Transform.Scale = new Vector3(0.06f, 0.24f, 0.95f);
            sideIntake.Tint = carbonBlack;
        }

        // 7. Eleron spate (aripă + 2 suporți)
        foreach (float postX in new[] { -0.58f, 0.58f })
        {
            SceneObject wingPost = AddObject("suport eleron", cube, _carRoot);
            wingPost.Transform.Position = new Vector3(postX, 0.86f, 1.92f);
            wingPost.Transform.Scale = new Vector3(0.07f, 0.36f, 0.22f);
            wingPost.Tint = carbonBlack;
        }
        SceneObject rearWing = AddObject("eleron spate", cube, _carRoot);
        rearWing.Transform.Position = new Vector3(0.0f, 1.05f, 1.96f);
        rearWing.Transform.Rotation = new Vector3(-5.0f, 0.0f, 0.0f);
        rearWing.Transform.Scale = new Vector3(1.94f, 0.07f, 0.34f);
        rearWing.Tint = carbonBlack;

        // 8. Faruri LED față și stopuri rotunde/duble spate + evacuări
        foreach (float x in new[] { -0.68f, 0.68f })
        {
            SceneObject headlight = AddObject("far", cube, _carRoot);
            headlight.Transform.Position = new Vector3(x, 0.56f, -2.14f);
            headlight.Transform.Scale = new Vector3(0.34f, 0.10f, 0.12f);
            headlight.Tint = new Color4(1.0f, 0.96f, 0.78f, 1.0f);

            SceneObject taillight = AddObject("stop", cube, _carRoot);
            taillight.Transform.Position = new Vector3(x, 0.58f, 2.18f);
            taillight.Transform.Scale = new Vector3(0.32f, 0.12f, 0.06f);
            taillight.Tint = new Color4(1.0f, 0.15f, 0.10f, 1.0f);
        }

        foreach (float x in new[] { -0.45f, 0.45f })
        {
            SceneObject exhaust = AddObject("evacuare", cylinder, _carRoot);
            exhaust.Transform.Position = new Vector3(x, 0.28f, 2.20f);
            exhaust.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
            exhaust.Transform.Scale = new Vector3(0.14f, 0.18f, 0.14f);
            exhaust.Tint = rimSilver;
        }

        // 9. Cele 4 roți (copii ale lui _carRoot)
        (float x, float z)[] wheelCoords =
        {
            (-0.96f, -1.32f), // față stânga
            ( 0.96f, -1.32f), // față dreapta
            (-0.98f,  1.32f), // spate stânga
            ( 0.98f,  1.32f), // spate dreapta
        };

        for (int i = 0; i < wheelCoords.Length; i++)
        {
            var (wx, wz) = wheelCoords[i];
            SceneObject wheelPivot = AddObject($"roată {i + 1}", null, _carRoot);
            wheelPivot.Transform.Position = new Vector3(wx, WheelRadius, wz);

            // Anvelopa
            SceneObject tire = AddObject($"anvelopă {i + 1}", cylinder, wheelPivot);
            tire.Transform.Rotation = new Vector3(0.0f, 0.0f, 90.0f);
            tire.Transform.Scale = new Vector3(WheelRadius * 2.0f, 0.28f, WheelRadius * 2.0f);
            tire.Tint = carbonBlack;

            // Janta argintie
            SceneObject rim = AddObject($"jantă {i + 1}", cylinder, wheelPivot);
            rim.Transform.Rotation = new Vector3(0.0f, 0.0f, 90.0f);
            rim.Transform.Scale = new Vector3(WheelRadius * 1.25f, 0.30f, WheelRadius * 1.25f);
            rim.Tint = rimSilver;

            // Spiță pe jantă ca să se vadă clar rotația roții
            SceneObject rimBar = AddObject($"spiță roată {i + 1}", cube, wheelPivot);
            rimBar.Transform.Scale = new Vector3(0.31f, WheelRadius * 1.15f, 0.07f);
            rimBar.Tint = ferrariRed;

            _wheels.Add(wheelPivot);
        }
    }

    private void BuildOrbitingSphere(Mesh sphere)
    {
        // Pivotul orbitei este copilul mașinii: urmărește mașina oriunde merge
        _orbitPivot = AddObject("pivot orbită sferă", null, _carRoot);
        _orbitPivot.Transform.Position = new Vector3(0.0f, 1.35f, 0.0f);
        _orbitPivot.Spin = new Vector3(0.0f, 135.0f, 0.0f); // se rotește în jurul mașinii (135°/s)

        // Sfera care orbitează în jurul mașinii
        _orbitSphere = AddObject("sferă orbitală", sphere, _orbitPivot);
        _orbitSphere.Transform.Position = new Vector3(2.85f, 0.25f, 0.0f);
        _orbitSphere.Transform.Scale = new Vector3(0.38f);
        _orbitSphere.Tint = new Color4(1.0f, 0.85f, 0.20f, 1.0f);
        _orbitSphere.Spin = new Vector3(0.0f, 180.0f, 0.0f);
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input); // actualizează Spin-ul pentru _orbitPivot și _orbitSphere

        if (input.WasPressed(ToggleAuto))
        {
            _autoMode = !_autoMode;
            if (_autoMode)
            {
                _autoDriveTween.Restart();
            }
            ConsolePanel.PrintMessage($"Ferrari: {(_autoMode ? "mod automat (Tween)" : "control manual (W/S sau Săgeți)")}");
        }

        if (input.WasPressed(ResetPos))
        {
            _carZ = 0.0f;
            _currentSpeed = 0.0f;
            _autoMode = false;
            ConsolePanel.PrintMessage("Ferrari: poziție resetată");
        }

        float throttle = input.Axis(DriveForward, DriveBackward); // -1 = înainte (-Z), +1 = înapoi (+Z)
        if (MathF.Abs(throttle) > 0.01f && _autoMode)
        {
            // Dacă apeși W/S sau Săgeți, trece automat pe control manual
            _autoMode = false;
        }

        float prevZ = _carZ;

        if (_autoMode)
        {
            _autoDriveTween.Update(dt);
            _carZ = _autoDriveTween.Value.Z;
            _currentSpeed = dt > 1e-6f ? (_carZ - prevZ) / dt : 0.0f;
        }
        else if (dt > 0.0f)
        {
            if (MathF.Abs(throttle) > 0.01f)
            {
                _currentSpeed = Math.Clamp(_currentSpeed + throttle * Acceleration * dt, -MaxSpeed, MaxSpeed);
            }
            else
            {
                // Frânare lină când nu este apăsată nicio tastă
                float factor = 1.0f - MathF.Exp(-Drag * dt);
                _currentSpeed += (0.0f - _currentSpeed) * factor;
            }

            _carZ = Math.Clamp(_carZ + _currentSpeed * dt, -TrackLimit, TrackLimit);
            if (MathF.Abs(_carZ) >= TrackLimit - 1e-4f)
            {
                _currentSpeed = 0.0f;
            }
        }

        _carRoot.Transform.Position = new Vector3(0.0f, 0.0f, _carZ);

        // Rotirea roților proporțional cu distanța parcursă (Δs = R * Δθ)
        float dz = _carZ - prevZ;
        float deltaWheelDeg = MathHelper.RadiansToDegrees(dz / WheelRadius);
        _wheelSpinAngle = (_wheelSpinAngle + deltaWheelDeg) % 360.0f;

        foreach (SceneObject wheel in _wheels)
        {
            wheel.Transform.Rotation = new Vector3(_wheelSpinAngle, 0.0f, 0.0f);
        }
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Mod deplasare  : {(_autoMode ? "automat (Tween PingPong)" : "manual (W/S sau Săgeți)")}";
        yield return $"Poziție Z      : {_carZ:F2} m   viteză: {_currentSpeed:F2} m/s";
        yield return $"Unghi orbită   : {_orbitPivot.Transform.Rotation.Y % 360.0f:F1}°";
    }
}