using UnityEngine;

namespace FigmaImporter.Editor
{
    public static class TextureUtils
    {
        // Expand the texture by adding padding around it
        public static byte[] Expand(byte[] bytes, int padding)
        {
            var originalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            originalTexture.LoadImage(bytes);

            var originalWidth = originalTexture.width;
            var originalHeight = originalTexture.height;

            var width = originalTexture.width + 2 * padding;
            var height = originalTexture.height + 2 * padding;

            var potTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Fill the new texture with a default color (e.g., transparent or black)
            var fillColors = new Color[width * height];
            for (var i = 0; i < fillColors.Length; i++)
            {
                fillColors[i] = new Color(0, 0, 0, 0); // Transparent black
            }

            potTexture.SetPixels(fillColors);

            var offsetX = (width - originalWidth) / 2;
            var offsetY = (height - originalHeight) / 2;
            potTexture.SetPixels(offsetX, offsetY, originalWidth, originalHeight, originalTexture.GetPixels());

            // Apply changes to the new texture
            potTexture.Apply();

            return potTexture.EncodeToPNG();
        }

        // Crop the texture to the bounding box of non-transparent pixels
        public static byte[] AutoCrop(byte[] bytes)
        {
            var originalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            originalTexture.LoadImage(bytes);

            var pixels = originalTexture.GetPixels();
            int width = originalTexture.width;
            int height = originalTexture.height;

            int minX = width, maxX = 0, minY = height, maxY = 0;

            // Find the bounding box of non-transparent pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = pixels[y * width + x];
                    if (pixel.a > 0) // Only consider non-transparent pixels
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // Check if the image is fully transparent
            if (maxX < minX || maxY < minY)
            {
                return bytes; // No visible content, return original image
            }

            int croppedWidth = maxX - minX + 1;
            int croppedHeight = maxY - minY + 1;

            var croppedTexture = new Texture2D(croppedWidth, croppedHeight, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(originalTexture.GetPixels(minX, minY, croppedWidth, croppedHeight));
            croppedTexture.Apply();

            return croppedTexture.EncodeToPNG();
        }

        // Expand the texture to the nearest power of two
        public static byte[] ExpandToPot(byte[] bytes)
        {
            var originalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            originalTexture.LoadImage(bytes);

            var originalWidth = originalTexture.width;
            var originalHeight = originalTexture.height;

            var potWidth = Mathf.NextPowerOfTwo(originalTexture.width);
            var potHeight = Mathf.NextPowerOfTwo(originalTexture.height);

            var potTexture = new Texture2D(potWidth, potHeight, TextureFormat.RGBA32, false);

            // Fill the new texture with a default color (e.g., transparent or black)
            var fillColors = new Color[potWidth * potHeight];
            for (var i = 0; i < fillColors.Length; i++)
            {
                fillColors[i] = new Color(0, 0, 0, 0); // Transparent black
            }

            potTexture.SetPixels(fillColors);

            var offsetX = (potWidth - originalWidth) / 2;
            var offsetY = (potHeight - originalHeight) / 2;
            potTexture.SetPixels(offsetX, offsetY, originalWidth, originalHeight, originalTexture.GetPixels());

            // Apply changes to the new texture
            potTexture.Apply();

            return potTexture.EncodeToPNG();
        }
    }
}