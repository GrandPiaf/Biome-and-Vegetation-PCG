using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TextureGenerator
{
    public static Texture2D TextureFromColorMap(Color[] colorMap, int width, int height) {
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colorMap);
        texture.Apply();
        return texture;
    }

    public static Texture2D TextureFromHeightMap(float[,] noiseMap) {
        int width = noiseMap.GetLength(0);
        int height = noiseMap.GetLength(1);

        Texture2D texture = new Texture2D(width, height);

        // It is faster to fill an array and use it in the texture
        // Than setting each pixel one by one in the texture directly
        Color[] colorMap = new Color[width * height];

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                colorMap[y * width + x] = Color.Lerp(Color.black, Color.white, noiseMap[x, y]);
            }
        }

        return TextureFromColorMap(colorMap, width, height);
    }

    public static Texture2D TextureFromVegetationList(List<PoissonSampleData> poissonDiskSamples, int width, int height) {

        Color[] colorMap = new Color[width * height];

        // Initialize to black
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                colorMap[y * width + x] = Color.black;
            }
        }

        for (int i = 0; i < poissonDiskSamples.Count; i++) {
            int y = (int)poissonDiskSamples[i].position.y;
            int x = (int)poissonDiskSamples[i].position.x;
            colorMap[y * width + x] = Color.white;
        }

        return TextureFromColorMap(colorMap, width, height);

    }

}
