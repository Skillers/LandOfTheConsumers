using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoiseMarchingCubes : MonoBehaviour
{
    public PerlinSettings settings;

    private float[,] heightMap;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void Start()
    {
        if (settings == null)
        {
            Debug.LogError("PerlinSettings reference is missing!");
            return;
        }

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));

        GenerateMesh();
    }

    private void Update()
    {
        if (settings == null) return;

        GenerateMesh();
    }

    private void GenerateMesh()
    {
        heightMap = new float[settings.width, settings.height];

        // Generate 2D height map using Perlin noise
        for (int x = 0; x < settings.width; x++)
        {
            for (int y = 0; y < settings.height; y++)
            {
                heightMap[x, y] = CalculateHeight(x, y);
            }
        }

        // Generate mesh from height map
        Mesh mesh = CreateTerrainMesh();
        meshFilter.mesh = mesh;
    }

    private Color CalculateColor(int x, int y)
    {
        float xCoord = (float)x / settings.width * settings.scale * settings.widthScale + settings.offSetX / (settings.scale * settings.widthScale) / 2f;
        float yCoord = (float)y / settings.height * settings.scale * settings.heightScale + settings.offSetY / (settings.scale * settings.heightScale) / 2f;

        float sample = Mathf.PerlinNoise(xCoord, yCoord);
        return new Color(sample, sample, sample);
    }

    private float CalculateHeight(int x, int y)
    {
        float xCoord = (float)x / settings.width * settings.scale * settings.widthScale + settings.offSetX / (settings.scale * settings.widthScale) / 2f;
        float yCoord = (float)y / settings.height * settings.scale * settings.heightScale + settings.offSetY / (settings.scale * settings.heightScale) / 2f;

        float sample = Mathf.PerlinNoise(xCoord, yCoord);
        return sample * settings.heightMultiplier;
    }

    private Mesh CreateTerrainMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        // Create vertices
        for (int x = 0; x < settings.width; x++)
        {
            for (int y = 0; y < settings.height; y++)
            {
                float height = heightMap[x, y];
                Vector3 position = new Vector3(x - settings.width / 2, height, y - settings.height / 2);

                vertices.Add(position);
                colors.Add(CalculateColor(x, y));
            }
        }

        // Create triangles
        for (int x = 0; x < settings.width - 1; x++)
        {
            for (int y = 0; y < settings.height - 1; y++)
            {
                int topLeft = x * settings.height + y;
                int topRight = (x + 1) * settings.height + y;
                int bottomLeft = x * settings.height + (y + 1);
                int bottomRight = (x + 1) * settings.height + (y + 1);

                // First triangle
                triangles.Add(topLeft);
                triangles.Add(bottomLeft);
                triangles.Add(topRight);

                // Second triangle
                triangles.Add(topRight);
                triangles.Add(bottomLeft);
                triangles.Add(bottomRight);
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }
}
