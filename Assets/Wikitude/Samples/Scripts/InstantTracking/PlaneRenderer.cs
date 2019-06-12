using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wikitude;

public class PlaneRenderer : MonoBehaviour {

    /* How thick the faded border should be. A positive value fades outside the existing bounds, while a negative value fades inside the bounds. */
    public float FadeDistance = 0.1f;
    public Material RenderPlaneMaterial;

    private Dictionary<long, GameObject> _renderPlanes = new Dictionary<long, GameObject>();

    public void OnPlaneRecognized(Wikitude.Plane recognizedPlane) {
        /* Whenever a new plane is recognized, create a new GameObject to display it. */
        var renderPlane = new GameObject("Plane");
        renderPlane.transform.SetParent(recognizedPlane.Drawable.transform);

        var mesh = new Mesh();
        renderPlane.AddComponent<MeshFilter>().mesh = mesh;
        renderPlane.AddComponent<MeshRenderer>().material = new Material(RenderPlaneMaterial);

        UpdateMesh(renderPlane, mesh, recognizedPlane);

        _renderPlanes.Add(recognizedPlane.ID, renderPlane);
    }

    public void OnPlaneTracked(Wikitude.Plane trackedPlane) {
        GameObject renderPlane;
        if (_renderPlanes.TryGetValue(trackedPlane.ID, out renderPlane)) {
            UpdateMesh(renderPlane, renderPlane.GetComponent<MeshFilter>().sharedMesh, trackedPlane);
        } else {
            Debug.LogError("Could not find tracked plane with ID: " + trackedPlane.ID);
        }
    }

    public void OnPlaneLost(Wikitude.Plane lostPlane) {
        GameObject renderPlane;
        if (_renderPlanes.TryGetValue(lostPlane.ID, out renderPlane)) {
            _renderPlanes.Remove(lostPlane.ID);
            Destroy(renderPlane);
        } else {
            Debug.LogError("Could not find lost plane with ID: " + lostPlane.ID);
        }
    }

    private void UpdateMesh(GameObject renderPlane, Mesh mesh, Wikitude.Plane plane) {
        /* Convert the raw float array into a list of vectors, making it easier to work with. */
        int vertexCount = plane.ConvexHull.Length / 2;
        List<Vector3> convexHull = new List<Vector3>(vertexCount);
        for (int i = 0; i < vertexCount; ++i) {
            convexHull.Add(new Vector3(plane.ConvexHull[i * 2], 0.0f, plane.ConvexHull[i * 2 + 1]));
        }

        var inset = ComputeInset(convexHull);

        /* The sign of the fadeDistance decides which polygon is outside, and which one is inside. */
        if (FadeDistance > 0) {
            UpdateMeshGeometry(mesh, convexHull, inset);
        } else {
            UpdateMeshGeometry(mesh, inset, convexHull);
        }

        /* Select different colors based on the plane type. */
        Color32 color = Color.white;
        switch (plane.PlaneType) {
            case PlaneType.HorizontalUpward:
            case PlaneType.HorizontalDownward: {
                color = new Color32(0, 0, 255, 102);
                break;
            }
            case PlaneType.Vertical: {
                color = new Color32(237, 33, 200, 102);
                break;
            }
            case PlaneType.Arbitrary: {
                color = new Color32(64, 237, 33, 102);
                break;
            }
        }

        renderPlane.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", color);
    }

    private void UpdateMeshGeometry(Mesh mesh, List<Vector3> insidePolygon, List<Vector3> outsidePolygon) {
        mesh.Clear();
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var uvs = new List<Vector2>();
        var colors = new List<Color32>();

        /* Add the points from the inside polygon. These points have full opacity. */
        for (int i = 0; i < insidePolygon.Count; ++i) {
            vertices.Add(insidePolygon[i]);
            uvs.Add(new Vector2(insidePolygon[i].x, insidePolygon[i].z));
            colors.Add(Color.white);
        }

        /* Add the points from the outside polygon. These points have zero opacity to provide the fade out effect. */
        for (int i = 0; i < outsidePolygon.Count; ++i) {
            vertices.Add(outsidePolygon[i]);
            uvs.Add(new Vector2(outsidePolygon[i].x, outsidePolygon[i].z));
            colors.Add(new Color(1.0f, 1.0f, 1.0f, 0.0f));
        }

        /* Add the indices for the convex hull, which is rendered as a triangle fan. */
        for (int i = 2; i < insidePolygon.Count; ++i) {
            indices.Add(0);
            indices.Add(i - 1);
            indices.Add(i);
        }

        /* Add the indices for the faded border, which is rendered as a triangle strip, using points from both the outside polygon, as well as from the inside polygon. */
        for (int i = 0; i < outsidePolygon.Count; ++i) {
            indices.Add(i + insidePolygon.Count);
            indices.Add((i + 1) % outsidePolygon.Count + insidePolygon.Count);
            indices.Add(i);

            indices.Add(i);
            indices.Add((i + 1) % outsidePolygon.Count + insidePolygon.Count);
            indices.Add((i + 1) % insidePolygon.Count);
        }

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
    }

    /* Computes an inset for the polygon described by convexHull.
    * A positive fadeDistance results in an inset that is outside the original convexHull.
    * A negative fadeDistance results in an inset that is inside the original convexHull.
    * It assumes that the points in the convexHull are arranged in a counter-clockwise fashion.
    *
    * How it works is that for each point, it takes each connecting line adjacent to it and translates them perpendicularly by fadeDistance.
    * The new point is found at the intersection of these lines.
    *
    *           intersection
    *              point
    *                |                              +------- prevOffsetDir
    *                |                              |
    *                v                              v
    *                * - - - - -------------------------
    *               .                    ^
    *              .                     |
    *             .                positive inset
    *            .                    distance
    *           .                        |
    *          .                         v
    *         .               *-------------------------
    *        /               / ^                    ^
    *       /               /  |                    |
    *      /               /   original             +------- prevDir
    *     /               /    point (currPoint)
    *    /               /
    *   /               /
    *  /               / <---- nextDir
    * /               /
    *
    */
    private List<Vector3> ComputeInset(List<Vector3> convexHull) {
        var inset = new List<Vector3>();

        for (int i = 0; i < convexHull.Count; ++i) {

            /* For each point, we need to also consider the points adjacent to it. */
            var prevPoint = convexHull[(convexHull.Count + i - 1) % convexHull.Count];
            var currPoint = convexHull[i];
            var nextPoint = convexHull[(i + 1) % convexHull.Count];

            /* Compute the vectors describing the lines between the current point and the adjacent points. */
            var prevDir = currPoint - prevPoint;
            var nextDir = nextPoint - currPoint;

            /* Compute the perpendicular vectors to the lines computed above.
            * This assumes the points are arranged in a counter-clockwise fashion. If that is not the case, the sign needs to be inverted!
            */
            var prevPerp = new Vector3(-prevDir.z, 0.0f, prevDir.x).normalized;
            var nextPerp = new Vector3(-nextDir.z, 0.0f, nextDir.x).normalized;

            /* Move each point in the perpendicular direction we computed before and
            * connect them. This creates two new lines (*OffsetDir) which correspond to the original
            * lines computed above, but offset by fadeDistance.
            */
            var prevOffsetStart = prevPoint + prevPerp * FadeDistance;
            var prevOffsetEnd = currPoint + prevPerp * FadeDistance;

            var nextOffsetStart = currPoint + nextPerp * FadeDistance;
            var nextOffsetEnd = nextPoint + nextPerp * FadeDistance;

            var prevOffsetDir = prevOffsetEnd - prevOffsetStart;
            var nextOffsetDir = nextOffsetEnd - nextOffsetStart;

            /* Compute the intersection point.
            * When using the cross product, only the y component is considered. Because all points are on the XZ plane,
            * the cross product will either point directly up or directly down.
            */
            Vector3 intersection;
            float cross = Vector3.Cross(prevOffsetDir, nextOffsetDir).y;
            if (Mathf.Approximately(cross, 0.0f)) {
                /* This means that the lines are parallel to each other, so the intersection point is just one of the points
                * on the offset line. */
                intersection = prevOffsetEnd;
            } else {
                float a = Vector3.Cross(nextOffsetEnd - prevOffsetStart, nextOffsetDir).y / cross;
                intersection = prevOffsetStart + a * prevOffsetDir;
            }
            inset.Add(intersection);
        }

        return inset;
    }
}
