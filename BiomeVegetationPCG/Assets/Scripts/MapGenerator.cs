using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    
    public enum DrawMode { NoiseMap, ColorMap, Mesh };
    public DrawMode drawMode;

    // Using 241 because, 240 can be divided by 2, 4, 6, 8, 10, 12
    public const int mapChunkSize = 241;
    [Range(0,6)]
    public int levelOfDetail;

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

    public void DrawMapInEditor() {
        MapData mapData = GenerateMapData();
        MapDisplay display = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        } else if (drawMode == DrawMode.ColorMap) {
            display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        } else if (drawMode == DrawMode.Mesh) {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, heightCurve, levelOfDetail), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
    }

    MapData GenerateMapData() {
        float[,] heightMap = Noise.GenerateNoiseMap(seed, mapChunkSize, mapChunkSize, noiseScale, octaves, persistance, lacunarity, offset);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];

        for (int y = 0; y < mapChunkSize; y++) {
            for (int x = 0; x < mapChunkSize; x++) {
                
                float currentHeight = heightMap[x, y];

                for (int i = 0; i < regions.Length; i++) {
                    if (currentHeight <= regions[i].height) {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                        break;
                    }
                }

            }
        }

        return new MapData(heightMap, colorMap);

    }

    void OnValidate() {
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


public struct MapData {
    public float[,] heightMap;
    //public float[,] moistureMap;
    public Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap) {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}