using Godot;
using LooseChange.Player;

namespace LooseChange.World
{
    public partial class SurveillanceCameraController : StaticBody3D
    {
        public enum SurveillanceMode
        {
            TrackPlayer = 0,
            RandomSweep = 1
        }

        [Export]
        public SurveillanceMode Mode { get; set; } = SurveillanceMode.RandomSweep;

        [Export]
        public NodePath CameraPivotPath { get; set; }

        [Export]
        public float MinYawDegrees { get; set; } = 0f;

        [Export]
        public float MaxYawDegrees { get; set; } = 180f;

        [Export]
        public float TrackTurnSpeedDegrees { get; set; } = 120f;

        [Export]
        public float RandomTurnSpeedDegrees { get; set; } = 45f;

        [Export]
        public Vector2 RandomPauseRangeSeconds { get; set; } = new Vector2(0.2f, 1.2f);

        [Export]
        public NodePath PlayerPath { get; set; }

        [ExportGroup("Performance")]
        [Export(PropertyHint.Range, "0.05,5.0,0.05")]
        public float PlayerCheckIntervalSeconds { get; set; } = 0.25f;

        [Export(PropertyHint.Range, "0,500,1")]
        public float ActiveRangeMeters { get; set; } = 25f;

        private Node3D _cameraPivot;
        private PlayerController _player;
        private readonly RandomNumberGenerator _rng = new();

        private float _currentYawDegrees;
        private float _targetYawDegrees;
        private float _randomPauseTimer;

        // Throttled player cache / activation
        private float _playerCheckTimer;
        private Vector3 _cachedPlayerGlobalPos;
        private bool _hasCachedPlayerPos;
        private bool _isPlayerInRange;
        private float _activeRangeSq;

        public override void _Ready()
        {
            _rng.Randomize();

            _cameraPivot = ResolveCameraPivot();
            if (_cameraPivot == null)
            {
                GD.PushWarning($"{Name}: No camera pivot was found. Falling back to root rotation.");
                _cameraPivot = this;
            }

            _player = ResolvePlayer();

            _currentYawDegrees = Mathf.RadToDeg(_cameraPivot.Rotation.Y);
            _targetYawDegrees = _currentYawDegrees;

            _activeRangeSq = ActiveRangeMeters * ActiveRangeMeters;

            // Force immediate first check so we have a correct initial state
            _playerCheckTimer = 0f;
            RefreshPlayerCache(force: true);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            // Always do ONLY the throttled player check
            RefreshPlayerCache(force: false, delta: dt);

            // If player is not in range, do nothing else (no sweep timers, no yaw, nothing)
            if (!_isPlayerInRange)
            {
                return;
            }

            switch (Mode)
            {
                case SurveillanceMode.TrackPlayer:
                    UpdateTrackPlayer(dt);
                    break;

                case SurveillanceMode.RandomSweep:
                    UpdateRandomSweep(dt);
                    break;
            }
        }

        private void RefreshPlayerCache(bool force, float delta = 0f)
        {
            _playerCheckTimer -= delta;
            if (!force && _playerCheckTimer > 0f)
            {
                return;
            }

            _playerCheckTimer = Mathf.Max(0.05f, PlayerCheckIntervalSeconds);

            // Resolve player only on interval
            if (_player == null || !_player.IsInsideTree())
            {
                _player = ResolvePlayer();
            }

            if (_player == null || !_player.IsInsideTree())
            {
                _hasCachedPlayerPos = false;
                _isPlayerInRange = false;
                return;
            }

            // Sample player position only on interval
            _cachedPlayerGlobalPos = _player.GlobalPosition;
            _hasCachedPlayerPos = true;

            // 3D distance check using squared distance (fast)
            Vector3 camPos = GlobalPosition;
            float distSq = camPos.DistanceSquaredTo(_cachedPlayerGlobalPos);
            _isPlayerInRange = distSq <= _activeRangeSq;
        }

        private void UpdateTrackPlayer(float delta)
        {
            if (!_hasCachedPlayerPos)
            {
                return;
            }

            Vector3 localPlayer = ToLocal(_cachedPlayerGlobalPos);
            float targetYawDegrees = Mathf.RadToDeg(Mathf.Atan2(localPlayer.X, localPlayer.Z));
            targetYawDegrees = Mathf.Clamp(targetYawDegrees, MinYawDegrees, MaxYawDegrees);

            _currentYawDegrees = Mathf.MoveToward(_currentYawDegrees, targetYawDegrees, TrackTurnSpeedDegrees * delta);
            ApplyYaw(_currentYawDegrees);
        }

        private void UpdateRandomSweep(float delta)
        {
            if (_randomPauseTimer > 0f)
            {
                _randomPauseTimer -= delta;
                return;
            }

            _currentYawDegrees = Mathf.MoveToward(_currentYawDegrees, _targetYawDegrees, RandomTurnSpeedDegrees * delta);
            ApplyYaw(_currentYawDegrees);

            if (Mathf.IsEqualApprox(_currentYawDegrees, _targetYawDegrees))
            {
                _targetYawDegrees = _rng.RandfRange(MinYawDegrees, MaxYawDegrees);
                _randomPauseTimer = _rng.RandfRange(RandomPauseRangeSeconds.X, RandomPauseRangeSeconds.Y);
            }
        }

        private void ApplyYaw(float yawDegrees)
        {
            Vector3 pivotRotation = _cameraPivot.Rotation;
            pivotRotation.Y = Mathf.DegToRad(yawDegrees);
            _cameraPivot.Rotation = pivotRotation;
        }

        private Node3D ResolveCameraPivot()
        {
            Node3D pivot = GetNodeOrNull<Node3D>(CameraPivotPath);
            if (pivot != null)
            {
                return pivot;
            }

            Camera3D childCamera = FindChildOfType<Camera3D>(this);
            if (childCamera != null && childCamera.GetParent() is Node3D cameraParent)
            {
                return cameraParent;
            }

            return FindChildOfType<Node3D>(this);
        }

        private PlayerController ResolvePlayer()
        {
            PlayerController player = GetNodeOrNull<PlayerController>(PlayerPath);
            if (player != null)
            {
                return player;
            }

            SceneTree tree = GetTree();
            if (tree == null)
            {
                return null;
            }

            foreach (Node node in tree.GetNodesInGroup("player"))
            {
                if (node is PlayerController groupedPlayer)
                {
                    return groupedPlayer;
                }
            }

            return FindChildOfType<PlayerController>(tree.Root);
        }

        private static T FindChildOfType<T>(Node root) where T : class
        {
            if (root is T match)
            {
                return match;
            }

            foreach (Node child in root.GetChildren())
            {
                T nested = FindChildOfType<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
