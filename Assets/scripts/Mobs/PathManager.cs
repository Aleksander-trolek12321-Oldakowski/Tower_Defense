using System.Collections.Generic;
using UnityEngine;

public class PathManager : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private List<PathManager> branches = new List<PathManager>();

    public static Dictionary<string, PathManager> Instances = new Dictionary<string, PathManager>();

    public bool HasBranches => branches.Count > 0;

    private void OnEnable()
    {
        if (!Instances.ContainsKey(name))
            Instances[name] = this;
    }

    private void OnDisable()
    {
        if (Instances.ContainsKey(name))
            Instances.Remove(name);
    }

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

    public int GetBranchCount() => branches.Count;

    public int FindWaypointIndexByPosition(Vector2 pos, float epsilon = 0.5f)
    {
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            if (Vector2.Distance(waypoints[i].position, pos) <= epsilon)
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
