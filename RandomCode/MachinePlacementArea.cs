using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LooseChange.Game.Core;
using LooseChange.Game.Production.Stations;
using LooseChange.Scripts.Machine;
using LooseChange.Scripts.Systems.Tables;

public enum PlacementItemType
{
    Machine,
    ProductionStation,
    Storage,
}

/// <summary>
/// Defines a spatial region that supports player machine placement, accumulates heat from
/// nearby machines, and renders a visual indicator that grows / brightens with heat.
/// </summary>
public partial class MachinePlacementArea : Area3D
{
    private static readonly List<MachinePlacementArea> _activeAreas = new();
    private static StandardMaterial3D _sharedGuideMaterial;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float CrimeLevel { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float FootTraffic { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "1,500,1")]
    public float MaxHeat { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0,50,0.1")]
    public float HeatDissipationPerSecond { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float MinHeatRadius { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.5,40,0.1")]
    public float MaxHeatRadius { get; set; } = 6.0f;

    [Export]
    public NodePath PlacementMarkerPath { get; set; }

    [Export]
    public string AreaId { get; set; } = string.Empty;

    [Export]
    public bool TerrainHelperEnabled { get; set; } = true;

    [Export]
    public Array<PlacementItemType> AllowedPlacementItems { get; set; }
        = new()
        {
        PlacementItemType.Machine,
        PlacementItemType.ProductionStation,
        PlacementItemType.Storage
        };

    [Export]
    public bool RequireFullPlacementBounds { get; set; } = false;

    public float CurrentHeat { get; private set; }

    private readonly List<MachineBase> _registeredMachines = new();
    private CollisionShape3D _boundsShape;
    private BoxShape3D _boxShape;
    private MeshInstance3D _placementGuide;
    private MeshInstance3D _heatVisual;
    private StandardMaterial3D _heatMaterial;
    private bool _highlighted;

    public Marker3D PlacementMarker => GetNodeOrNull<Marker3D>(PlacementMarkerPath);

    public Vector3 GetPlacementPosition()
    {
        Vector3 position = PlacementMarker?.GlobalTransform.Origin ?? GlobalTransform.Origin;

        if (TerrainHelper.IsReady)
        {
            if (TerrainHelperEnabled)
            {
                position.Y = TerrainHelper.GetHeightAt(position);
            }            
        }

        return position;
    }

    public Transform3D GetPlacementTransform(Vector3? positionOverride = null)
    {
        Transform3D transform = PlacementMarker?.GlobalTransform ?? GlobalTransform;
        transform.Basis = transform.Basis.Orthonormalized();

        if (positionOverride.HasValue)
        {
            transform.Origin = positionOverride.Value;
        }

        if (TerrainHelper.IsReady)
        {
            if (TerrainHelperEnabled)
            {
                Vector3 origin = transform.Origin;
                origin.Y = TerrainHelper.GetHeightAt(origin);
                transform.Origin = origin;
            }
        }

        return transform;
    }

    public override void _Ready()
    {
        base._Ready();
        if (string.IsNullOrEmpty(AreaId))
        {
            AreaId = Name;
        }

        if (!Engine.IsEditorHint() && TerrainHelper.IsReady)
        {
            if (TerrainHelperEnabled)
            {
                Vector3 pos = GlobalPosition;
                pos.Y = TerrainHelper.GetHeightAt(pos);
                GlobalPosition = pos;
            }  
        }

        _boundsShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        _boxShape = _boundsShape?.Shape as BoxShape3D;
        InitializePlacementGuide();
        InitializeHeatVisual();
        SetProcess(true);
    }

    public bool ContainsBody(Node3D body)
    {
        if (body == null)
            return false;

        Array<Node3D> bodies = GetOverlappingBodies();
        foreach (Node3D candidate in bodies)
        {
            if (candidate == body)
                return true;
        }

        return false;
    }

    public static MachinePlacementArea FindContainingArea(Node3D body)
    {
        if (body == null)
            return null;

        for (int i = _activeAreas.Count - 1; i >= 0; i--)
        {
            MachinePlacementArea area = _activeAreas[i];
            if (!GodotObject.IsInstanceValid(area))
            {
                _activeAreas.RemoveAt(i);
                continue;
            }

            if (!area.Monitoring || !area.Monitorable)
                continue;

            if (area.ContainsBody(body))
                return area;
        }

        return null;
    }

    public static MachinePlacementArea FindAreaInRange(Vector3 position, float range)
    {
        if (range <= 0f)
        {
            return null;
        }

        float rangeSquared = range * range;
        MachinePlacementArea closest = null;
        float closestSquared = rangeSquared;

        for (int i = _activeAreas.Count - 1; i >= 0; i--)
        {
            MachinePlacementArea area = _activeAreas[i];
            if (!GodotObject.IsInstanceValid(area))
            {
                _activeAreas.RemoveAt(i);
                continue;
            }

            if (!area.Monitoring || !area.Monitorable)
            {
                continue;
            }

            float distanceSquared = area.GetPlacementPosition().DistanceSquaredTo(position);
            if (distanceSquared <= closestSquared)
            {
                closestSquared = distanceSquared;
                closest = area;
            }
        }

        return closest;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted)
        {
            return;
        }

        _highlighted = highlighted;

        if (_placementGuide != null)
        {
            _placementGuide.Visible = highlighted;
        }
    }

    public bool TryProjectCursor(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 surfacePoint)
    {
        surfacePoint = GetPlacementPosition();

        if (_boundsShape == null || _boxShape == null)
        {
            return false;
        }

        Vector3 normal = _boundsShape.GlobalTransform.Basis.Y.Normalized();
        Vector3 planeOrigin = _boundsShape.GlobalTransform.Origin - normal * (_boxShape.Size.Y * 0.5f);
        Plane plane = new Plane(normal, planeOrigin);

        Vector3? intersection = plane.IntersectsRay(rayOrigin, rayDirection);
        if (intersection == null)
        {
            // No hit
            return false;
        }

        Vector3 rawPoint = intersection.Value;

        surfacePoint = ClampPointToBounds(rawPoint);
        return true;
    }

    public bool AllowsPlacement(Node placementNode)
    {
        if (placementNode == null)
        {
            return false;
        }

        if (AllowedPlacementItems == null || AllowedPlacementItems.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < AllowedPlacementItems.Count; i++)
        {
            if (IsPlacementTypeAllowed(AllowedPlacementItems[i], placementNode))
            {
                return true;
            }
        }

        return false;
    }

    public bool AllowsPlacement(Node3D placementNode, Transform3D placementTransform)
    {
        if (!AllowsPlacement(placementNode))
        {
            return false;
        }

        if (!RequireFullPlacementBounds)
        {
            return true;
        }

        return IsPlacementInsideBounds(placementNode, placementTransform);
    }

    private static bool IsPlacementTypeAllowed(PlacementItemType placementType, Node placementNode)
    {
        switch (placementType)
        {
            case PlacementItemType.Machine:
                return placementNode is MachineBase;
            case PlacementItemType.ProductionStation:
                return placementNode is ProductionStationBase;
            case PlacementItemType.Storage:
                return placementNode is LooseChange.Scripts.Props.Storage.StoragePropBase;
            default:
                return false;
        }
    }

    private bool IsPlacementInsideBounds(Node3D placementNode, Transform3D placementTransform)
    {
        if (_boundsShape == null || _boxShape == null || placementNode == null)
        {
            return true;
        }

        List<CollisionShape3D> shapes = new();
        CollectCollisionShapes(placementNode, shapes);

        if (shapes.Count == 0)
        {
            return true;
        }

        Transform3D boundsTransform = _boundsShape.GlobalTransform;
        Vector3 halfExtents = _boxShape.Size * 0.5f;
        const float tolerance = 0.01f;
        Transform3D placementInverse = placementNode.GlobalTransform.AffineInverse();

        for (int i = 0; i < shapes.Count; i++)
        {
            CollisionShape3D shapeNode = shapes[i];
            Shape3D shape = shapeNode?.Shape;
            if (shape == null)
            {
                continue;
            }

            Aabb shapeAabb = GetLocalAabb(shape);
            Transform3D localToPlacement = placementInverse * shapeNode.GlobalTransform;
            Transform3D proposedShapeTransform = placementTransform * localToPlacement;

            if (!AreAabbCornersWithinBounds(shapeAabb, proposedShapeTransform, boundsTransform, halfExtents, tolerance))
            {
                return false;
            }
        }

        return true;
    }

    private static Aabb GetLocalAabb(Shape3D shape)
    {
        switch (shape)
        {
            case BoxShape3D box:
                {
                    Vector3 size = box.Size;
                    return new Aabb(-size * 0.5f, size);
                }

            case SphereShape3D sphere:
                {
                    float r = sphere.Radius;
                    Vector3 size = new Vector3(r * 2f, r * 2f, r * 2f);
                    return new Aabb(new Vector3(-r, -r, -r), size);
                }

            case CapsuleShape3D capsule:
                {
                    float r = capsule.Radius;
                    // In Godot, Capsule height is the cylinder part (the hemispheres add radius on each end)
                    float totalY = capsule.Height + (2f * r);
                    return new Aabb(new Vector3(-r, -totalY * 0.5f, -r), new Vector3(2f * r, totalY, 2f * r));
                }

            case CylinderShape3D cylinder:
                {
                    float r = cylinder.Radius;
                    float h = cylinder.Height;
                    return new Aabb(new Vector3(-r, -h * 0.5f, -r), new Vector3(2f * r, h, 2f * r));
                }

            // Fallback for more complex shapes (Convex/Concave/etc.)
            default:
                {
                    var debugMesh = shape.GetDebugMesh();
                    if (debugMesh != null)
                        return debugMesh.GetAabb();

                    // Worst-case fallback
                    return new Aabb(Vector3.Zero, Vector3.Zero);
                }
        }
    }

    private static void CollectCollisionShapes(Node node, List<CollisionShape3D> results)
    {
        if (node == null)
        {
            return;
        }

        if (node is CollisionShape3D collisionShape)
        {
            results.Add(collisionShape);
        }

        foreach (Node child in node.GetChildren())
        {
            CollectCollisionShapes(child, results);
        }
    }

    private static bool AreAabbCornersWithinBounds(
        Aabb aabb,
        Transform3D shapeTransform,
        Transform3D boundsTransform,
        Vector3 halfExtents,
        float tolerance)
    {
        Vector3 min = aabb.Position;
        Vector3 max = aabb.Position + aabb.Size;

        Vector3[] corners =
        {
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 worldPoint = shapeTransform * corners[i];
            Vector3 localPoint = boundsTransform.AffineInverse() * worldPoint;

            if (Mathf.Abs(localPoint.X) > halfExtents.X + tolerance)
            {
                return false;
            }

            if (Mathf.Abs(localPoint.Z) > halfExtents.Z + tolerance)
            {
                return false;
            }
        }

        return true;
    }

    public Vector3 ClampPointToBounds(Vector3 worldPoint)
    {
        if (_boundsShape == null || _boxShape == null)
        {
            return worldPoint;
        }

        Transform3D boundsTransform = _boundsShape.GlobalTransform;
        Vector3 localPoint = boundsTransform.AffineInverse() * worldPoint;
        Vector3 halfExtents = _boxShape.Size * 0.5f;

        localPoint.X = Mathf.Clamp(localPoint.X, -halfExtents.X, halfExtents.X);
        localPoint.Z = Mathf.Clamp(localPoint.Z, -halfExtents.Z, halfExtents.Z);
        localPoint.Y = -halfExtents.Y + 0.01f;

        return boundsTransform * localPoint;
    }

    public override void _EnterTree()
    {
        base._EnterTree();

        if (!_activeAreas.Contains(this))
        {
            _activeAreas.Add(this);
        }

        GameManager.Instance?.RegisterPlacementArea(this);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _activeAreas.Remove(this);

        _registeredMachines.Clear();
        GameManager.Instance?.UnregisterPlacementArea(this);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdatePlacementGuideTransform();
        UpdateHeatVisualTransform();
        UpdateHeat((float)delta);
    }

    public void RegisterMachine(MachineBase machine)
    {
        if (machine == null || _registeredMachines.Contains(machine))
        {
            return;
        }

        _registeredMachines.Add(machine);
    }

    public void UnregisterMachine(MachineBase machine)
    {
        if (machine == null)
        {
            return;
        }

        _registeredMachines.Remove(machine);
    }

    public void ReduceHeat(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        CurrentHeat = Mathf.Max(0f, CurrentHeat - amount);
        UpdateHeatVisual();
    }

    public void AddHeat(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        CurrentHeat = Mathf.Clamp(CurrentHeat + amount, 0f, MaxHeat);
        UpdateHeatVisual();
    }

    public PlacementAreaStateRecord CaptureState()
    {
        if (string.IsNullOrEmpty(AreaId))
        {
            AreaId = Name;
        }

        return new PlacementAreaStateRecord
        {
            AreaId = AreaId,
            CurrentHeat = CurrentHeat,
        };
    }

    public void ApplyState(PlacementAreaStateRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.AreaId))
        {
            return;
        }

        AreaId = record.AreaId;
        CurrentHeat = Mathf.Clamp(record.CurrentHeat, 0f, MaxHeat);
        UpdateHeatVisual();
    }

    private void InitializePlacementGuide()
    {
        if (_boxShape == null)
        {
            return;
        }

        _placementGuide = new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            Mesh = new BoxMesh
            {
                Size = new Vector3(_boxShape.Size.X, 0.05f, _boxShape.Size.Z),
            },
        };

        if (_sharedGuideMaterial == null)
        {
            _sharedGuideMaterial = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.2f, 0.8f, 1f, 0.3f),
            };
        }

        _placementGuide.MaterialOverride = _sharedGuideMaterial;
        AddChild(_placementGuide);
        UpdatePlacementGuideTransform();
    }

    private void InitializeHeatVisual()
    {
        _heatVisual = new MeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            Mesh = new CylinderMesh
            {
                Height = 0.1f,
                TopRadius = 1f,
                BottomRadius = 1f,
                RadialSegments = 24,
            },
        };

        _heatMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            AlbedoColor = new Color(1f, 0.2f, 0.1f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.1f, 0.05f),
        };

        _heatVisual.MaterialOverride = _heatMaterial;
        AddChild(_heatVisual);
        UpdateHeatVisualTransform();
    }

    private void UpdatePlacementGuideTransform()
    {
        if (_placementGuide == null || _boundsShape == null || _boxShape == null)
        {
            return;
        }

        Transform3D transform = _boundsShape.Transform;
        Vector3 offset = new Vector3(0, (-_boxShape.Size.Y * 0.5f) + 0.025f, 0);
        Transform3D localTransform = transform.Translated(offset);
        _placementGuide.Transform = localTransform;
    }

    private void UpdateHeatVisualTransform()
    {
        if (_heatVisual == null || _boundsShape == null || _boxShape == null)
        {
            return;
        }

        Transform3D transform = _boundsShape.Transform;
        Vector3 offset = new Vector3(0, (-_boxShape.Size.Y * 0.5f) + 0.02f, 0);
        _heatVisual.Transform = transform.Translated(offset);
    }

    private void UpdateHeat(float delta)
    {
        float totalContribution = 0f;

        for (int i = _registeredMachines.Count - 1; i >= 0; i--)
        {
            MachineBase machine = _registeredMachines[i];
            if (machine == null || !GodotObject.IsInstanceValid(machine))
            {
                _registeredMachines.RemoveAt(i);
                continue;
            }

            totalContribution += Mathf.Max(0f, machine.GetHeatContribution());
        }

        if (totalContribution > 0f)
        {
            CurrentHeat += totalContribution * delta;
        }

        if (HeatDissipationPerSecond > 0f)
        {
            CurrentHeat = Mathf.Max(0f, CurrentHeat - HeatDissipationPerSecond * delta);
        }

        CurrentHeat = Mathf.Clamp(CurrentHeat, 0f, Mathf.Max(MaxHeat, 0.001f));
        UpdateHeatVisual();
    }

    private void UpdateHeatVisual()
    {
        if (_heatVisual == null || _heatMaterial == null)
        {
            return;
        }

        bool visible = CurrentHeat > 0.01f;
        _heatVisual.Visible = visible;

        if (!visible)
        {
            return;
        }

        // Clamp01 replacement
        float raw = CurrentHeat / Mathf.Max(MaxHeat, 0.001f);
        float normalized = Mathf.Clamp(raw, 0f, 1f);

        float radius = Mathf.Lerp(
            Mathf.Max(0.1f, MinHeatRadius),
            Mathf.Max(MinHeatRadius, MaxHeatRadius),
            normalized
        );

        Vector3 scale = _heatVisual.Scale;
        scale.X = radius;
        scale.Z = radius;
        scale.Y = Mathf.Lerp(0.05f, 0.25f, normalized);
        _heatVisual.Scale = scale;

        float alpha = Mathf.Lerp(0.15f, 0.75f, normalized);
        Color color = new Color(
            1f,
            Mathf.Lerp(0.25f, 0.05f, normalized),
            0.05f,
            alpha
        );

        _heatMaterial.AlbedoColor = color;

        // WithAlpha replacement – copy and override A
        Color emissionColor = color;
        emissionColor.A = 1f;
        _heatMaterial.Emission = emissionColor;
    }
}
