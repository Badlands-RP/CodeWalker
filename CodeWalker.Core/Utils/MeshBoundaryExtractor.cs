using SharpDX;
using System.Collections.Generic;
using CodeWalker.GameFiles;

namespace CodeWalker
{
    /// <summary>
    /// Utility class to extract boundary edges from mesh geometries
    /// </summary>
    public class MeshBoundaryExtractor
    {
        private struct Edge
        {
            public int V1;
            public int V2;

            public Edge(int v1, int v2)
            {
                // Normalize edge so V1 < V2 for consistent comparison
                if (v1 < v2)
                {
                    V1 = v1;
                    V2 = v2;
                }
                else
                {
                    V1 = v2;
                    V2 = v1;
                }
            }

            public override int GetHashCode()
            {
                return V1.GetHashCode() ^ (V2.GetHashCode() << 16);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Edge)) return false;
                Edge other = (Edge)obj;
                return V1 == other.V1 && V2 == other.V2;
            }
        }

        /// <summary>
        /// Extract boundary edges from a drawable geometry
        /// Boundary edges are edges that belong to only one triangle
        /// </summary>
        public static List<Vector3> ExtractBoundaryEdges(DrawableGeometry geom, Vector3 scale)
        {
            var result = new List<Vector3>();

            if (geom == null) return result;
            if (geom.VertexData == null) return result;
            if (geom.IndexBuffer == null) return result;
            if (geom.IndexBuffer.Indices == null) return result;

            var vdata = geom.VertexData;
            var indices = geom.IndexBuffer.Indices;

            if (vdata.VertexBytes == null) return result;
            if (vdata.Info == null) return result;

            int triCount = indices.Length / 3;

            // For very large meshes, use a simplified approach to avoid performance issues
            if (triCount > 20000)
            {
                // For very large meshes, just sample some edges instead of computing all boundaries
                // This is much faster and still gives a reasonable visual representation
                int sampleRate = triCount / 5000; // Sample approximately 5000 triangles
                for (int i = 0; i < triCount; i += sampleRate)
                {
                    int i0 = indices[i * 3 + 0];
                    int i1 = indices[i * 3 + 1];
                    int i2 = indices[i * 3 + 2];

                    Vector3 v0 = vdata.GetVector3(i0, 0) * scale;
                    Vector3 v1 = vdata.GetVector3(i1, 0) * scale;
                    Vector3 v2 = vdata.GetVector3(i2, 0) * scale;

                    // Add all three edges of sampled triangles
                    result.Add(v0);
                    result.Add(v1);
                    result.Add(v1);
                    result.Add(v2);
                    result.Add(v2);
                    result.Add(v0);
                }
                return result;
            }

            // Count edge occurrences
            var edgeCount = new Dictionary<Edge, int>(triCount * 3);

            for (int i = 0; i < triCount; i++)
            {
                int i0 = indices[i * 3 + 0];
                int i1 = indices[i * 3 + 1];
                int i2 = indices[i * 3 + 2];

                // Add three edges of the triangle
                IncrementEdge(edgeCount, new Edge(i0, i1));
                IncrementEdge(edgeCount, new Edge(i1, i2));
                IncrementEdge(edgeCount, new Edge(i2, i0));
            }

            // Extract boundary edges (edges with count == 1)
            foreach (var kvp in edgeCount)
            {
                if (kvp.Value == 1) // Boundary edge
                {
                    var edge = kvp.Key;
                    Vector3 v1 = vdata.GetVector3(edge.V1, 0) * scale;
                    Vector3 v2 = vdata.GetVector3(edge.V2, 0) * scale;

                    result.Add(v1);
                    result.Add(v2);
                }
            }

            return result;
        }

        /// <summary>
        /// Extract boundary edges from multiple geometries and combine them
        /// </summary>
        public static List<Vector3> ExtractBoundaryEdges(DrawableGeometry[] geometries, Vector3 scale)
        {
            var result = new List<Vector3>();

            if (geometries == null) return result;

            foreach (var geom in geometries)
            {
                var edges = ExtractBoundaryEdges(geom, scale);
                result.AddRange(edges);
            }

            return result;
        }

        private static void IncrementEdge(Dictionary<Edge, int> edgeCount, Edge edge)
        {
            if (edgeCount.ContainsKey(edge))
            {
                edgeCount[edge]++;
            }
            else
            {
                edgeCount[edge] = 1;
            }
        }
    }
}

