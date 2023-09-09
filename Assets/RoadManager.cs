using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathCreation;
using UnityEngine.UI;
using TMPro;

public struct EarPoint
{
    public Vector2 point;
    public float angle;
}

public class RoadManager : MonoBehaviour
{
    [SerializeField]
    Transform parent;
    [SerializeField]
    GameObject textPrefab;
    [Header("Road settings")]
    public float roadWidth = .4f;
    [Range(0, .5f)]
    public float thickness = .15f;
    public bool flattenSurface;

    [Header("Material settings")]
    public Material roadMaterial;
    public Material undersideMaterial;
    public float textureTiling = 1;

    [SerializeField, HideInInspector]
    GameObject meshHolder;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;

    VertexPath GeneratePath(Vector3[] points)
    {
        BezierPath bezierPath = new BezierPath(points, false, PathSpace.xyz);
        return new VertexPath(bezierPath, parent, 15, 1f);
    }

    float GetAngle(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        var a = new Vector2(p2.x - p1.x, p2.y - p1.y);
        var b = new Vector2(p2.x - p3.x, p2.y - p3.y);

        // arc cos of dot product of a and b divided by the product of their magnitudes
        var angle = Mathf.Acos((a.x * b.x + a.y * b.y) / (Mathf.Sqrt(a.x * a.x + a.y * a.y) * Mathf.Sqrt(b.x * b.x + b.y * b.y))) * Mathf.Rad2Deg;

        return angle;
    }

    // Start is called before the first frame update
    void Start()
    {
        AssignMeshComponents();
        AssignMaterials();
        var path = GeneratePath(new Vector3[]{
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 20),
            new Vector3(100, 0, 33),
            new Vector3(-20, 0, 66),
            new Vector3(0, 0, 80),
            new Vector3(0, 0, 90),
            new Vector3(0, 0, 100),
        });

        Vector3[] verts = new Vector3[path.NumPoints * 8];
        Vector2[] uvs = new Vector2[verts.Length];
        Vector3[] normals = new Vector3[verts.Length];

        int numTris = 2 * (path.NumPoints - 1) + ((path.isClosedLoop) ? 2 : 0);
        int[] roadTriangles = new int[numTris * 3];
        int[] underRoadTriangles = new int[numTris * 3];
        int[] sideOfRoadTriangles = new int[numTris * 2 * 3];

        int vertIndex = 0;
        int triIndex = 0;

        // Vertices for the top of the road are layed out:
        // 0  1
        // 8  9
        // and so on... So the triangle map 0,8,1 for example, defines a triangle from top left to bottom left to bottom right.
        int[] triangleMap = { 0, 8, 1, 1, 8, 9 };
        int[] sidesTriangleMap = { 4, 6, 14, 12, 4, 14, 5, 15, 7, 13, 15, 5 };

        bool usePathNormals = !(path.space == PathSpace.xyz && flattenSurface);

        for (int i = 0; i < path.NumPoints; i++)
        {
            Vector3 localUp = (usePathNormals) ? Vector3.Cross(path.GetTangent(i), path.GetNormal(i)) : path.up;
            Vector3 localRight = (usePathNormals) ? path.GetNormal(i) : Vector3.Cross(localUp, path.GetTangent(i));

            // Find position to left and right of current path vertex
            Vector3 vertSideA = path.GetPoint(i) - localRight * Mathf.Abs(roadWidth);
            Vector3 vertSideB = path.GetPoint(i) + localRight * Mathf.Abs(roadWidth);

            // Add top of road vertices
            verts[vertIndex + 0] = vertSideA;
            verts[vertIndex + 1] = vertSideB;
            // Add bottom of road vertices
            verts[vertIndex + 2] = vertSideA - localUp * thickness;
            verts[vertIndex + 3] = vertSideB - localUp * thickness;

            // Duplicate vertices to get flat shading for sides of road
            verts[vertIndex + 4] = verts[vertIndex + 0];
            verts[vertIndex + 5] = verts[vertIndex + 1];
            verts[vertIndex + 6] = verts[vertIndex + 2];
            verts[vertIndex + 7] = verts[vertIndex + 3];

            // Set uv on y axis to path time (0 at start of path, up to 1 at end of path)
            uvs[vertIndex + 0] = new Vector2(0, path.times[i]);
            uvs[vertIndex + 1] = new Vector2(1, path.times[i]);

            // Top of road normals
            normals[vertIndex + 0] = localUp;
            normals[vertIndex + 1] = localUp;
            // Bottom of road normals
            normals[vertIndex + 2] = -localUp;
            normals[vertIndex + 3] = -localUp;
            // Sides of road normals
            normals[vertIndex + 4] = -localRight;
            normals[vertIndex + 5] = localRight;
            normals[vertIndex + 6] = -localRight;
            normals[vertIndex + 7] = localRight;

            // Set triangle indices
            if (i < path.NumPoints - 1 || path.isClosedLoop)
            {
                for (int j = 0; j < triangleMap.Length; j++)
                {
                    roadTriangles[triIndex + j] = (vertIndex + triangleMap[j]) % verts.Length;
                    // reverse triangle map for under road so that triangles wind the other way and are visible from underneath
                    underRoadTriangles[triIndex + j] = (vertIndex + triangleMap[triangleMap.Length - 1 - j] + 2) % verts.Length;
                }
                for (int j = 0; j < sidesTriangleMap.Length; j++)
                {
                    sideOfRoadTriangles[triIndex * 2 + j] = (vertIndex + sidesTriangleMap[j]) % verts.Length;
                }

            }

            vertIndex += 8;
            triIndex += 6;
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.subMeshCount = 3;
        mesh.SetTriangles(roadTriangles, 0);
        mesh.SetTriangles(underRoadTriangles, 1);
        mesh.SetTriangles(sideOfRoadTriangles, 2);
        mesh.RecalculateBounds();

        //get every 8th vertice
        Vector2[] leftSide = new Vector2[path.NumPoints];
        Vector2[] rightSide = new Vector2[path.NumPoints];

        for (int i = 0; i < path.NumPoints; i++)
        {
            leftSide[i] = new Vector2(verts[i * 8].x, verts[i * 8].z);
            rightSide[i] = new Vector2(verts[i * 8 + 1].x, verts[i * 8 + 1].z);
        }

        // for (int i = 0; i < path.NumPoints; i++) {
        //     Debug.DrawLine (leftSide[i], rightSide[i], Color.red, 100f);
        // }

        // bounds array of vector 2
        var leftPolygon = new Vector2[path.NumPoints + 2];
        leftPolygon[0] = new Vector2(-100, 100);
        leftPolygon[1] = new Vector2(-100, 0);
        for (int i = 0; i < path.NumPoints; i++)
        {
            leftPolygon[i + 2] = leftSide[i];
        }

        //draw left polygon
        for (int i = 0; i < leftPolygon.Length; i++)
        {
            Debug.DrawLine(
                new Vector3(leftPolygon[i].x, 0, leftPolygon[i].y),
                new Vector3(leftPolygon[(i + 1) % leftPolygon.Length].x, 0, leftPolygon[(i + 1) % leftPolygon.Length].y),
                Color.green,
                100f
            );
        }


        //copy into list
        var leftPolygonList = new List<EarPoint>();
        // calculate angles
        for (int i = 0; i < leftPolygon.Length; i++)
        {
            var prev = leftPolygon[(i + leftPolygon.Length - 1) % leftPolygon.Length];
            var curr = leftPolygon[i];
            var next = leftPolygon[(i + 1) % leftPolygon.Length];
            var angle = GetAngle(prev, curr, next);
            leftPolygonList.Add(new EarPoint { point = curr, angle = angle });
                Debug.Log("Angle: " + angle);

            if (angle > 120){
                //create primitive circle
                var circle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                circle.transform.position = new Vector3(curr.x, angle * 0.01f, curr.y);
                circle.transform.localScale = new Vector3(1, 1f, 1f);
                Debug.Log("Angle: " + angle);
            }
        }

        var steps = 15;
        var index = 0;

        // while (steps > 0 && leftPolygonList.Count > 3)
        // {
        //     steps--;
        //     //get 3 points with angles < 180
        //     var prev = leftPolygonList[(index + leftPolygonList.Count - 1) % leftPolygonList.Count];
        //     var curr = leftPolygonList[index];
        //     var next = leftPolygonList[(index + 1) % leftPolygonList.Count];

        //     if (prev.angle < 180 && curr.angle < 180 && next.angle < 180) {
        //         //draw line
        //         Debug.DrawLine(
        //             new Vector3(prev.point.x, 0, prev.point.y),
        //             new Vector3(next.point.x, 0, next.point.y),
        //             Color.red,
        //             100f
        //         );
        //         //remove point
        //         leftPolygonList.RemoveAt(index);
        //         //fix index
        //         index = (index + leftPolygonList.Count - 1) % leftPolygonList.Count;
        //         //recalculate angles for prev and next
        //         var prevPrev = leftPolygonList[(index + leftPolygonList.Count - 2) % leftPolygonList.Count];
        //         var nextNext = leftPolygonList[(index + 2) % leftPolygonList.Count];
        //         prev.angle = GetAngle(prevPrev.point, prev.point, next.point);
        //         next.angle = GetAngle(prev.point, next.point, nextNext.point);
        //     } else {
        //         index++;
        //     }
        // }

    }

    // Update is called once per frame
    void Update()
    {

    }

    // Add MeshRenderer and MeshFilter components to this gameobject if not already attached
    void AssignMeshComponents()
    {

        if (meshHolder == null)
        {
            meshHolder = new GameObject("Road Mesh Holder");
        }

        meshHolder.transform.rotation = Quaternion.identity;
        meshHolder.transform.position = Vector3.zero;
        meshHolder.transform.localScale = Vector3.one;

        // Ensure mesh renderer and filter components are assigned
        if (!meshHolder.gameObject.GetComponent<MeshFilter>())
        {
            meshHolder.gameObject.AddComponent<MeshFilter>();
        }
        if (!meshHolder.GetComponent<MeshRenderer>())
        {
            meshHolder.gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer = meshHolder.GetComponent<MeshRenderer>();
        meshFilter = meshHolder.GetComponent<MeshFilter>();
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        meshFilter.sharedMesh = mesh;
    }

    void AssignMaterials()
    {
        if (roadMaterial != null && undersideMaterial != null)
        {
            meshRenderer.sharedMaterials = new Material[] { roadMaterial, undersideMaterial, undersideMaterial };
            meshRenderer.sharedMaterials[0].mainTextureScale = new Vector3(1, textureTiling);
        }
    }




    void CreateLine(Transform container, string name, Vector3[] points, Color color, float width, int order = 1)
    {
        var lineGameObject = new GameObject(name);
        lineGameObject.transform.parent = container;
        var lineRenderer = lineGameObject.AddComponent<LineRenderer>();

        lineRenderer.SetPositions(points);

        lineRenderer.material = new Material(Shader.Find("Standard"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.sortingOrder = order;
    }
}
