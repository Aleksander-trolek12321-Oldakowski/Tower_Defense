using System.Collections.Generic;
using UnityEngine;

public class PathManager : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private List<PathManager> branches = new List<PathManager>();

    public bool HasBranches => branches.Count > 0;

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count) return null;
        return waypoints[index];
    }

    public int GetWaypointCount() => waypoints.Count;

    public PathManager GetBranch(int index)
    {
        if (index < 0 || index >= branches.Count) return null;
        return branches[index];
    }

    public int GetBranchCount() => branches?.Count ?? 0;

    public int FindWaypointIndexByPosition(Vector2 pos, float eps = 0.01f)
    {
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (Vector2.SqrMagnitude((Vector2)waypoints[i].position - pos) <= eps * eps)
                return i;
        }
        return -1;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        if (HasBranches)
        {
            Gizmos.color = Color.yellow;
            foreach (var b in branches)
            {
                if (b != null && waypoints.Count > 0 && b.waypoints.Count > 0)
                    Gizmos.DrawLine(waypoints[^1].position, b.waypoints[0].position);
            }
        }
    }
#endif
}
