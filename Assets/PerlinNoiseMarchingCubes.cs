using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoiseMarchingCubes : MonoBehaviour
{
    public PerlinSettings settings;
    private const int chunkSize = 32;

    private float[,] heightMap;
    private List<GameObject> chunks = new List<GameObject>();

    private void Start()
    {
        if (settings == null)
        {
            Debug.LogError("PerlinSettings reference is missing!");
            return;
        }

        GenerateMesh();
    }

    private void Update()
    {
        if (settings == null) return;

        GenerateMesh();
    }

    private void GenerateMesh()
    {
        // Clear existing chunks
        foreach (var chunk in chunks)
        {
            if (chunk != null)
                DestroyImmediate(chunk);
        }
        chunks.Clear();

        // Generate full height map
        heightMap = new float[settings.width, settings.height];
        for (int x = 0; x < settings.width; x++)
        {
            for (int y = 0; y < settings.height; y++)
            {
                heightMap[x, y] = CalculateHeight(x, y);
            }
        }

        // Calculate number of chunks needed
        int chunksX = Mathf.CeilToInt((float)settings.width / chunkSize);
        int chunksY = Mathf.CeilToInt((float)settings.height / chunkSize);

        // Create chunks
        for (int chunkX = 0; chunkX < chunksX; chunkX++)
        {
            for (int chunkY = 0; chunkY < chunksY; chunkY++)
            {
                CreateChunk(chunkX, chunkY);
            }
        }
    }

    private void CreateChunk(int chunkX, int chunkY)
    {
        GameObject chunkObj = new GameObject($"Chunk_{chunkX}_{chunkY}");
        chunkObj.transform.parent = this.transform;
        chunks.Add(chunkObj);

        MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));

        // Calculate start and end positions for this chunk
        int startX = chunkX * chunkSize;
        int startY = chunkY * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, settings.width);
        int endY = Mathf.Min(startY + chunkSize, settings.height);

        // Generate mesh for this chunk
        Mesh mesh = CreateChunkMesh(startX, startY, endX, endY);
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

    private Mesh CreateChunkMesh(int startX, int startY, int endX, int endY)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        int chunkWidth = endX - startX;
        int chunkHeight = endY - startY;

        // Create vertices for this chunk
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                float height = heightMap[x, y];
                Vector3 position = new Vector3(x - settings.width / 2, height, y - settings.height / 2);

                vertices.Add(position);
                colors.Add(CalculateColor(x, y));
            }
        }

        // Create triangles for this chunk
        for (int x = 0; x < chunkWidth - 1; x++)
        {
            for (int y = 0; y < chunkHeight - 1; y++)
            {
                int topLeft = x * chunkHeight + y;
                int topRight = (x + 1) * chunkHeight + y;
                int bottomLeft = x * chunkHeight + (y + 1);
                int bottomRight = (x + 1) * chunkHeight + (y + 1);

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
