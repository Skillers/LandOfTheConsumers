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
    public float chunkSpawnDelay = 0.1f;
    private bool isGenerating = false;
    private bool initialGenerationComplete = false;

    // Track previous settings to detect changes
    private int prevWidth;
    private int prevHeight;
    private int prevOffSetX;
    private int prevOffSetY;
    private float prevHeightMultiplier;
    private float prevScale;
    private float prevXScale;
    private float prevZScale;

    private void Start()
    {
        if (settings == null)
        {
            Debug.LogError("PerlinSettings reference is missing!");
            return;
        }

        CacheSettings();
        StartCoroutine(GenerateInitialMesh());
    }

    private void Update()
    {
        if (settings == null || isGenerating || !initialGenerationComplete) return;

        // Only regenerate if settings have changed
        if (HasSettingsChanged())
        {
            CacheSettings();
            UpdateAllChunks();
        }
    }

    private void CacheSettings()
    {
        prevWidth = settings.width;
        prevHeight = settings.height;
        prevOffSetX = settings.offSetX;
        prevOffSetY = settings.offSetY;
        prevHeightMultiplier = settings.heightMultiplier;
        prevScale = settings.scale;
        prevXScale = settings.xScale;
        prevZScale = settings.zScale;
    }

    private bool HasSettingsChanged()
    {
        return prevWidth != settings.width ||
               prevHeight != settings.height ||
               prevOffSetX != settings.offSetX ||
               prevOffSetY != settings.offSetY ||
               prevHeightMultiplier != settings.heightMultiplier ||
               prevScale != settings.scale ||
               prevXScale != settings.xScale ||
               prevZScale != settings.zScale;
    }

    private IEnumerator GenerateInitialMesh()
    {
        isGenerating = true;

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

        // Create chunks one at a time with delay to prevent lag
        for (int chunkX = 0; chunkX < chunksX; chunkX++)
        {
            for (int chunkY = 0; chunkY < chunksY; chunkY++)
            {
                CreateChunk(chunkX, chunkY);
                yield return new WaitForSeconds(chunkSpawnDelay);
            }
        }

        isGenerating = false;
        initialGenerationComplete = true;
    }

    private void UpdateAllChunks()
    {
        // Regenerate height map with new settings
        heightMap = new float[settings.width, settings.height];
        for (int x = 0; x < settings.width; x++)
        {
            for (int y = 0; y < settings.height; y++)
            {
                heightMap[x, y] = CalculateHeight(x, y);
            }
        }

        // Update all existing chunks immediately
        int chunkIndex = 0;
        int chunksX = Mathf.CeilToInt((float)settings.width / chunkSize);
        int chunksY = Mathf.CeilToInt((float)settings.height / chunkSize);

        for (int chunkX = 0; chunkX < chunksX; chunkX++)
        {
            for (int chunkY = 0; chunkY < chunksY; chunkY++)
            {
                if (chunkIndex < chunks.Count && chunks[chunkIndex] != null)
                {
                    // Calculate start and end positions for this chunk
                    int startX = chunkX * chunkSize;
                    int startY = chunkY * chunkSize;
                    int endX = Mathf.Min(startX + chunkSize + 1, settings.width);
                    int endY = Mathf.Min(startY + chunkSize + 1, settings.height);

                    // Update chunk position at its corner coordinates
                    Vector3 chunkPosition = new Vector3(startX, 0, startY);
                    chunks[chunkIndex].transform.position = chunkPosition;

                    // Update the mesh for this chunk
                    MeshFilter meshFilter = chunks[chunkIndex].GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        meshFilter.mesh = CreateChunkMesh(startX, startY, endX, endY);
                    }
                }
                chunkIndex++;
            }
        }
    }

    private void CreateChunk(int chunkX, int chunkY)
    {
        GameObject chunkObj = new GameObject($"Chunk_{chunkX}_{chunkY}");
        chunkObj.transform.parent = this.transform;
        chunks.Add(chunkObj);

        // Position the chunk at its corner coordinates
        int startX = chunkX * chunkSize;
        int startY = chunkY * chunkSize;
        Vector3 chunkPosition = new Vector3(startX, 0, startY);
        chunkObj.transform.position = chunkPosition;

        MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Standard"));

        // Calculate end positions - include one extra vertex to connect with next chunk
        int endX = Mathf.Min(startX + chunkSize + 1, settings.width);
        int endY = Mathf.Min(startY + chunkSize + 1, settings.height);

        // Generate mesh for this chunk
        Mesh mesh = CreateChunkMesh(startX, startY, endX, endY);
        meshFilter.mesh = mesh;
    }

    private Color CalculateColor(int x, int y)
    {
        float xCoord = (float)x / settings.width * settings.scale * settings.xScale + settings.offSetX / (settings.scale * settings.xScale) / 2f;
        float yCoord = (float)y / settings.height * settings.scale * settings.zScale + settings.offSetY / (settings.scale * settings.zScale) / 2f;

        float sample = Mathf.PerlinNoise(xCoord, yCoord);
        return new Color(sample, sample, sample);
    }

    private float CalculateHeight(int x, int y)
    {
        float xCoord = (float)x / settings.width * settings.scale * settings.xScale + settings.offSetX / (settings.scale * settings.xScale) / 2f;
        float yCoord = (float)y / settings.height * settings.scale * settings.zScale + settings.offSetY / (settings.scale * settings.zScale) / 2f;

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

        // Create vertices for this chunk in local space
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                float height = heightMap[x, y];
                // Local position relative to chunk (chunk is already positioned in world space)
                Vector3 localPosition = new Vector3(x - startX, height, y - startY);

                vertices.Add(localPosition);
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
