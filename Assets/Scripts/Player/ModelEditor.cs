using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// An in-game editor for the characters' looks (F10, or Dev Tools > Model editor): the enemies, and the
/// player's own simple body (seen from the drone). Each is a model of simple shapes on a few joints
/// (EnemyModel), starting from exactly how the game builds it: pick one, add, move, turn, size and colour
/// shapes, mark eyes, set the joints and legs, and save it. Saved models are JSON files (in the project, under
/// Resources/EnemyModels) that can be sent along as a design; Copy puts the JSON on the clipboard to paste
/// anywhere. A saved scour or player model with "Use in game" on is what the game builds them from.
///
/// It shows the model on a lit turntable far below the world, while the game waits, paused. Right-drag to
/// turn round it, middle-drag (or Shift + right-drag) to move, the wheel to zoom, click a shape to pick it.
/// Esc or F10 closes it (unsaved changes stay until Play stops).
/// DarkForestGenerator adds this.
/// </summary>
public class ModelEditor : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    /// <summary>True while the editor is open (the game is paused, its input off).</summary>
    public static bool IsOpen { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => IsOpen = false;

    static ModelEditor instance;
    static readonly Vector3 Stage = new Vector3(0f, -3000f, 0f);
    static readonly Color Highlight = new Color(1f, 0.55f, 0.15f);

    /// <summary>Opens the editor (from the dev tools).</summary>
    public static void Open()
    {
        if (instance == null) instance = FindAnyObjectByType<ModelEditor>();
        if (instance == null) instance = new GameObject("Model Editor").AddComponent<ModelEditor>();
        instance.SetOpen(true);
    }

    // The model being edited, per enemy (kept while Play lasts, saved or not).
    readonly Dictionary<string, EnemyModel> editing = new Dictionary<string, EnemyModel>();
    readonly HashSet<string> unsaved = new HashSet<string>();
    int enemyIndex;
    int selected = -1;
    bool rebuild;
    string EnemyName => EnemyModel.Enemies[enemyIndex];
    EnemyModel Model => editing[EnemyName];

    // The turntable.
    Transform stage, modelRoot;
    Camera viewCam;
    EnemyModel.Built built;
    readonly List<Material> partMaterials = new List<Material>();
    float yaw = 200f, pitch = 8f, distance = 4f;
    Vector3 focus;

    // What's put back on closing.
    float timeScaleBefore;
    bool fogBefore;
    Camera mainCamera;
    readonly List<Behaviour> paused = new List<Behaviour>();
    bool restoreNextFrame;

    // The panels.
    Vector2 listScroll, propScroll;
    readonly Dictionary<string, string> typing = new Dictionary<string, string>();
    string status = "";
    float statusUntil;
    GUIStyle title, label, small, button, selectedButton;
    float unit = 1f;
    Rect leftPanel, rightPanel;

    void Update()
    {
        if (MainMenu.Showing) return;
        if (HorrorInput.ModelEditorPressed || (IsOpen && HorrorInput.ReleaseCursorPressed)) SetOpen(!IsOpen);
        if (!IsOpen) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (rebuild) Rebuild();
        HighlightSelected();
    }

    void LateUpdate()
    {
        // Put the game's input back a frame after closing, so the key that closed it doesn't also pause.
        if (!restoreNextFrame) return;
        restoreNextFrame = false;
        foreach (var b in paused) if (b != null) b.enabled = true;
        paused.Clear();
    }

    void SetOpen(bool open)
    {
        if (open == IsOpen) return;
        IsOpen = open;
        if (open)
        {
            instance = this;
            DevTools.Close();
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            fogBefore = RenderSettings.fog;
            RenderSettings.fog = false; // (the turntable's in clear air; HdrpAtmosphere follows this)
            restoreNextFrame = false;
            paused.Clear();
            Pause(FindAnyObjectByType<FirstPersonController>());
            Pause(FindAnyObjectByType<Hotbar>());
            Pause(FindAnyObjectByType<LampInteractor>());
            Pause(FindAnyObjectByType<PauseMenu>());
            mainCamera = Camera.main;
            if (mainCamera != null) mainCamera.enabled = false;
            BuildStage();
            if (!editing.ContainsKey(EnemyName)) Select(EnemyName);
            rebuild = true;
            FrameModel();
        }
        else
        {
            Time.timeScale = timeScaleBefore;
            RenderSettings.fog = fogBefore;
            if (mainCamera != null) mainCamera.enabled = true;
            if (stage != null) Destroy(stage.gameObject);
            stage = null;
            built = null;
            restoreNextFrame = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Pause(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        paused.Add(b);
    }

    void OnDisable()
    {
        if (IsOpen) SetOpen(false);
    }

    // ---------------------------------------------------------------- The model

    /// <summary>
    /// Switches to a character: what's being edited, else what's saved, else exactly how the game builds it
    /// (or, if that can't be had, a simple stand-in).
    /// </summary>
    void Select(string enemy)
    {
        enemyIndex = Mathf.Max(0, System.Array.IndexOf(EnemyModel.Enemies, enemy));
        if (!editing.ContainsKey(enemy))
            editing[enemy] = EnemyModel.Load(enemy) ?? EnemyModel.Capture(enemy) ?? EnemyModel.Template(enemy);
        selected = -1;
        rebuild = true;
        FrameModel();
    }

    void Changed()
    {
        unsaved.Add(EnemyName);
        rebuild = true;
    }

    void Rebuild()
    {
        rebuild = false;
        if (stage == null) return;
        if (modelRoot != null) Destroy(modelRoot.gameObject);
        modelRoot = new GameObject("Model").transform;
        modelRoot.SetParent(stage, false);
        partMaterials.Clear();
        var legMat = MaterialFactory.Lit(Model.parts.Count > 0 ? Model.parts[0].color : Color.black, 0.15f);
        built = Model.Build(modelRoot, 1f, part =>
        {
            if (part == null) return legMat;
            var m = part.glow > 0f ? MaterialFactory.NoFog(part.color * part.glow) : MaterialFactory.Lit(part.color, 0.15f);
            partMaterials.Add(m);
            return m;
        }, true, true);
        selected = Mathf.Clamp(selected, -1, Model.parts.Count - 1);
    }

    /// <summary>The picked shape pulses orange, so you can see which it is.</summary>
    void HighlightSelected()
    {
        if (built == null) return;
        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
        for (int i = 0; i < partMaterials.Count && i < Model.parts.Count; i++)
        {
            var part = Model.parts[i];
            Color c = part.glow > 0f ? part.color * part.glow : part.color;
            if (i == selected) c = Color.Lerp(c, Highlight * (part.glow > 0f ? part.glow : 1f), pulse);
            MaterialFactory.SetColor(partMaterials[i], c);
        }
    }

    // ---------------------------------------------------------------- The turntable

    void BuildStage()
    {
        stage = new GameObject("Model Editor Stage").transform;
        stage.position = Stage;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Turntable";
        Destroy(floor.GetComponent<Collider>());
        floor.transform.SetParent(stage, false);
        floor.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        floor.transform.localScale = new Vector3(6f, 0.02f, 6f);
        floor.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.22f, 0.21f, 0.2f), 0.1f);

        viewCam = new GameObject("Model Editor Camera").AddComponent<Camera>();
        viewCam.transform.SetParent(stage, false);
        viewCam.clearFlags = CameraClearFlags.SolidColor;
        viewCam.backgroundColor = new Color(0.16f, 0.16f, 0.17f);
        viewCam.nearClipPlane = 0.02f;
        viewCam.farClipPlane = 2000f;
        viewCam.fieldOfView = 40f;
        viewCam.depth = 50f;
        viewCam.gameObject.AddComponent<HDAdditionalCameraData>();
    }

    /// <summary>Lights placed for the model's size: a warm key, a cool fill, a rim from behind.</summary>
    void PlaceLights(float size)
    {
        foreach (var l in stage.GetComponentsInChildren<Light>()) Destroy(l.gameObject);
        AddLight(new Vector3(-1.2f, 1.6f, 1.4f) * size, LightType.Spot, new Color(1f, 0.92f, 0.82f), 4f, true);
        AddLight(new Vector3(1.5f, 0.9f, 1.2f) * size, LightType.Point, new Color(0.7f, 0.8f, 1f), 1.4f, false);
        AddLight(new Vector3(0.3f, 1.4f, -1.6f) * size, LightType.Spot, new Color(1f, 0.6f, 0.45f), 3f, false);

        void AddLight(Vector3 at, LightType type, Color color, float strength, bool shadows)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.localPosition = at + Vector3.up * size * 0.5f;
            light.transform.LookAt(stage.position + Vector3.up * size * 0.5f);
            light.type = type;
            light.color = color;
            light.lightUnit = LightUnit.Candela;
            light.intensity = strength * at.sqrMagnitude; // about as bright on the model whatever its size
            light.range = size * 6f;
            light.spotAngle = 70f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }
    }

    /// <summary>Looks at the whole model, and lights it.</summary>
    void FrameModel()
    {
        float h = editing.ContainsKey(EnemyName) ? Mathf.Max(Model.height, 0.3f) : 2.4f;
        focus = Vector3.up * h * 0.5f;
        distance = h * 1.9f;
        if (stage != null) PlaceLights(h);
    }

    void PlaceCamera()
    {
        if (viewCam == null) return;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        viewCam.transform.position = stage.position + focus - rot * Vector3.forward * distance;
        viewCam.transform.rotation = rot;
    }

    // ---------------------------------------------------------------- Drawing

    void OnGUI()
    {
        if (!IsOpen || stage == null) return;
        GUI.depth = -950;
        unit = Mathf.Max(0.75f, Screen.height / 1080f);
        Styles();

        leftPanel = new Rect(10f * unit, 10f * unit, 360f * unit, Screen.height - 20f * unit);
        rightPanel = new Rect(Screen.width - 420f * unit, 10f * unit, 410f * unit, Screen.height - 20f * unit);
        Fill(leftPanel, new Color(0f, 0f, 0f, 0.8f));
        Fill(rightPanel, new Color(0f, 0f, 0f, 0.8f));

        HandleViewport();
        PlaceCamera();
        DrawLeft();
        DrawRight();

        var hint = new Rect(leftPanel.xMax + 10f * unit, Screen.height - 34f * unit, rightPanel.x - leftPanel.xMax - 20f * unit, 26f * unit);
        GUI.Label(hint, "Right-drag: turn round   Middle-drag / Shift+right-drag: move   Wheel: zoom   Click: pick a shape   Esc / F10: close", small);
        if (Time.unscaledTime < statusUntil)
            GUI.Label(new Rect(hint.x, 14f * unit, hint.width, 26f * unit), status, label);
    }

    void HandleViewport()
    {
        var e = Event.current;
        Vector2 mouse = e.mousePosition;
        bool overPanel = leftPanel.Contains(mouse) || rightPanel.Contains(mouse);
        if (e.type == EventType.ScrollWheel && !overPanel)
        {
            distance = Mathf.Clamp(distance * (1f + e.delta.y * 0.05f), Model.height * 0.2f, Model.height * 8f);
            e.Use();
        }
        if (e.type == EventType.MouseDrag && !overPanel)
        {
            bool pan = e.button == 2 || (e.button == 1 && e.shift);
            if (pan)
            {
                float speed = distance * 0.0015f;
                focus += (-viewCam.transform.right * e.delta.x + viewCam.transform.up * e.delta.y) * speed;
                e.Use();
            }
            else if (e.button == 1)
            {
                yaw += e.delta.x * 0.3f;
                pitch = Mathf.Clamp(pitch + e.delta.y * 0.3f, -80f, 80f);
                e.Use();
            }
        }
        if (e.type == EventType.MouseDown && e.button == 0 && !overPanel && built != null)
        {
            // Pick the shape under the pointer.
            var ray = viewCam.ScreenPointToRay(new Vector3(mouse.x, Screen.height - mouse.y, 0f));
            selected = -1;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Collide))
            {
                int i = built.parts.IndexOf(hit.collider.gameObject);
                if (i >= 0 && hit.distance < best) { best = hit.distance; selected = i; }
            }
            e.Use();
        }
    }

    void DrawLeft()
    {
        GUILayout.BeginArea(Inset(leftPanel));
        GUILayout.Label("MODEL EDITOR  (F10)", title);

        // Which enemy.
        GUILayout.BeginHorizontal();
        for (int i = 0; i < EnemyModel.Enemies.Length; i++)
        {
            if (i == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            string name = EnemyModel.Enemies[i] + (unsaved.Contains(EnemyModel.Enemies[i]) ? " *" : "");
            if (GUILayout.Button(name, i == enemyIndex ? selectedButton : button)) Select(EnemyModel.Enemies[i]);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f * unit);

        // Saving and sharing.
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save", button))
        {
            string path = Model.Save();
            unsaved.Remove(EnemyName);
            if (EnemyName == "Player") { var body = FindAnyObjectByType<PlayerBody>(); if (body != null) body.Rebuild(); }
            Say($"Saved: {path}");
        }
        if (GUILayout.Button("Copy JSON", button))
        {
            GUIUtility.systemCopyBuffer = JsonUtility.ToJson(Model, true);
            Say("Copied the model to the clipboard: paste it anywhere to share it.");
        }
        if (GUILayout.Button("Paste JSON", button))
        {
            try
            {
                var pasted = JsonUtility.FromJson<EnemyModel>(GUIUtility.systemCopyBuffer);
                if (pasted == null || pasted.parts == null) throw new System.Exception("no model there");
                pasted.enemy = EnemyName;
                editing[EnemyName] = pasted;
                selected = -1;
                Changed();
                FrameModel();
                Say("Pasted a model from the clipboard (Save to keep it).");
            }
            catch (System.Exception ex) { Say("Couldn't paste: " + ex.Message); }
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert to saved", button))
        {
            var saved = EnemyModel.Load(EnemyName);
            if (saved != null) { editing[EnemyName] = saved; unsaved.Remove(EnemyName); selected = -1; rebuild = true; FrameModel(); Say("Back to the saved model."); }
            else Say("Nothing saved yet.");
        }
        if (GUILayout.Button("The game's look", button))
        {
            editing[EnemyName] = EnemyModel.Capture(EnemyName) ?? EnemyModel.Template(EnemyName);
            selected = -1;
            Changed();
            FrameModel();
            Say("Back to exactly how the game builds it.");
        }
        if (GUILayout.Button("Delete saved", button))
        {
            EnemyModel.Delete(EnemyName);
            Say("Deleted the saved model: the game goes back to its own look.");
        }
        GUILayout.EndHorizontal();
        bool use = GUILayout.Toggle(Model.useInGame, EnemyName == "Scour" ? "  Use in game (new scours are built from it once saved)"
            : EnemyName == "Player" ? "  Use in game (your body, seen from the drone, once saved)"
            : "  Use in game (only the scour and the player can, for now: this one's a design)");
        if (use != Model.useInGame) { Model.useInGame = use; Changed(); }
        GUILayout.Space(8f * unit);

        // The shapes.
        GUILayout.Label("Shapes", title);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Add:", label, GUILayout.Width(40f * unit));
        foreach (string shape in EnemyModel.Shapes)
            if (GUILayout.Button(shape, button)) AddPart(shape);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUI.enabled = selected >= 0;
        if (GUILayout.Button("Duplicate", button)) Duplicate(false);
        if (GUILayout.Button("Mirror", button)) Duplicate(true);
        if (GUILayout.Button("Delete", button)) { Model.parts.RemoveAt(selected); selected = Mathf.Min(selected, Model.parts.Count - 1); Changed(); }
        if (GUILayout.Button("Up", button) && selected > 0) Swap(selected, selected - 1);
        if (GUILayout.Button("Down", button) && selected < Model.parts.Count - 1) Swap(selected, selected + 1);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        listScroll = GUILayout.BeginScrollView(listScroll);
        if (GUILayout.Button("Joints, legs & notes", selected < 0 ? selectedButton : button)) selected = -1;
        for (int i = 0; i < Model.parts.Count; i++)
        {
            var part = Model.parts[i];
            string text = $"{part.name}   ({part.shape}, {part.attach}{(part.eye ? ", eye" : "")})";
            if (GUILayout.Button(text, i == selected ? selectedButton : button)) selected = i;
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawRight()
    {
        GUILayout.BeginArea(Inset(rightPanel));
        propScroll = GUILayout.BeginScrollView(propScroll);
        if (selected >= 0 && selected < Model.parts.Count) DrawPart(Model.parts[selected]);
        else DrawJoints();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawPart(ModelPart part)
    {
        GUILayout.Label("Shape", title);
        string name = GUILayout.TextField(part.name);
        if (name != part.name) { part.name = name; Changed(); }

        if (part.shape == "Mesh") GUILayout.Label($"A detailed shape: {part.mesh}. (Pick a simple shape below to swap it for one.)", small);
        int shape = GUILayout.Toolbar(System.Array.IndexOf(EnemyModel.Shapes, part.shape), EnemyModel.Shapes, button);
        if (shape >= 0 && EnemyModel.Shapes[shape] != part.shape) { part.shape = EnemyModel.Shapes[shape]; Changed(); }
        GUILayout.Label("On (moves with it)", small);
        int attach = GUILayout.Toolbar(System.Array.IndexOf(EnemyModel.Joints, part.attach), EnemyModel.Joints, button);
        if (attach >= 0 && EnemyModel.Joints[attach] != part.attach) { part.attach = EnemyModel.Joints[attach]; Changed(); }
        bool eye = GUILayout.Toggle(part.eye, "  An eye (glows; on the scour it widens and burns like its eyes)");
        if (eye != part.eye) { part.eye = eye; if (eye && part.glow <= 0f) part.glow = 4f; Changed(); }

        float h = Model.height;
        GUILayout.Space(6f * unit);
        GUILayout.Label("Position (m, from what it's on)", small);
        Vector3 pos = Vector3Field("pos", part.position, -h, h);
        GUILayout.Label("Rotation (degrees)", small);
        Vector3 rot = Vector3Field("rot", part.rotation, -180f, 180f);
        GUILayout.Label("Size (m)", small);
        Vector3 size = Vector3Field("size", part.scale, 0.0005f, h);
        if (pos != part.position || rot != part.rotation || size != part.scale)
        {
            part.position = pos;
            part.rotation = rot;
            part.scale = size;
            Moved(selected);
        }
        GUILayout.Space(6f * unit);
        GUILayout.Label("Colour", small);
        Color c = part.color;
        c.r = Number("Red", "r", c.r, 0f, 1f);
        c.g = Number("Green", "g", c.g, 0f, 1f);
        c.b = Number("Blue", "b", c.b, 0f, 1f);
        c.a = 1f;
        if (c != part.color) { part.color = c; unsaved.Add(EnemyName); } // (HighlightSelected colours it)
        float glow = Number("Glow", "glow", part.glow, 0f, 12f);
        if (!Mathf.Approximately(glow, part.glow))
        {
            bool kindChanged = (glow > 0f) != (part.glow > 0f); // (lit, or glowing: a different material)
            part.glow = glow;
            if (kindChanged) Changed(); else unsaved.Add(EnemyName);
        }
    }

    void DrawJoints()
    {
        var m = Model;
        float h = m.height;
        GUILayout.Label("Joints", title);
        GUILayout.Label("Where the head, arms and hips are (m, from the ground under it). Shapes on them move with them.", small);
        m.head = Track(LabelledVector("Head", "head", m.head, -h, h * 1.2f), m.head);
        m.armLeft = Track(LabelledVector("Left arm", "armL", m.armLeft, -h, h * 1.2f), m.armLeft);
        m.armRight = Track(LabelledVector("Right arm", "armR", m.armRight, -h, h * 1.2f), m.armRight);
        m.hipLeft = Track(LabelledVector("Left hip", "hipL", m.hipLeft, -h, h * 1.2f), m.hipLeft);
        m.hipRight = Track(LabelledVector("Right hip", "hipR", m.hipRight, -h, h * 1.2f), m.hipRight);
        m.mouth = Track(LabelledVector("Mouth (from the head)", "mouth", m.mouth, -h * 0.3f, h * 0.3f), m.mouth);

        GUILayout.Space(8f * unit);
        GUILayout.Label("Legs", title);
        bool legs = GUILayout.Toggle(m.legs, "  Legs (long, limp, dragged along the ground)");
        if (legs != m.legs) { m.legs = legs; Changed(); }
        if (m.legs)
        {
            m.thigh = Track(Number("Thigh", "thigh", m.thigh, 0.05f, h), m.thigh);
            m.shin = Track(Number("Shin", "shin", m.shin, 0.05f, h), m.shin);
            m.legThickness = Track(Number("Thickness", "legThick", m.legThickness, 0.005f, h * 0.2f), m.legThickness);
        }

        GUILayout.Space(8f * unit);
        GUILayout.Label("Size", title);
        float height = Number("Height (m)", "height", m.height, 0.2f, 80f);
        if (!Mathf.Approximately(height, m.height)) { m.height = height; Changed(); FrameModel(); }

        GUILayout.Space(8f * unit);
        GUILayout.Label("Notes (for whoever makes it real)", title);
        string notes = GUILayout.TextArea(m.notes, GUILayout.MinHeight(90f * unit));
        if (notes != m.notes) { m.notes = notes; unsaved.Add(EnemyName); }
    }

    float Track(float value, float before)
    {
        if (!Mathf.Approximately(value, before)) Changed();
        return value;
    }

    Vector3 Track(Vector3 value, Vector3 before)
    {
        if (value != before) Changed();
        return value;
    }

    /// <summary>A shape moved, turned or resized: just that shape is put right (not the whole model rebuilt).</summary>
    void Moved(int index)
    {
        unsaved.Add(EnemyName);
        if (built == null || index < 0 || index >= built.parts.Count || built.parts[index] == null) { rebuild = true; return; }
        var part = Model.parts[index];
        var t = built.parts[index].transform;
        t.localPosition = part.position;
        t.localRotation = Quaternion.Euler(part.rotation);
        t.localScale = part.scale;
    }

    Vector3 LabelledVector(string name, string id, Vector3 v, float min, float max)
    {
        GUILayout.Label(name, small);
        return Vector3Field(id, v, min, max);
    }

    Vector3 Vector3Field(string id, Vector3 v, float min, float max)
    {
        v.x = Number("x", id + "x", v.x, min, max);
        v.y = Number("y", id + "y", v.y, min, max);
        v.z = Number("z", id + "z", v.z, min, max);
        return v;
    }

    /// <summary>A slider with a box to type an exact number into.</summary>
    float Number(string name, string id, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, label, GUILayout.Width(80f * unit));
        float slid = GUILayout.HorizontalSlider(value, Mathf.Min(min, value), Mathf.Max(max, value), GUILayout.ExpandWidth(true));
        if (!Mathf.Approximately(slid, value)) { value = slid; typing.Remove(id); }

        string control = "num_" + id;
        GUI.SetNextControlName(control);
        bool focused = GUI.GetNameOfFocusedControl() == control;
        string shown = focused && typing.TryGetValue(id, out var t) ? t : value.ToString("0.###", CultureInfo.InvariantCulture);
        string typed = GUILayout.TextField(shown, GUILayout.Width(70f * unit));
        if (focused) typing[id] = typed; else typing.Remove(id);
        if (typed != shown && float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) value = f;
        GUILayout.EndHorizontal();
        return value;
    }

    // ---------------------------------------------------------------- Editing

    void AddPart(string shape)
    {
        var part = new ModelPart
        {
            name = shape,
            shape = shape,
            attach = "Body",
            position = Vector3.up * Model.height * 0.5f + Vector3.forward * Model.height * 0.15f,
            scale = Vector3.one * Model.height * 0.08f,
            color = Color.black,
        };
        Model.parts.Add(part);
        selected = Model.parts.Count - 1;
        Changed();
    }

    /// <summary>A copy of the picked shape, nudged aside; mirrored: flipped to the other side (and arm).</summary>
    void Duplicate(bool mirror)
    {
        var source = Model.parts[selected];
        var copy = JsonUtility.FromJson<ModelPart>(JsonUtility.ToJson(source));
        if (mirror)
        {
            copy.position.x = -copy.position.x;
            copy.rotation = new Vector3(copy.rotation.x, -copy.rotation.y, -copy.rotation.z);
            if (copy.attach == "ArmL") copy.attach = "ArmR";
            else if (copy.attach == "ArmR") copy.attach = "ArmL";
            copy.name = SwapSide(copy.name);
        }
        else copy.position += Vector3.right * Model.height * 0.05f;
        Model.parts.Insert(selected + 1, copy);
        selected++;
        Changed();
    }

    static string SwapSide(string name)
    {
        if (name.EndsWith(" L")) return name.Substring(0, name.Length - 2) + " R";
        if (name.EndsWith(" R")) return name.Substring(0, name.Length - 2) + " L";
        if (name.Contains("Left")) return name.Replace("Left", "Right");
        if (name.Contains("Right")) return name.Replace("Right", "Left");
        return name + " (mirrored)";
    }

    void Swap(int a, int b)
    {
        (Model.parts[a], Model.parts[b]) = (Model.parts[b], Model.parts[a]);
        selected = b;
        Changed();
    }

    // ---------------------------------------------------------------- Look

    void Say(string text)
    {
        status = text;
        statusUntil = Time.unscaledTime + 6f;
        Debug.Log("[Horror] Model editor: " + text);
    }

    Rect Inset(Rect r) => new Rect(r.x + 12f * unit, r.y + 10f * unit, r.width - 24f * unit, r.height - 20f * unit);

    static void Fill(Rect r, Color color)
    {
        var before = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = before;
    }

    void Styles()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label);
            small = new GUIStyle(GUI.skin.label) { wordWrap = true };
            button = new GUIStyle(GUI.skin.button);
            selectedButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }
        title.fontSize = Mathf.RoundToInt(17f * unit);
        title.normal.textColor = new Color(1f, 0.75f, 0.4f);
        label.fontSize = button.fontSize = selectedButton.fontSize = Mathf.RoundToInt(14f * unit);
        small.fontSize = Mathf.RoundToInt(12f * unit);
        label.normal.textColor = small.normal.textColor = new Color(0.9f, 0.86f, 0.78f);
        selectedButton.normal.textColor = new Color(1f, 0.8f, 0.4f);
    }
}
