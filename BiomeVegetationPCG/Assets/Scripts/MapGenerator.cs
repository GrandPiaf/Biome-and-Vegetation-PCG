using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    
    public enum DrawMode { HeightMap, MoistureMap, BiomeColorMap, Mesh, Vegetation};
    public DrawMode drawMode;

    public Noise.NormalizeMode normalizeMode;

    // Using 241 because, 240 can be divided by 2, 4, 6, 8, 10, 12
    public const int mapChunkSize = 241;
    [Range(0,6)]
    public int editorPreviewLOD;

    public float noiseScale;


    [Tooltip("More octaves leads to more frequencies added resulting in a more precisely-defined terrain.")]
    public int octaves;

    [Tooltip("Affect how rapidly the amplitude decrease for each octave. Each octave modifies the terrain slighter than the previous one.")]
    [Range(0,1)]
    public float persistance;

    [Tooltip("Control the increase in detail. More lacunarity results in more details at each octave (as a power math function).")]
    public float lacunarity;


    public int seedHeight;
    public int seedMoisture;
    public int seedVegetation;
    public UnityEngine.Vector2 offset;

    public float meshHeightMultiplier;

    [Tooltip("Can be used to flatten the result only (not the noise generator). Straight curve = no changes to the terrain. Can make water flat or mountains very 'peaky'.")]
    public AnimationCurve meshHeightCurve;


    public bool autoUpdate;

    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    public void DrawMapInEditor() {
        MapData mapData = GenerateMapData(UnityEngine.Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.HeightMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.MoistureMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.moistureMap));
        }
        else if (drawMode == DrawMode.BiomeColorMap) {
            display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.biomeMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Mesh) {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, editorPreviewLOD), TextureGenerator.TextureFromColorMap(mapData.biomeMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Vegetation) {
            display.DrawTexture(TextureGenerator.TextureFromVegetationList(mapData.poissonDiskSamples, mapData.heightMap.GetLength(0), mapData.heightMap.GetLength(1)));
        }
    }

    public void RequestMapData(UnityEngine.Vector2 center, Action<MapData> callback) {
        ThreadStart threeadStart = delegate {
            MapDataThread(center, callback);
        };
        new Thread(threeadStart).Start();
    }

    void MapDataThread(UnityEngine.Vector2 center, Action<MapData> callback) {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue) {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback) {
        ThreadStart threadStart = delegate {
            MeshDataThread(mapData, lod, callback);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback) {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod);
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void Update() {
        if (mapDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if (meshDataThreadInfoQueue.Count > 0) {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }


    MapData GenerateMapData(UnityEngine.Vector2 center) {

        float[,] heightMap = Noise.GenerateNoiseMap(seedHeight, mapChunkSize, mapChunkSize, noiseScale, octaves, persistance, lacunarity, center + offset, normalizeMode);

        float[,] moistureMap = Noise.GenerateNoiseMap(seedMoisture, mapChunkSize, mapChunkSize, noiseScale, octaves, persistance, lacunarity, center + offset, normalizeMode);

        List<Vector2> poissonDiskSamples = Noise.GeneratePoissonDiskSampling(seedVegetation, mapChunkSize, mapChunkSize);

        Color[] biomeMap = new Color[mapChunkSize * mapChunkSize];


        // Getting colorMap for each 'pixel' in the texture
        // Depending on heightMap and moistureMap
        // Resulting in a biomeMap

        for (int y = 0; y < mapChunkSize; y++) {
            for (int x = 0; x < mapChunkSize; x++) {
                
                float currentHeight = heightMap[x, y];
                float currentMoisture = moistureMap[x, y];

                for (int i = 0; i < regions.Length; i++) {
                    if (currentHeight >= regions[i].height && currentMoisture >= regions[i].moisture) {
                        biomeMap[y * mapChunkSize + x] = regions[i].color;
                    }
                }

            }
        }

        return new MapData(heightMap, moistureMap, biomeMap, poissonDiskSamples);

    }

    void OnValidate() {
        if (lacunarity < 1) {
            lacunarity = 1;
        }
        if (octaves < 0) {
            octaves = 0;
        }

    }

    struct MapThreadInfo<T> {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter) {
            this.callback = callback;
            this.parameter = parameter;
        }
    }

}

[System.Serializable]
public struct TerrainType {
    public string name;
    public float height;
    public float moisture;
    public Color color;
}


public struct MapData {
    public readonly float[,] heightMap;
    public readonly float[,] moistureMap;
    public readonly Color[] biomeMap;
    public readonly List<Vector2> poissonDiskSamples;

    public MapData(float[,] heightMap, float[,] moistureMap, Color[] biomeMap, List<Vector2> poissonDiskSamples) {
        this.heightMap = heightMap;
        this.moistureMap = moistureMap;
        this.biomeMap = biomeMap;
        this.poissonDiskSamples = poissonDiskSamples;
    }
}