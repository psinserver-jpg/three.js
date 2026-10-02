using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    public class NavNode
    {
        public int id;
        public Vector3 pos;
        public readonly List<int> links = new List<int>();
        public int component = -1;
    }

    /// <summary>
    /// Runtime-generated walk graph. The map area is sampled on a grid (multiple floors supported),
    /// nodes are linked when a character could actually walk between them, and A* runs over the result.
    /// This avoids needing a baked NavMesh asset.
    /// </summary>
    public class NavGraph
    {
        public readonly List<NavNode> nodes = new List<NavNode>();
        int mainComponent;
        const float Cell = 2.5f;

        // A* scratch
        float[] g;
        int[] from;
        int[] stamp;
        bool[] closed;
        int search;

        public static NavGraph Build(Bounds area)
        {
            var graph = new NavGraph();
            graph.Generate(area);
            return graph;
        }

        void Generate(Bounds area)
        {
            int nx = Mathf.CeilToInt(area.size.x / Cell);
            int nz = Mathf.CeilToInt(area.size.z / Cell);
            var grid = new List<int>[nx, nz];
            var hits = new RaycastHit[16];
            float top = area.max.y + 20f;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = area.min.x + (i + 0.5f) * Cell;
                    float z = area.min.z + (j + 0.5f) * Cell;
                    int n = Physics.RaycastNonAlloc(new Vector3(x, top, z), Vector3.down, hits, top + 5f, Layers.World, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++)
                    {
                        var h = hits[k];
                        if (h.normal.y < 0.75f) continue;
                        Vector3 p = h.point;
                        if (p.y < -0.5f) continue;
                        if (Physics.CheckCapsule(p + Vector3.up * 0.75f, p + Vector3.up * 1.7f, 0.45f, Layers.World, QueryTriggerInteraction.Ignore))
                            continue;
                        bool dup = false;
                        if (grid[i, j] != null)
                            foreach (int id in grid[i, j])
                                if (Mathf.Abs(nodes[id].pos.y - p.y) < 0.5f) { dup = true; break; }
                        if (dup) continue;
                        var node = new NavNode { id = nodes.Count, pos = p };
                        nodes.Add(node);
                        if (grid[i, j] == null) grid[i, j] = new List<int>();
                        grid[i, j].Add(node.id);
                    }
                }

            int[,] offsets = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (grid[i, j] == null) continue;
                    foreach (int a in grid[i, j])
                        for (int o = 0; o < 4; o++)
                        {
                            int ni = i + offsets[o, 0], nj = j + offsets[o, 1];
                            if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || grid[ni, nj] == null) continue;
                            foreach (int b in grid[ni, nj])
                                if (CanTraverse(nodes[a].pos, nodes[b].pos))
                                {
                                    nodes[a].links.Add(b);
                                    nodes[b].links.Add(a);
                                }
                        }
                }

            // label connected components and keep the largest as the playable network
            int comp = 0, bestComp = 0, bestSize = 0;
            var queue = new Queue<int>();
            foreach (var n in nodes)
            {
                if (n.component >= 0) continue;
                int size = 0;
                n.component = comp;
                queue.Enqueue(n.id);
                while (queue.Count > 0)
                {
                    var cur = nodes[queue.Dequeue()];
                    size++;
                    foreach (int l in cur.links)
                        if (nodes[l].component < 0) { nodes[l].component = comp; queue.Enqueue(l); }
                }
                if (size > bestSize) { bestSize = size; bestComp = comp; }
                comp++;
            }
            mainComponent = bestComp;

            g = new float[nodes.Count];
            from = new int[nodes.Count];
            stamp = new int[nodes.Count];
            closed = new bool[nodes.Count];
        }

        static bool CanTraverse(Vector3 a, Vector3 b)
        {
            if (Mathf.Abs(a.y - b.y) > 2.2f) return false;
            Vector3 d = b - a;
            float dist = d.magnitude;
            if (Physics.CapsuleCast(a + Vector3.up * 1.0f, a + Vector3.up * 1.6f, 0.3f, d / dist, dist, Layers.World, QueryTriggerInteraction.Ignore))
                return false;
            float prev = a.y;
            for (int s = 1; s <= 4; s++)
            {
                float t = s / 5f;
                Vector3 q = Vector3.Lerp(a, b, t);
                RaycastHit h;
                if (!Physics.Raycast(q + Vector3.up * 1.4f, Vector3.down, out h, 3.5f, Layers.World, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(h.point.y - prev) > 0.55f) return false;
                prev = h.point.y;
            }
            return Mathf.Abs(b.y - prev) <= 0.55f;
        }

        public int Nearest(Vector3 p)
        {
            int best = -1;
            float bestCost = float.MaxValue;
            foreach (var n in nodes)
            {
                if (n.component != mainComponent) continue;
                Vector3 d = n.pos - p;
                float dy = d.y;
                d.y = 0f;
                float cost = d.sqrMagnitude + dy * dy * 6f;
                if (dy > 1.2f) cost += 400f; // nodes well above us are usually not directly reachable
                if (cost < bestCost) { bestCost = cost; best = n.id; }
            }
            return best;
        }

        public Vector3 NodePos(int id) { return nodes[id].pos; }

        /// <summary>A* from p to q. Writes waypoint positions (excluding start) into result.</summary>
        public bool FindPath(Vector3 p, Vector3 q, List<Vector3> result)
        {
            result.Clear();
            if (nodes.Count == 0) return false;
            int s = Nearest(p), t = Nearest(q);
            if (s < 0 || t < 0) return false;
            search++;
            var open = new MinHeap();
            g[s] = 0f;
            from[s] = -1;
            stamp[s] = search;
            closed[s] = false;
            open.Push(s, Vector3.Distance(nodes[s].pos, nodes[t].pos));
            int expansions = 0;
            while (open.Count > 0 && expansions < 6000)
            {
                int cur = open.Pop();
                if (stamp[cur] == search && closed[cur]) continue;
                closed[cur] = true;
                expansions++;
                if (cur == t) break;
                var cn = nodes[cur];
                foreach (int nb in cn.links)
                {
                    float cost = g[cur] + Vector3.Distance(cn.pos, nodes[nb].pos);
                    if (stamp[nb] != search)
                    {
                        stamp[nb] = search;
                        closed[nb] = false;
                        g[nb] = float.MaxValue;
                    }
                    if (closed[nb] || cost >= g[nb]) continue;
                    g[nb] = cost;
                    from[nb] = cur;
                    open.Push(nb, cost + Vector3.Distance(nodes[nb].pos, nodes[t].pos));
                }
            }
            if (stamp[t] != search || !closed[t]) return false;
            for (int c = t; c != -1 && c != s; c = from[c]) result.Add(nodes[c].pos);
            result.Reverse();
            result.Add(q);
            return true;
        }

        class MinHeap
        {
            readonly List<int> ids = new List<int>();
            readonly List<float> keys = new List<float>();
            public int Count { get { return ids.Count; } }

            public void Push(int id, float key)
            {
                ids.Add(id);
                keys.Add(key);
                int i = ids.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = ids[0];
                int last = ids.Count - 1;
                ids[0] = ids[last];
                keys[0] = keys[last];
                ids.RemoveAt(last);
                keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < ids.Count && keys[l] < keys[m]) m = l;
                    if (r < ids.Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int ti = ids[a]; ids[a] = ids[b]; ids[b] = ti;
                float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
            }
        }
    }
}
