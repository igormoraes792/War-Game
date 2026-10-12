#nullable enable
using UnityEngine;

namespace War2D.Presentation.Board
{
    /// <summary>
    /// Gerador de arte procedural de alta resolução para a superfície do mapa 2D de WAR.
    /// Renderiza um fundo oceânico náutico clássico com gradiente suave, grade de coordenadas náuticas,
    /// Rosa dos Ventos clássica e moldura nobre de mogno com chanfro dourado.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapBoard2DGraphic : MonoBehaviour
    {
        [Header("Dimensões da Textura")]
        [SerializeField] private int textureWidth = 2048;
        [SerializeField] private int textureHeight = 1280;

        [Header("Cores Cartográficas Náuticas")]
        [SerializeField] private Color oceanDeepColor = new Color(0.08f, 0.13f, 0.20f, 1f);
        [SerializeField] private Color oceanShallowColor = new Color(0.12f, 0.19f, 0.29f, 1f);
        [SerializeField] private Color gridLineColor = new Color(0.20f, 0.30f, 0.42f, 0.18f);
        [SerializeField] private Color compassGoldColor = new Color(0.78f, 0.65f, 0.35f, 0.45f);

        private Texture2D? _generatedTexture;

        public Texture2D GenerateMapTexture()
        {
            if (_generatedTexture != null) return _generatedTexture;

            int w = textureWidth;
            int h = textureHeight;

            _generatedTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "War2D_Map_Ocean_Graphic",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[w * h];
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float maxR = Mathf.Sqrt(cx * cx + cy * cy);

            // 1. Oceano com gradiente radial suave (estilo mapa de gabinete náutico)
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / maxR);

                    // Centro azul marinho iluminado, bordas escuras profundas
                    Color col = Color.Lerp(oceanShallowColor, oceanDeepColor, t * 0.95f);

                    // Grade náutica sutil (linhas de latitude e longitude a cada 64 pixels)
                    bool isGridX = (x % 64 == 0);
                    bool isGridY = (y % 64 == 0);
                    if (isGridX || isGridY)
                    {
                        col = Color.Lerp(col, col + gridLineColor, 0.35f);
                    }

                    pixels[y * w + x] = col;
                }
            }

            // 2. Rosa dos Ventos Náutica no canto inferior esquerdo (Oceano Pacífico Sul)
            DrawCompassRose(pixels, w, h, (int)(w * 0.18f), (int)(h * 0.25f), (int)(w * 0.055f));

            // 3. Moldura de madeira nobre de mogno com chanfro duplo dourado nas bordas
            int borderThickness = (int)(w * 0.018f);
            Color woodColor = new Color(0.18f, 0.11f, 0.07f, 1f);
            Color goldRim = new Color(0.78f, 0.62f, 0.30f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x < borderThickness || x >= w - borderThickness ||
                                     y < borderThickness || y >= h - borderThickness);

                    if (isBorder)
                    {
                        bool isInnerGold = (x == borderThickness - 1 || x == w - borderThickness ||
                                            y == borderThickness - 1 || y == h - borderThickness);

                        pixels[y * w + x] = isInnerGold ? goldRim : woodColor;
                    }
                }
            }

            _generatedTexture.SetPixels(pixels);
            _generatedTexture.Apply(false, true);

            return _generatedTexture;
        }

        private void DrawCompassRose(Color[] pixels, int w, int h, int ox, int oy, int radius)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int distSq = dx * dx + dy * dy;
                    if (distSq > r2) continue;

                    int px = ox + dx;
                    int py = oy + dy;
                    if (px < 0 || px >= w || py < 0 || py >= h) continue;

                    float angle = Mathf.Atan2(dy, dx);
                    // Padrão de 8 pontas da rosa dos ventos
                    float ray = Mathf.Abs(Mathf.Cos(angle * 4f));
                    float ring = Mathf.Abs(Mathf.Sqrt(distSq) - radius * 0.65f);

                    if (ring < 2.5f || (ray > 0.85f && distSq < r2 * 0.85f))
                    {
                        int idx = py * w + px;
                        pixels[idx] = Color.Lerp(pixels[idx], compassGoldColor, 0.55f);
                    }
                }
            }
        }
    }
}
