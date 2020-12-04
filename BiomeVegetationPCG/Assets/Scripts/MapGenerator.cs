using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    
    public enum DrawMode { NoiseMap, ColorMap, Mesh };
    public DrawMode drawMode;

    public int mapWidth;
    public int mapHeight;
    public float noiseScale;


    [Tooltip("More octaves leads to more frequencies added resulting in a more precisely-defined terrain.")]
    public int octaves;

    [Tooltip("Affect how rapidly the amplitude decrease for each octave. Each octave modifies the terrain slighter than the previous one.")]
    [Range(0,1)]
    public float persistance;

    [Tooltip("Control the increase in detail. More lacunarity results in more details at each octave (as a power math function).")]
    public float lacunarity;


    public int seed;
    public Vector2 offset;

    public float meshHeightMultiplier;

    [Tooltip("Can be used to flatten the result only (not the noise generator). Straight curve = no changes to the terrain. Can make water flat or mountains very 'peaky'.")]
    public AnimationCurve heightCurve;


    public bool autoUpdate;

    public TerrainType[] regions;

    public void GenerateMap() {
        float[,] heightMap = Noise.GenerateNoiseMap(seed, mapWidth, mapHeight, noiseScale, octaves, persistance, lacunarity, offset);

        Color[] colorMap = new Color[mapWidth * mapHeight];

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                
                float currentHeight = heightMap[x, y];

                for (int i = 0; i < regions.Length; i++) {
                    if (currentHeight <= regions[i].height) {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }

            }
        }

        MapDisplay display = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(heightMap));
        } 
        else if (drawMode == DrawMode.ColorMap) {
            display.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
        } 
        else if (drawMode == DrawMode.Mesh) {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(heightMap, meshHeightMultiplier, heightCurve), TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
        }
    }

    void OnValidate() {
        if (mapWidth < 1) {
            mapWidth = 1;
        }
        if (mapHeight < 1) {
            mapHeight = 1;
        }
        if (lacunarity < 1) {
            lacunarity = 1;
        }
        if (octaves < 0) {
            octaves = 0;
        }
    }
}

[System.Serializable]
public struct TerrainType {
    public string name;
    public float height;
    public Color color;
}
