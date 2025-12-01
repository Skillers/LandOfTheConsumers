using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoise : MonoBehaviour
{
    public PerlinSettings settings;
    private GameObject[,] quads;

    public GameObject quadPrefab;
    private void Start()
    {
        quads = new GameObject[settings.width, settings.height];
        SetupComponents();
    }

    private void SetupComponents()
    {
        for (int x = 0; x < settings.width; x++)
        {
            for (int y = 0; y < settings.height; y++)
            {
                GameObject temp = Instantiate(quadPrefab, new Vector3(x - settings.width/2, 0, y - settings.height/2), Quaternion.Euler(90, 0, 0), this.transform);
                quads[x, y] = temp;
            }
        }
    }

    private void Update()
    {
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
