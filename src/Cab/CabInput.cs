using Godot;

namespace LastTrain.Cab;

/// <summary>
/// Mouse interaction with the cab, handled from the root viewport (the low-res SubViewport
/// doesn't receive input directly). Screen positions are scaled into the low-res viewport
/// to cast pick rays from the driver's camera.
///
///   Left drag on a lever: move it (mouse is captured while dragging; it latches on release).
///   Left click on a clickable (firebox door): do the thing.
///   Right drag: look around.
///   Hold E: lean out of the side window to see past the boiler (like real drivers do).
/// </summary>
public partial class CabInput : Node
{
    const float LeverPixelsForFullTravel = 260f;   // screen pixels of vertical drag for full lever travel
    const float LookDegreesPerPixel = 0.15f;
    const float PickReach = 3f;

    CabRig _cab = null!;
    SubViewport _viewport = null!;

    Lever? _dragging;
    bool _looking;
    Vector2 _restoreMouse;
    float _yaw;
    float _pitch = -15f;

    /// <summary>Head position when leaning out of the right-hand side window.</summary>
    public static readonly Vector3 LeanPosition = new(1.85f, 3.2f, -0.45f);

    public string HoverText { get; private set; } = "";
    /// <summary>For tools: lean out without a key held.</summary>
    public bool ForceLean { get; set; }
    public bool Leaning => ForceLean || Input.IsPhysicalKeyPressed(Key.E);

    public CabInput Init(CabRig cab, SubViewport viewport)
    {
        _cab = cab;
        _viewport = viewport;
        Name = "CabInput";
        return this;
    }

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed)
                {
                    var target = Pick(mb.Position);
                    if (target?.Lever is Lever lever)
                    {
                        _dragging = lever;
                        lever.Model.BeginDrag();
                        Capture(mb.Position);
                    }
                    else
                    {
                        target?.OnClick?.Invoke();
                    }
                }
                else if (_dragging != null)
                {
                    _dragging.Model.EndDrag();
                    _dragging = null;
                    Release();
                }
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Right } mb:
                _looking = mb.Pressed;
                if (mb.Pressed) Capture(mb.Position); else Release();
                break;

            case InputEventMouseMotion mm:
                if (_dragging != null)
                {
                    // Drag up = push the lever away = increase.
                    _dragging.Model.Drag(-mm.Relative.Y / LeverPixelsForFullTravel * _dragging.Model.Range);
                }
                else if (_looking)
                {
                    _yaw = Mathf.Clamp(_yaw - mm.Relative.X * LookDegreesPerPixel, -120f, 120f);
                    _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * LookDegreesPerPixel, -65f, 45f);
                    _cab.Camera.RotationDegrees = new Vector3(_pitch, _yaw, 0);
                }
                break;
        }
    }

    public override void _Process(double delta)
    {
        Vector3 head = Leaning ? LeanPosition : CabRig.EyePosition;
        float blend = 1f - Mathf.Exp(-10f * (float)delta);
        _cab.Camera.Position = _cab.Camera.Position.Lerp(head, blend);

        if (_dragging != null)
        {
            HoverText = _dragging.Describe();
            return;
        }
        if (_looking)
        {
            HoverText = "";
            return;
        }
        HoverText = Pick(GetViewport().GetMousePosition())?.Describe() ?? "";
    }

    void Capture(Vector2 screenPosition)
    {
        _restoreMouse = screenPosition;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    void Release()
    {
        if (_dragging != null || _looking) return;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Input.WarpMouse(_restoreMouse);
    }

    PickTarget? Pick(Vector2 screenPosition)
    {
        Vector2 screenSize = GetViewport().GetVisibleRect().Size;
        if (screenSize.X <= 0 || screenSize.Y <= 0) return null;
        Vector2 p = screenPosition * ((Vector2)_viewport.Size / screenSize);

        var camera = _cab.Camera;
        Vector3 from = camera.ProjectRayOrigin(p);
        Vector3 to = from + camera.ProjectRayNormal(p) * PickReach;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        var hit = camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return null;
        return hit["collider"].AsGodotObject() as PickTarget;
    }
}
