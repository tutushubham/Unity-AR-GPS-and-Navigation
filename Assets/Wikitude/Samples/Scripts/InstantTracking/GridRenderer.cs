using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    public static Color DefaultTargetColor = new Color(1.0f, 0.525f, 0, 1.0f);
    private static Color GridColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    private static Color MainLineColor = GridColor * 0.9f;
    private static Color UnitLineColor = GridColor * 0.75f;

    public Color TargetColor = DefaultTargetColor;

    private const int NumberOfLinesOnEitherSide = 50;
    private const float LineSpacing = 0.25f;
    private const int IntervalBetweenMainLines = 10;
    private const float TargetSize = 8.0f;

    private Material _lineMaterial;
    /* Array containing the bounding planes of the main camera frustum. Used to clip the grid lines. */
    private Plane[] _cameraPlanes;

    private void Start() {
        InitializeLineMaterial();
    }

    private void OnRenderObject() {
        /* Because the Wikitude SDK uses a secondary camera to render the camera frame, we need to make sure to render the grid only on the main camera. */
        if (Camera.current == Camera.main) {
            RenderGrid();
        }
    }

    /* Initializes a material that has the appropriate blending and culling options required to properly render the grid lines. */
    private void InitializeLineMaterial() {
        var shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader);
        _lineMaterial.hideFlags = HideFlags.HideAndDontSave;

        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);
    }

    private void RenderGrid() {
        /* Set the material. */
        _lineMaterial.SetPass(0);
        /* Update the planes required for clipping. */
        _cameraPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        GL.Begin(GL.LINES);

        RenderGrill(Vector3.forward, Vector3.right);
        RenderGrill(Vector3.right, Vector3.forward);

        RenderLine(Vector3.forward,  Vector3.zero, TargetColor, TargetSize);
        RenderLine(Vector3.right,  Vector3.zero, TargetColor, TargetSize);

        GL.End();
    }

    /* Renders a sequence of parallel lines. Each line is rendered along the mainAxis, and the lines are spaced along the perpendicular vector, in both directions.
     * Lines get progressively more transparent as they get farther away from the mainAxis.
     */
    private void RenderGrill(Vector3 mainAxis, Vector3 perpendicular) {
        for (int i = -NumberOfLinesOnEitherSide; i <= NumberOfLinesOnEitherSide; ++i) {
            var color = UnitLineColor;
            if (i == 0) {
                color = GridColor;
            } else if (i % IntervalBetweenMainLines == 0) {
                color = MainLineColor;
            }
            float intensity = 1.0f - Mathf.Abs(i * 2.0f) / NumberOfLinesOnEitherSide;
            color.a *= intensity;
            if (color.a > 0.01f) {
                RenderLine(mainAxis, perpendicular * i * LineSpacing, color, (float)NumberOfLinesOnEitherSide / 8.0f);
            }
        }
    }

    /* Renders a line with a certain length along an axis centered in the origin.
     * An offset can be added to move where the line is rendered.
     * The color is fully transparent at the origin and gets progressively more transparent as it gets closer to the origin.
     */
    private void RenderLine(Vector3 axis, Vector3 offset, Color color, float length) {
        RenderLine(GetTransparent(color), axis * (-length) + offset, color, offset);
        RenderLine(color, offset, GetTransparent(color), axis * length + offset);
    }

    /* Renders a line from startPosition to endPosition, using startColor and endColor.
     * Internally clips the lines to the camera frustum.
     */
    private void RenderLine(Color startColor, Vector3 startPosition, Color endColor, Vector3 endPosition) {
        Color clippedEndColor = Color.Lerp(startColor, endColor, Clip(startPosition, ref endPosition));
        Color clippedStartColor = Color.Lerp(startColor, endColor, 1.0f - Clip(endPosition, ref startPosition));

        GL.Color(clippedStartColor);
        GL.Vertex(startPosition);
        GL.Color(clippedEndColor);
        GL.Vertex(endPosition);
    }

    /* Method that manually clips the vertices to the camera planes because on some Android devices clipping of GL_LINES doesn't work correctly.
     * The ref end parameter is modified to reflect the clipped position.
     * Returns the ratio between the new distance between start and end and the old one (how much the line was clipped).
     */
    private float Clip(Vector3 start, ref Vector3 end) {
        Vector3 v = end - start;
        float magnitude = v.magnitude;
        Vector3 vNorm = v / magnitude;

        var startRay = new Ray(start, vNorm);
        float minDistance = magnitude;

        foreach (var plane in _cameraPlanes) {
            if (Vector3.Dot(vNorm, plane.normal) < 0) {
                float enter;
                if (plane.Raycast(startRay, out enter)) {
                    if (enter < minDistance) {
                        minDistance = enter;
                    }
                }
            }
        }

        end = startRay.GetPoint(minDistance);
        return minDistance / magnitude;
    }

    /* Returns the fully transparent version of a color. */
    private Color GetTransparent(Color color) {
        color.a = 0.0f;
        return color;
    }
}
