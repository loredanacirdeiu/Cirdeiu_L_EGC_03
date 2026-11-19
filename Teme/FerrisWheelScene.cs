// Varianta C: roata panoramică
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema acasă - Varianta C: roata panoramică.
/// - Butucul (pivot central) se rotește și poartă spițele, janta și 8 cabine.
/// - Fiecare cabină compensează rotația roții ca să rămână verticală (+ legănare amortizată).
/// - Up / Down schimbă viteza țintă; viteza reală urmărește lin viteza țintă (independent de FPS).
/// - Space oprește roata lin.
/// - Tastele 1-8 aduc cabina aleasă jos, la peron, pe drumul cel mai scurt (<= 180°), folosind Tween.
/// </summary>
public sealed class FerrisWheelScene : Scene
{
    // Acțiunile proprii scenei (apar în meniul H).
    private const string SpeedUp = "mărește viteza roții";
    private const string SpeedDown = "scade viteza roții";
    private const string StopWheel = "oprește roata lin";
    private static readonly string[] CabinActions =
    {
        "cabina 1 la peron",
        "cabina 2 la peron",
        "cabina 3 la peron",
        "cabina 4 la peron",
        "cabina 5 la peron",
        "cabina 6 la peron",
        "cabina 7 la peron",
        "cabina 8 la peron",
    };

    private const int CabinCount = 8;
    private const float WheelRadius = 4.2f;
    private const float HubHeight = 5.4f;
    private const float SpeedStep = 15.0f;       // grade / s la fiecare apăsare Up / Down
    private const float MaxSpeed = 90.0f;        // grade / s
    private const float SmoothingK = 3.5f;       // constanta de amortizare pentru viteză

    private static readonly Color4[] CabinColors =
    {
        new(0.95f, 0.30f, 0.30f, 1.0f), // 1 - roșu
        new(0.95f, 0.55f, 0.20f, 1.0f), // 2 - portocaliu
        new(0.95f, 0.85f, 0.25f, 1.0f), // 3 - galben
        new(0.35f, 0.85f, 0.40f, 1.0f), // 4 - verde
        new(0.25f, 0.80f, 0.85f, 1.0f), // 5 - cyan
        new(0.35f, 0.55f, 0.95f, 1.0f), // 6 - albastru
        new(0.65f, 0.40f, 0.95f, 1.0f), // 7 - mov
        new(0.95f, 0.45f, 0.75f, 1.0f), // 8 - roz
    };

    private SceneObject _hub = null!;
    private readonly List<(SceneObject Pivot, float BaseAngleDeg)> _cabins = new();

    // Cinematica roții
    private float _wheelAngle;          // grade
    private float _currentSpeed;        // grade / s
    private float _targetSpeed = 25.0f; // pornește cu o rotație lină

    // Tween pentru aducerea unei cabine la peron (tastele 1-8)
    private Tween? _dockTween;
    private int _selectedCabin;         // 0 = niciuna, 1..8 = cabina selectată

    // Bonus: resort amortizat pentru balansul cabinelor la accelerare/frânare
    private float _swingAngle;          // grade
    private float _swingVelocity;       // grade / s

    public override string Name => "roata panoramică";

    public override string Status
    {
        get
        {
            if (_dockTween != null && !_dockTween.IsFinished)
            {
                return $"andocare cabina {_selectedCabin}";
            }
            if (_selectedCabin > 0 && MathF.Abs(_currentSpeed) < 0.1f && MathF.Abs(_targetSpeed) < 0.1f)
            {
                return $"cabina {_selectedCabin} la peron";
            }
            return $"v = {_currentSpeed:F1}°/s (țintă {_targetSpeed:F0}°/s)";
        }
    }

    public override IEnumerable<string> ActionNames =>
        new[] { SpeedUp, SpeedDown, StopWheel }.Concat(CabinActions);

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 6.2f, 15.0f);
        CameraTarget = new Vector3(0.0f, 4.8f, 0.0f);

        // Legăm acțiunile (fără a citi vreodată KeyboardState direct)
        input.Bind(SpeedUp, Keys.Up);
        input.Bind(SpeedDown, Keys.Down);
        input.Bind(StopWheel, Keys.Space);

        Keys[] digitKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8 };
        Keys[] padKeys = { Keys.KeyPad1, Keys.KeyPad2, Keys.KeyPad3, Keys.KeyPad4, Keys.KeyPad5, Keys.KeyPad6, Keys.KeyPad7, Keys.KeyPad8 };
        for (int i = 0; i < CabinCount; i++)
        {
            input.Bind(CabinActions[i], digitKeys[i], padKeys[i]);
        }

        // Forme geometrice
        Mesh grid = AddMesh(Shapes.Grid(12, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));

        AddObject("podea", grid);

        BuildSupportAndPlatform(cube);
        BuildWheel(cube, cylinder);
    }

    private void BuildSupportAndPlatform(Mesh cube)
    {
        Color4 steelColor = new(0.45f, 0.48f, 0.54f, 1.0f);

        // Peronul de jos, unde opresc cabinele
        SceneObject platform = AddObject("peron", cube);
        platform.Transform.Position = new Vector3(0.0f, 0.15f, 0.0f);
        platform.Transform.Scale = new Vector3(3.2f, 0.3f, 2.4f);
        platform.Tint = new Color4(0.55f, 0.42f, 0.28f, 1.0f);

        // Patru picioare de susținere (față-spate, stânga-dreapta), înclinate spre butuc
        float legLength = MathF.Sqrt(HubHeight * HubHeight + 2.4f * 2.4f);
        float tiltDeg = MathHelper.RadiansToDegrees(MathF.Atan2(2.4f, HubHeight));

        foreach (float z in new[] { -1.15f, 1.15f })
        {
            SceneObject leftLeg = AddObject("picior stânga", cube);
            leftLeg.Transform.Position = new Vector3(-1.2f, HubHeight * 0.5f, z);
            leftLeg.Transform.Rotation = new Vector3(0.0f, 0.0f, -tiltDeg);
            leftLeg.Transform.Scale = new Vector3(0.22f, legLength, 0.22f);
            leftLeg.Tint = steelColor;

            SceneObject rightLeg = AddObject("picior dreapta", cube);
            rightLeg.Transform.Position = new Vector3(1.2f, HubHeight * 0.5f, z);
            rightLeg.Transform.Rotation = new Vector3(0.0f, 0.0f, tiltDeg);
            rightLeg.Transform.Scale = new Vector3(0.22f, legLength, 0.22f);
            rightLeg.Tint = steelColor;
        }
    }

    private void BuildWheel(Mesh cube, Mesh cylinder)
    {
        Color4 rimColor = new(0.75f, 0.78f, 0.84f, 1.0f);
        Color4 spokeColor = new(0.58f, 0.62f, 0.68f, 1.0f);

        // Butucul (pivotul central al roții)
        _hub = AddObject("butuc", null);
        _hub.Transform.Position = new Vector3(0.0f, HubHeight, 0.0f);

        // Axul vizual al butucului
        SceneObject axle = AddObject("ax central", cylinder, _hub);
        axle.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
        axle.Transform.Scale = new Vector3(0.7f, 2.5f, 0.7f);
        axle.Tint = new Color4(0.35f, 0.38f, 0.44f, 1.0f);

        // 16 segmente de jantă (față + spate) + 8 perechi de spițe
        int rimSegments = 16;
        float segmentLength = 2.0f * WheelRadius * MathF.Sin(MathF.PI / rimSegments) + 0.08f;

        for (int i = 0; i < rimSegments; i++)
        {
            float angleDeg = i * (360.0f / rimSegments);
            float rad = MathHelper.DegreesToRadians(angleDeg);
            float x = WheelRadius * MathF.Cos(rad);
            float y = WheelRadius * MathF.Sin(rad);

            foreach (float z in new[] { -0.65f, 0.65f })
            {
                SceneObject rim = AddObject($"jantă {i}", cube, _hub);
                rim.Transform.Position = new Vector3(x, y, z);
                rim.Transform.Rotation = new Vector3(0.0f, 0.0f, angleDeg + 90.0f);
                rim.Transform.Scale = new Vector3(segmentLength, 0.14f, 0.14f);
                rim.Tint = rimColor;
            }
        }

        // Cele 8 spițe și cele 8 cabine
        for (int i = 0; i < CabinCount; i++)
        {
            float baseAngleDeg = i * (360.0f / CabinCount);
            float rad = MathHelper.DegreesToRadians(baseAngleDeg);
            float x = WheelRadius * MathF.Cos(rad);
            float y = WheelRadius * MathF.Sin(rad);

            // Spițe (față și spate)
            foreach (float z in new[] { -0.65f, 0.65f })
            {
                SceneObject spoke = AddObject($"spiță {i + 1}", cube, _hub);
                spoke.Transform.Position = new Vector3(x * 0.5f, y * 0.5f, z);
                spoke.Transform.Rotation = new Vector3(0.0f, 0.0f, baseAngleDeg);
                spoke.Transform.Scale = new Vector3(WheelRadius, 0.08f, 0.08f);
                spoke.Tint = spokeColor;
            }

            // Bară transversală de prindere între cele două jante
            SceneObject crossbar = AddObject($"ax cabină {i + 1}", cylinder, _hub);
            crossbar.Transform.Position = new Vector3(x, y, 0.0f);
            crossbar.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
            crossbar.Transform.Scale = new Vector3(0.12f, 1.35f, 0.12f);
            crossbar.Tint = spokeColor;

            // Pivotul cabinei (copil al butucului, se rotește invers decât roata)
            SceneObject cabinPivot = AddObject($"pivot cabină {i + 1}", null, _hub);
            cabinPivot.Transform.Position = new Vector3(x, y, 0.0f);

            // Tija de suspensie a cabinei
            SceneObject hanger = AddObject($"braț cabină {i + 1}", cube, cabinPivot);
            hanger.Transform.Position = new Vector3(0.0f, -0.25f, 0.0f);
            hanger.Transform.Scale = new Vector3(0.08f, 0.5f, 0.08f);
            hanger.Tint = spokeColor;

            // Corpul cabinei
            SceneObject cabinBody = AddObject($"cabină {i + 1}", cube, cabinPivot);
            cabinBody.Transform.Position = new Vector3(0.0f, -0.65f, 0.0f);
            cabinBody.Transform.Scale = new Vector3(0.85f, 0.65f, 0.95f);
            cabinBody.Tint = CabinColors[i];

            _cabins.Add((cabinPivot, baseAngleDeg));
        }
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        // 1. Citirea comenzilor
        if (input.WasPressed(SpeedUp))
        {
            _dockTween = null;
            _selectedCabin = 0;
            _targetSpeed = Math.Clamp(_targetSpeed + SpeedStep, -MaxSpeed, MaxSpeed);
        }
        if (input.WasPressed(SpeedDown))
        {
            _dockTween = null;
            _selectedCabin = 0;
            _targetSpeed = Math.Clamp(_targetSpeed - SpeedStep, -MaxSpeed, MaxSpeed);
        }
        if (input.WasPressed(StopWheel))
        {
            _dockTween = null;
            _selectedCabin = 0;
            _targetSpeed = 0.0f;
        }

        // Tastele 1-8: aduc cabina selectată jos, la peron (-90° / 270°), pe drumul cel mai scurt
        for (int i = 0; i < CabinCount; i++)
        {
            if (input.WasPressed(CabinActions[i]))
            {
                StartDockingCabin(i);
            }
        }

        // 2. Actualizarea unghiului roții (fie prin Tween de andocare, fie prin viteză amortizată)
        float prevSpeed = _currentSpeed;

        if (_dockTween != null)
        {
            float prevAngle = _wheelAngle;
            _dockTween.Update(dt);
            _wheelAngle = _dockTween.Value.X;
            _currentSpeed = dt > 1e-6f ? (_wheelAngle - prevAngle) / dt : 0.0f;

            if (_dockTween.IsFinished)
            {
                _wheelAngle = NormalizeAngle(_wheelAngle);
                _currentSpeed = 0.0f;
                _targetSpeed = 0.0f;
                _dockTween = null;
            }
        }
        else
        {
            // Formula independentă de numărul de cadre pe secundă din enunț:
            // v <- v + (v_țintă - v) * (1 - e^(-k * dt))
            float factor = 1.0f - MathF.Exp(-SmoothingK * dt);
            _currentSpeed += (_targetSpeed - _currentSpeed) * factor;
            _wheelAngle = NormalizeAngle(_wheelAngle + _currentSpeed * dt);
        }

        _hub.Transform.Rotation = new Vector3(0.0f, 0.0f, _wheelAngle);

        // 3. Bonus: resort amortizat pentru balansul cabinelor când roata accelerează sau frânează
        float angularAccel = dt > 1e-6f ? (_currentSpeed - prevSpeed) / dt : 0.0f;
        if (dt > 0.0f)
        {
            const float springK = 28.0f;
            const float damping = 4.5f;
            const float inertiaScale = 0.35f;

            float swingAccel = -springK * _swingAngle - damping * _swingVelocity - inertiaScale * angularAccel;
            _swingVelocity += swingAccel * dt;
            _swingAngle = Math.Clamp(_swingAngle + _swingVelocity * dt, -35.0f, 35.0f);
        }

        // Fiecare cabină se rotește invers față de roată (-_wheelAngle) ca să rămână verticală
        foreach (var (pivot, _) in _cabins)
        {
            pivot.Transform.Rotation = new Vector3(0.0f, 0.0f, -_wheelAngle + _swingAngle);
        }
    }

    private void StartDockingCabin(int cabinIndex)
    {
        _selectedCabin = cabinIndex + 1;
        _targetSpeed = 0.0f;

        // Poziția de jos (la peron) corespunde unghiului global -90° (sau 270°).
        // Unghiul global al cabinei este: baseAngle + wheelAngle.
        // Vrem: baseAngle + targetWheelAngle ≡ -90° (mod 360°).
        float baseAngle = _cabins[cabinIndex].BaseAngleDeg;
        float desiredWheelAngle = -90.0f - baseAngle;

        // Drumul cel mai scurt: diferența în intervalul [-180°, 180°]
        float delta = NormalizeAngle(desiredWheelAngle - _wheelAngle);
        float targetAngle = _wheelAngle + delta;

        // Durata proporțională cu distanța unghiulară (între 0.8 s și 2.4 s)
        float duration = Math.Clamp(MathF.Abs(delta) / 75.0f, 0.8f, 2.4f);
        _dockTween = new Tween(new Vector3(_wheelAngle, 0.0f, 0.0f),
                               new Vector3(targetAngle, 0.0f, 0.0f),
                               duration,
                               Easing.EaseInOut);
    }

    /// <summary>Aduce orice unghi în intervalul [-180, 180].</summary>
    private static float NormalizeAngle(float deg)
    {
        deg %= 360.0f;
        if (deg > 180.0f) deg -= 360.0f;
        if (deg < -180.0f) deg += 360.0f;
        return deg;
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Unghi roată    : {NormalizeAngle(_wheelAngle):F1}°";
        yield return $"Viteză         : {_currentSpeed:F1}°/s (țintă: {_targetSpeed:F1}°/s)";
        yield return $"Cabină aleasă  : {(_selectedCabin > 0 ? $"#{_selectedCabin}" : "niciuna")}   balans: {_swingAngle:F1}°";
    }
}