using System;
using System.Collections.Generic;
using Godot;
using LooseChange.Scripts.World;

namespace LooseChange.World
{
    public partial class BuildingNpcPathRoute : Node3D
    {
        [ExportGroup("Route")]
        [Export]
        public LocationZone.LocationType DestinationType { get; set; } = LocationZone.LocationType.Cafe;

        [Export]
        public NodePath EntryWaypointRootPath { get; set; } = new("EntryWaypoints");

        [Export]
        public NodePath WorkstationPath { get; set; } = new("WorkstationPoint");

        [Export(PropertyHint.Range, "0,32,1")]
        public int DoorWaitWaypointIndex { get; set; } = 0;

        private readonly List<Node3D> _cachedEntryWaypoints = new();
        private readonly List<List<Node3D>> _cachedEntryWaypointRoutes = new();
        private readonly Dictionary<Node3D, SlidingDoorInteraction> _doorInteractionByWaypoint = new();
        private int _lastSelectedRouteIndex;

        public override void _Ready()
        {
            AddToGroup("npc_building_routes");
            CacheEntryWaypoints();
        }

        public SlidingDoorInteraction GetDoorInteractionForWaypoint(Node3D waypoint)
        {
            if (waypoint == null)
            {
                return null;
            }

            if (_doorInteractionByWaypoint.Count == 0)
            {
                CacheEntryWaypoints();
            }

            if (_doorInteractionByWaypoint.TryGetValue(waypoint, out SlidingDoorInteraction interaction))
            {
                return interaction;
            }

            EntryWaypoints routeRoot = waypoint.GetParent() as EntryWaypoints;
            return ResolveDoorInteraction(routeRoot);
        }

        public IReadOnlyList<Node3D> GetEntryWaypoints()
        {
            if (_cachedEntryWaypoints.Count == 0)
            {
                CacheEntryWaypoints();
            }

            return _cachedEntryWaypoints;
        }

        public IReadOnlyList<Node3D> GetEntryWaypointsFrom(Vector3 origin)
        {
            if (_cachedEntryWaypointRoutes.Count == 0)
            {
                CacheEntryWaypoints();
            }

            if (_cachedEntryWaypointRoutes.Count == 0)
            {
                return _cachedEntryWaypoints;
            }

            _lastSelectedRouteIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _cachedEntryWaypointRoutes.Count; i++)
            {
                List<Node3D> route = _cachedEntryWaypointRoutes[i];
                if (route.Count == 0)
                {
                    continue;
                }

                float distance = origin.DistanceSquaredTo(route[0].GlobalPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    _lastSelectedRouteIndex = i;
                }
            }

            return _cachedEntryWaypointRoutes[_lastSelectedRouteIndex];
        }

        public IReadOnlyList<Node3D> BuildExitWaypoints()
        {
            if (_cachedEntryWaypointRoutes.Count == 0)
            {
                CacheEntryWaypoints();
            }

            if (_cachedEntryWaypointRoutes.Count == 0)
            {
                return new List<Node3D>();
            }

            int selectedIndex = Mathf.Clamp(_lastSelectedRouteIndex, 0, _cachedEntryWaypointRoutes.Count - 1);
            List<Node3D> selectedRoute = _cachedEntryWaypointRoutes[selectedIndex];

            List<Node3D> exitWaypoints = new();
            for (int i = selectedRoute.Count - 1; i >= 0; i--)
            {
                exitWaypoints.Add(selectedRoute[i]);
            }

            return exitWaypoints;
        }

        private void CacheEntryWaypoints()
        {
            _cachedEntryWaypoints.Clear();
            _cachedEntryWaypointRoutes.Clear();
            _doorInteractionByWaypoint.Clear();
            _lastSelectedRouteIndex = 0;

            List<Node3D> waypointRootsInSequence = ResolveEntryWaypointRootsInSequence();
            Marker3D workstation = GetNodeOrNull<Marker3D>(WorkstationPath);

            List<Node3D> combinedRoute = new();

            foreach (Node3D routeRootNode in waypointRootsInSequence)
            {
                List<Node3D> segment = new();
                EntryWaypoints routeRoot = routeRootNode as EntryWaypoints;
                SlidingDoorInteraction segmentDoorInteraction = ResolveDoorInteraction(routeRoot);

                foreach (Node child in routeRootNode.GetChildren())
                {
                    if (child is Node3D waypoint)
                    {
                        segment.Add(waypoint);
                    }
                }

                segment.Sort(CompareWaypointOrder);

                foreach (Node3D waypoint in segment)
                {
                    _doorInteractionByWaypoint[waypoint] = segmentDoorInteraction;
                }

                combinedRoute.AddRange(segment);
            }

            if (workstation != null)
            {
                combinedRoute.Add(workstation);
            }

            if (combinedRoute.Count > 0)
            {
                _cachedEntryWaypointRoutes.Add(combinedRoute);
                _cachedEntryWaypoints.AddRange(combinedRoute);
            }
        }

        private List<Node3D> ResolveEntryWaypointRootsInSequence()
        {
            List<Node3D> roots = new();

            Node3D configuredRoot = GetNodeOrNull<Node3D>(EntryWaypointRootPath);

            string rootPrefix = null;
            if (configuredRoot != null)
            {
                rootPrefix = GetWaypointRootPrefix(configuredRoot.Name.ToString());
            }

            foreach (Node child in GetChildren())
            {
                if (child is not Node3D waypointRoot)
                {
                    continue;
                }

                string name = waypointRoot.Name.ToString();

                if (rootPrefix != null)
                {
                    if (!name.StartsWith(rootPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                else if (!name.StartsWith("EntryWaypoints", StringComparison.Ordinal))
                {
                    continue;
                }

                roots.Add(waypointRoot);
            }

            roots.Sort((a, b) =>
            {
                int seqA = GetPathSequenceOrDefault(a);
                int seqB = GetPathSequenceOrDefault(b);

                int bySeq = seqA.CompareTo(seqB);
                if (bySeq != 0)
                {
                    return bySeq;
                }

                return string.CompareOrdinal(a.Name.ToString(), b.Name.ToString());
            });

            return roots;
        }

        private static int GetPathSequenceOrDefault(Node node)
        {
            if (node is EntryWaypoints typed)
            {
                return typed.PathSequence;
            }

            return 0;
        }

        private static int CompareWaypointOrder(Node3D a, Node3D b)
        {
            string nameA = a?.Name.ToString() ?? string.Empty;
            string nameB = b?.Name.ToString() ?? string.Empty;

            int rankA = GetWaypointOrderRank(nameA);
            int rankB = GetWaypointOrderRank(nameB);

            if (rankA != rankB)
            {
                return rankA.CompareTo(rankB);
            }

            return string.CompareOrdinal(nameA, nameB);
        }

        private static int GetWaypointOrderRank(string waypointName)
        {
            if (waypointName.StartsWith("OutsideDoor", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (waypointName.StartsWith("InsideDoor", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private static string GetWaypointRootPrefix(string rootName)
        {
            if (string.IsNullOrEmpty(rootName))
            {
                return "EntryWaypoints";
            }

            int index = rootName.Length - 1;
            while (index >= 0 && char.IsDigit(rootName[index]))
            {
                index--;
            }

            if (index < 0)
            {
                return "EntryWaypoints";
            }

            return rootName.Substring(0, index + 1);
        }

        private SlidingDoorInteraction ResolveDoorInteraction(EntryWaypoints entryWaypoints)
        {
            if (entryWaypoints == null)
            {
                return null;
            }

            return entryWaypoints.GetNodeOrNull<SlidingDoorInteraction>(entryWaypoints.DoorInteractionPath);
        }
    }
}
