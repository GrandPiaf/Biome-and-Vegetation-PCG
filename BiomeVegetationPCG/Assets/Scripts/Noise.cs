using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    public enum NormalizeMode {Local, Global};

    public static float [,] GenerateNoiseMap(int seed, int mapWidth, int mapHeight, float scale, int octaves, float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode) {

        System.Random prng = new System.Random(seed);

        float[,] noiseMap = new float[mapWidth, mapHeight];

        UnityEngine.Vector2[] octaveOffsets = new UnityEngine.Vector2[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++) {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new UnityEngine.Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }
            

        if (scale <= 0) {
            scale = 0.0001f;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {

                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++) {

                    float sampleX = (x - halfWidth + octaveOffsets[i].x) / scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance; //persistance is in range (0,1) so amplitude decreases
                    frequency *= lacunarity; // frequency increases

                }

                if (noiseHeight > maxLocalNoiseHeight) {
                    maxLocalNoiseHeight = noiseHeight;
                } else if (noiseHeight < minLocalNoiseHeight) {
                    minLocalNoiseHeight = noiseHeight;
                }

                noiseMap[x, y] = noiseHeight;

            }
        }

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                if (normalizeMode == NormalizeMode.Local) { //If not using endless terrain
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                } else {
                    float normalizedHeight = (noiseMap[x, y] + 1f) / (2f * maxPossibleHeight / 2f);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        return noiseMap;
    
    }


    public static List<Vector2> GeneratePoissonDiskSampling(int seed, int mapWidth, int mapHeight, int newPointsCount, float minDistance) {

        if (minDistance <= 0) { // Empty list, no tree for this map
            return new List<Vector2>();
        }

        System.Random prng = new System.Random(seed);


        float cellSize = minDistance / Mathf.Sqrt(2f);

        PoissonGrid grid = new PoissonGrid((int)Mathf.Ceil(mapWidth / cellSize) + 1, (int)Mathf.Ceil(mapHeight / cellSize) + 1, cellSize); // + 1 to have correct grid size

        List<Vector2> samplePoints = new List<Vector2>();

        // Used as a random queue
        List<Vector2> processList = new List<Vector2>();

        // Generate first point randomly
        float firstPointX = ((float)prng.NextDouble()) * mapWidth;
        float firstPointY = ((float)prng.NextDouble()) * mapHeight;
        Vector2 firstPoint = new Vector2(firstPointX, firstPointY);

        processList.Add(firstPoint);
        samplePoints.Add(firstPoint);
        grid.Add(firstPoint);

        // List not empty
        while (processList.Count != 0) {

            int indexPop = prng.Next(0, processList.Count);

            Vector2 point = processList[indexPop];
            processList.RemoveAt(indexPop);

            for (int i = 0; i < newPointsCount; i++) {

                Vector2 newPoint = GenerateRandomPointAround(point, minDistance, prng);

                if (IsInside(mapWidth, mapHeight, newPoint) && !grid.IsNeighbourClose(newPoint, minDistance)) {
                    processList.Add(newPoint);
                    samplePoints.Add(newPoint);
                    grid.Add(newPoint);
                }

            }

        }

        return samplePoints;
    }

    private static bool IsCorrectBiome(Vector2 newPoint, int width, Color[] biomeMap, Color currentBiome) {
        return biomeMap[(int)newPoint.y * width + (int)newPoint.x].Equals(currentBiome);
    }

    private static bool IsInside(float mapWidth, float mapHeight, Vector2 newPoint) {
        return newPoint.x >= 0 && newPoint.x < mapWidth && newPoint.y >= 0 && newPoint.y < mapHeight;
    }

    private static Vector2 GenerateRandomPointAround(Vector2 point, float minDistance, System.Random prng) {

        float r1 = (float)prng.NextDouble();
        float r2 = (float)prng.NextDouble();

        float radius = minDistance * (r1 + 1f);

        float angle = 2f * Mathf.PI * r2;

        float newX = point.x + radius * Mathf.Cos(angle);
        float newY = point.y + radius * Mathf.Sin(angle);

        return new Vector2(newX, newY);
    }

}

public class PoissonGrid {

    public int gridWidth;
    public int gridHeight;

    public float cellSize;

    public GridCell[,] grid;

    public PoissonGrid(int gridWidth, int gridHeight, float cellSize) {
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
        this.cellSize = cellSize;
        this.grid = new GridCell[gridWidth, gridHeight];
    }

    public void Add(Vector2 firstPoint) {
        Vector2Int cellCoords = GetCellCoordinates(firstPoint);
        grid[cellCoords.x, cellCoords.y] = new GridCell();
        grid[cellCoords.x, cellCoords.y].Add(firstPoint);
    }

    private Vector2Int GetCellCoordinates(Vector2 firstPoint) {
        int cellX = (int)Mathf.Ceil(firstPoint.x / cellSize);
        int cellY = (int)Mathf.Ceil(firstPoint.y / cellSize);
        return new Vector2Int(cellX, cellY);
    }

    public bool IsNeighbourClose(Vector2 newPoint, float minDistance) {

        // Retrieve cell coordinates in array
        Vector2Int cellCoords = GetCellCoordinates(newPoint);

        // Loop through neighbours cell
        int minX = Mathf.Max(0, cellCoords.x - 2);
        int maxX = Mathf.Min(gridWidth, cellCoords.x + 3);

        int minY = Mathf.Max(0, cellCoords.y - 2);
        int maxY = Mathf.Min(gridHeight, cellCoords.y + 3);

        for (int x = minX; x < maxX; x++) {
            for (int y = minY; y < maxY; y++) {

                if (grid[x, y] != null) { //There is something to check

                    if (grid[x, y].Distance(newPoint) < minDistance) {
                        return true;
                    }

                }

            }
        }

        return false;
    }

    public class GridCell {
        public Vector2 content;

        public void Add(Vector2 point) {
            content = point;
        }

        public float Distance(Vector2 newPoint) {
            return Vector2.Distance(content, newPoint);
        }
    }

}
