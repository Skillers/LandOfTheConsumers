using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoise : MonoBehaviour
{
    public PerlinSettings settings;
    private const int chunkSize = 32;
    private List<GameObject> chunks = new List<GameObject>();
    private GameObject[,] quads;

    public GameObject quadPrefab;

    private void Start()
    {
        if (settings == null)
        {
            Debug.LogError("PerlinSettings reference is missing!");
            return;
        }

        quads = new GameObject[settings.width, settings.height];
        SetupComponents();
    }

    private void SetupComponents()
    {
        // Clear existing chunks
        foreach (var chunk in chunks)
        {
            if (chunk != null)
                DestroyImmediate(chunk);
        }
        chunks.Clear();

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

        // Calculate start and end positions for this chunk
        int startX = chunkX * chunkSize;
        int startY = chunkY * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, settings.width);
        int endY = Mathf.Min(startY + chunkSize, settings.height);

        // Create quads for this chunk
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                GameObject temp = Instantiate(quadPrefab, new Vector3(x - settings.width / 2, 0, y - settings.height / 2), Quaternion.Euler(90, 0, 0), chunkObj.transform);
                quads[x, y] = temp;
                temp.GetComponent<Renderer>().material.color = CalculateColor(x, y);
            }
        }
    }

    private void Update()
    {
        if (settings == null) return;

        for (int x = 0; x < settings.width; x++)
        {
            for (int y = 0; y < settings.height; y++)
            {
                quads[x, y].GetComponent<Renderer>().material.color = CalculateColor(x, y);
                quads[x, y].transform.position = new Vector3(quads[x, y].transform.position.x, CalculateHeight(x, y), quads[x, y].transform.position.z);
            }
        }
    }

    private Vector2 GetCoords(int x, int y)
    {
        float xCoord = (float)x / settings.width * (settings.scale + settings.widthScale) + settings.offSetX / (settings.scale + settings.widthScale) / 2f;
        float yCoord = (float)y / settings.height * (settings.scale + settings.heightScale) + settings.offSetY / (settings.scale + settings.heightScale) / 2f;
        return new Vector2(xCoord, yCoord);
    }

    private float GetSample(int x, int y)
    {
        Vector2 Coords = GetCoords(x, y);

        float sample = Mathf.PerlinNoise(Coords.x, Coords.y);
        
        return sample;
    }
    
    private Color CalculateColor(int x, int y)
    {
        float sample = GetSample(x, y);
        return new Color(sample, sample, sample);
    }

    private float CalculateHeight(int x, int y)
    {
        float sample = GetSample(x, y);
        return sample * settings.heightMultiplier;
    }
}
