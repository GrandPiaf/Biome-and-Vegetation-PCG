using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    
    public enum DrawMode { None, HeightMap, MoistureMap, BiomeColorMap, Mesh, VegetationMap };
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
    public int newPointsCount;


    public Vector2 offset;

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

        if (drawMode == DrawMode.None) {
            display.ResetDisplay();
        }
        else if (drawMode == DrawMode.HeightMap) {
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
        else if (drawMode == DrawMode.VegetationMap) {
            display.DrawTexture(TextureGenerator.TextureFromVegetationList(mapData.poissonDiskSamples, mapData.heightMap.GetLength(0), mapData.heightMap.GetLength(1)));
        }
    }

    public void RequestMapData(UnityEngine.Vector2 center, Action<MapData> callback) {
        ThreadStart threadStart = delegate {
            MapDataThread(center, callback);
        };
        new Thread(threadStart).Start();
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

    MapData GenerateMapData(Vector2 center) {

        float[,] heightMap = Noise.GenerateNoiseMap(seedHeight, mapChunkSize, mapChunkSize, noiseScale, octaves, persistance, lacunarity, center + offset, normalizeMode);

        float[,] moistureMap = Noise.GenerateNoiseMap(seedMoisture, mapChunkSize, mapChunkSize, noiseScale, octaves, persistance, lacunarity, center + offset, normalizeMode);

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

        /*
         * Pour chaque region
         *      Générer une carte de place d'arbre selon le minDistance défini dans l'éditeur
         *      Garder QUE les arbres placés dans le biome correspondant
         */

        List<PoissonSampleData> poissonDiskSamples = new List<PoissonSampleData>();

        for (int i = 0; i < regions.Length; i++) {
            List<Vector2> poissonDiskSamplesRegion = Noise.GeneratePoissonDiskSampling(seedVegetation, mapChunkSize, mapChunkSize, newPointsCount, regions[i].minDistance);

            for (int k = 0; k < poissonDiskSamplesRegion.Count; k++) {

                Color biomeColor = biomeMap[(int)poissonDiskSamplesRegion[k].y * mapChunkSize + (int)poissonDiskSamplesRegion[k].x];

                if (biomeColor.Equals(regions[i].color)) {
                    poissonDiskSamples.Add(new PoissonSampleData(poissonDiskSamplesRegion[k], regions[i].vegetationPrefab));
                }
            }
        }

        return new MapData(heightMap, moistureMap, biomeMap, poissonDiskSamples, meshHeightCurve, meshHeightMultiplier);

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
    public float minDistance;
    public Color color;
    public GameObject vegetationPrefab;
}


public struct MapData {
    public readonly float[,] heightMap;
    public readonly float[,] moistureMap;
    public readonly Color[] biomeMap;
    public readonly List<PoissonSampleData> poissonDiskSamples;
    public readonly AnimationCurve heightCurve;
    public readonly float heightMultiplier;

    public MapData(float[,] heightMap, float[,] moistureMap, Color[] biomeMap, List<PoissonSampleData> poissonDiskSamples, AnimationCurve heightCurve, float heightMultiplier) {
        this.heightMap = heightMap;
        this.moistureMap = moistureMap;
        this.biomeMap = biomeMap;
        this.poissonDiskSamples = poissonDiskSamples;
        this.heightCurve = heightCurve;
        this.heightMultiplier = heightMultiplier;
    }
}

public struct PoissonSampleData {
    public readonly Vector2 position;
    public readonly GameObject vegetationPrefab;

    public PoissonSampleData(Vector2 position, GameObject vegetationPrefab) {
        this.position = position;
        this.vegetationPrefab = vegetationPrefab;
    }
}