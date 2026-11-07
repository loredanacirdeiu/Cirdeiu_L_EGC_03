using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Față de laboratorul 3: o scenă cu mai multe obiecte, fiecare cu matricea lui model,
/// o cameră cu matricele view și projection, tastatura și mouse-ul.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)

    // --- T4: Starea pentru Pivot ---
    private Vector2 _pivotPosition;
    private bool _hasPivot;

    // --- T5: Obiectul Satelit ---
    private Mesh _satelliteMesh = null!;
    private float _satelliteAngle; // unghiul curent de rotație pe orbită

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;

    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine("=================================================");
        Console.WriteLine("                MENIU CONTROALE                  ");
        Console.WriteLine("=================================================");
        Console.WriteLine("  W, A, S, D       : Miscare camera");
        Console.WriteLine("  Rotită mouse     : Zoom camera");
        Console.WriteLine("  Săgeti           : Miscare obiect selectat");
        Console.WriteLine("  Q / E            : Rotatie obiect selectat");
        Console.WriteLine("  Click Stanga     : Muta obiectul selectat sub cursor (T3)");
        Console.WriteLine("  Click Dreapta    : Pune pivotul sub cursor (T4)");
        Console.WriteLine("  Tasta P (tinută) : Roteste obiectul în jurul pivotului (T4)");
        Console.WriteLine("  Tab              : Schimba obiectul selectat (satelitul il urmareste - T5)");
        Console.WriteLine("  O                : Comuta intre proiectie Ortografica / Perspectiva");
        Console.WriteLine("  F                : Comută modul Wireframe (contur / plin)");
        Console.WriteLine("  R                : Reseteaza scena si camera");
        Console.WriteLine("  Escape           : Inchide aplicatia");
        Console.WriteLine("=================================================\n");

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: aceleași forme din laboratorul 3, acum plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O, secțiunea 7.2).
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
                  new Transform2D { 
                      Position = new Vector2(-1.2f, 1.0f),
                      Rotation=45f,
                      Scale = new Vector2(1.5f, 0.5f)
                  
                  });
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });

        _satelliteMesh = Shapes.Triangle();
    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        float dt = (float)args.Time;
        SceneObject sel = _scene[_selected];

        const float moveSpeed = 2.0f;   // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.Left))  sel.Transform.Position.X -= moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down))  sel.Transform.Position.Y -= moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up))    sel.Transform.Position.Y += moveSpeed * dt;

        // TODO (T2): Q / E rotesc obiectul, Z / X îl scalează

        if (KeyboardState.IsKeyPressed(Keys.Tab)) _selected = (_selected + 1) % _scene.Count;
        if (KeyboardState.IsKeyPressed(Keys.O))   _camera.IsPerspective = !_camera.IsPerspective;
        if (KeyboardState.IsKeyPressed(Keys.F))   _wireframe = !_wireframe;
        if (KeyboardState.IsKeyPressed(Keys.R))   ResetScene();

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }
        float speedMultiplier = KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f;

        if(KeyboardState.IsKeyPressed(Keys.Q))
        {
            sel.Transform.Rotation += 90.0f * dt * speedMultiplier;
        }
        if (KeyboardState.IsKeyPressed(Keys.E))
        {
            sel.Transform.Rotation -= 90.0f * dt * speedMultiplier;
        }

        // --- Obiectul selectat: mouse-ul (secțiunea 9) -------------------------------

        // TODO (T3): la clic stânga, obiectul selectat se mută în punctul din lume aflat sub cursor
        //            (folosiți ScreenToWorld; Depth nu se schimbă)

        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            Vector2 mousePos = MouseState.Position;
            Vector2 worldPos = ScreenToWorld(mousePos);
            sel.Transform.Position = worldPos;
        }

        // TODO (T4, facultativ): clic dreapta = pivot; tasta P ținută = rotație în jurul pivotului

        // T4: Clic dreapta = stabilire pivot sub cursor
        if (MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivotPosition = ScreenToWorld(MouseState.Position);
            _hasPivot = true;
        }

        // T4: Tasta P = Rotație în jurul pivotului (90° pe secundă)
        if (_hasPivot && KeyboardState.IsKeyDown(Keys.P))
        {
            float deltaAngleDegrees = 90.0f * dt;
            float deltaAngleRad = MathHelper.DegreesToRadians(deltaAngleDegrees);

            // Vectorul de la pivot la poziția curentă a obiectului
            Vector2 r = sel.Transform.Position - _pivotPosition;

            // Rotim vectorul de poziție în jurul originii pivotului
            float cos = MathF.Cos(deltaAngleRad);
            float sin = MathF.Sin(deltaAngleRad);
            Vector2 rRotated = new Vector2(
                r.X * cos - r.Y * sin,
                r.X * sin + r.Y * cos
            );

            sel.Transform.Position = _pivotPosition + rRotated;
            sel.Transform.Rotation += deltaAngleDegrees; // Rotație proprie (efectul „Luna”)
        }

        // --- T5: Actualizare unghi satelit ---
        // O rotație completă (360°) la 4 secunde => 90°/secundă
        _satelliteAngle += 90.0f * dt;

        // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------

        float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimitem o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // TODO (T5, facultativ): satelitul obiectului selectat

        // --- T5: Desenare Satelit ---
        SceneObject selectedObj = _scene[_selected];

        // Matricea locală a satelitului: Scalare(0.3) -> Translație(1.2, 0) -> Rotație pe orbită
        Matrix4 localSatellite = Matrix4.CreateScale(0.3f, 0.3f, 1.0f) *
                                 Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f) *
                                 Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_satelliteAngle));

        // Matricea finală Model = Transformare Locală * Matricea Model a părintelui (obiectul selectat)
        Matrix4 satelliteModel = localSatellite * selectedObj.Transform.ModelMatrix;

        _shader.SetMatrix4("uModel", satelliteModel);
        _shader.SetColor("uTint", new Color4(1.0f, 0.8f, 0.2f, 1.0f));
        _satelliteMesh.Draw();

        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        _shader.Dispose();

        base.OnUnload();
    }

    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        // TODO (T3): pasul 1 (pixel → NDC), apoi pasul 2 (NDC → lume, cu inversa lui view * projection)

        // 1. Pixel → NDC (Normalized Device Coordinates: [-1, 1])
        float xNdc = (2.0f * mousePixels.X) / ClientSize.X - 1.0f;
        float yNdc = 1.0f - (2.0f * mousePixels.Y) / ClientSize.Y; // 1 - (...) compensează axa Y inversată (sus-jos)

        // 2. NDC → Lume, folosind inversa matricii View * Projection
        Matrix4 viewProjection = _camera.ViewMatrix * _camera.ProjectionMatrix;
        Matrix4 invViewProjection = Matrix4.Invert(viewProjection);

        // Transformăm coordonatele NDC în spațiul lumii
        Vector4 worldPos = invViewProjection * new Vector4(xNdc, yNdc, 0.0f, 1.0f); if (worldPos.W != 0.0f)
        {
            worldPos /= worldPos.W;
        }

        return new Vector2(worldPos.X, worldPos.Y);
    }

    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}
