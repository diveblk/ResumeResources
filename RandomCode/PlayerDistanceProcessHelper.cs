using Godot;
using LooseChange.Game.Core;
using LooseChange.Player;

namespace LooseChange.World
{
    /// <summary>
    /// Enables or disables processing flags on a target node based on player distance.
    /// Add this helper as a child node and point TargetNodePath to the node whose
    /// processing should be toggled.
    /// </summary>
    public partial class PlayerDistanceProcessHelper : Node3D
    {
        [ExportGroup("Target")]
        [Export]
        public NodePath TargetNodePath { get; set; }

        [Export]
        public NodePath PlayerPath { get; set; }

        [ExportGroup("Distance")]
        [Export(PropertyHint.Range, "0.05,5.0,0.05")]
        public float PlayerCheckIntervalSeconds { get; set; } = 0.25f;

        [Export(PropertyHint.Range, "0,500,1")]
        public float ActiveRangeMeters { get; set; } = 25f;

        [Export]
        public bool DisableWhenPlayerMissing { get; set; } = true;

        [ExportGroup("Processing Flags")]
        [Export]
        public bool ToggleProcess { get; set; } = true;

        [Export]
        public bool TogglePhysicsProcess { get; set; } = true;

        [Export]
        public bool ToggleProcessInput { get; set; } = false;

        [Export]
        public bool ToggleProcessUnhandledInput { get; set; } = false;

        [Export]
        public bool ToggleProcessUnhandledKeyInput { get; set; } = false;

        private Node _targetNode;
        private PlayerController _player;

        private bool _baseProcessEnabled;
        private bool _basePhysicsProcessEnabled;
        private bool _baseProcessInputEnabled;
        private bool _baseProcessUnhandledInputEnabled;
        private bool _baseProcessUnhandledKeyInputEnabled;

        private bool _isPlayerInRange;
        private bool _lastAppliedEnabledState;
        private bool _hasAppliedState;

        private float _playerCheckTimer;
        private float _activeRangeSq;

        public override void _Ready()
        {
            _targetNode = ResolveTargetNode();
            CacheBaseProcessingState();

            _activeRangeSq = ActiveRangeMeters * ActiveRangeMeters;
            _playerCheckTimer = 0f;
            RefreshPlayerRangeState(force: true);
            ApplyTargetState();
        }

        public override void _Process(double delta)
        {
            RefreshPlayerRangeState(force: false, delta: (float)delta);
            ApplyTargetState();
        }

        public void RefreshNow()
        {
            RefreshPlayerRangeState(force: true);
            ApplyTargetState();
        }

        private void RefreshPlayerRangeState(bool force, float delta = 0f)
        {
            _playerCheckTimer -= delta;
            if (!force && _playerCheckTimer > 0f)
            {
                return;
            }

            _playerCheckTimer = Mathf.Max(0.05f, PlayerCheckIntervalSeconds);
            _activeRangeSq = ActiveRangeMeters * ActiveRangeMeters;

            if (_player == null || !_player.IsInsideTree())
            {
                _player = ResolvePlayer();
            }

            if (_player == null || !_player.IsInsideTree())
            {
                _isPlayerInRange = !DisableWhenPlayerMissing;
                return;
            }

            float distanceSq = GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
            _isPlayerInRange = distanceSq <= _activeRangeSq;
        }

        private void ApplyTargetState()
        {
            if (_targetNode == null)
            {
                return;
            }

            bool shouldEnable = _isPlayerInRange;
            if (_hasAppliedState && _lastAppliedEnabledState == shouldEnable)
            {
                return;
            }

            _hasAppliedState = true;
            _lastAppliedEnabledState = shouldEnable;

            if (ToggleProcess)
            {
                _targetNode.SetProcess(shouldEnable && _baseProcessEnabled);
            }

            if (TogglePhysicsProcess)
            {
                _targetNode.SetPhysicsProcess(shouldEnable && _basePhysicsProcessEnabled);
            }

            if (ToggleProcessInput)
            {
                _targetNode.SetProcessInput(shouldEnable && _baseProcessInputEnabled);
            }

            if (ToggleProcessUnhandledInput)
            {
                _targetNode.SetProcessUnhandledInput(shouldEnable && _baseProcessUnhandledInputEnabled);
            }

            if (ToggleProcessUnhandledKeyInput)
            {
                _targetNode.SetProcessUnhandledKeyInput(shouldEnable && _baseProcessUnhandledKeyInputEnabled);
            }
        }

        private Node ResolveTargetNode()
        {
            Node target = GetNodeOrNull(TargetNodePath);
            if (target != null)
            {
                return target;
            }

            return GetParent();
        }

        private void CacheBaseProcessingState()
        {
            if (_targetNode == null)
            {
                return;
            }

            _baseProcessEnabled = _targetNode.IsProcessing();
            _basePhysicsProcessEnabled = _targetNode.IsPhysicsProcessing();
            _baseProcessInputEnabled = _targetNode.IsProcessingInput();
            _baseProcessUnhandledInputEnabled = _targetNode.IsProcessingUnhandledInput();
            _baseProcessUnhandledKeyInputEnabled = _targetNode.IsProcessingUnhandledKeyInput();
        }

        private PlayerController ResolvePlayer()
        {
            PlayerController player = GetNodeOrNull<PlayerController>(PlayerPath);
            if (player != null)
            {
                return player;
            }

            GameManager manager = GameManager.Instance;
            if (manager?.Player != null)
            {
                return manager.Player;
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
