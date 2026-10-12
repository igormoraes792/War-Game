#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Data;
using War.Core.Models;

namespace War.Presentation.Board
{
    /// <summary>
    /// Gerador da superfície gráfica estilizada do mapa-múndi tático 3D.
    /// Gera proceduralmente a textura náutica/militar de alta resolução (oceanos profundos,
    /// silhuetas dos continentes, malha de latitude/longitude e acabamento de sala de comando).
    /// </summary>
    [ExecuteAlways]
    public class MapBoardGraphic : MonoBehaviour
    {
        [Header("Dimensões do Tabuleiro")]
        [SerializeField] private float boardWidth = 52f;
        [SerializeField] private float boardDepth = 32f;

        [Header("Resolução da Textura Procedural")]
        [SerializeField] private int textureWidth = 2048;
        [SerializeField] private int textureHeight = 1280;

        [Header("Materiais e Texturas")]
        [SerializeField] private Material? boardMaterial;
        [SerializeField] private Texture2D? generatedTexture;

        public float BoardWidth => boardWidth;
        public float BoardDepth => boardDepth;
        public Texture2D? BoardTexture => generatedTexture;

        /// <summary>
        /// Gera a textura procedural do mapa e atribui ao MeshRenderer deste objeto.
        /// </summary>
        public Texture2D GenerateAndApplyTexture()
        {
            var tex = GenerateProceduralMapTexture(textureWidth, textureHeight);
            generatedTexture = tex;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                var shader = FindSafeLitShader();
                if (boardMaterial == null || boardMaterial.shader != shader)
                {
                    boardMaterial = new Material(shader)
                    {
                        name = "War_MapBoard_Procedural_Mat"
                    };
                }

                boardMaterial.mainTexture = tex;
                if (boardMaterial.HasProperty("_BaseMap"))
                {
                    boardMaterial.SetTexture("_BaseMap", tex);
                }
                if (boardMaterial.HasProperty("_Smoothness"))
                {
                    boardMaterial.SetFloat("_Smoothness", 0.35f); // Acabamento acetinado militar
                }

                meshRenderer.sharedMaterial = boardMaterial;
            }

            return tex;
        }

        /// <summary>
        /// Cria a textura completa com oceano escuro acetinado, continentes suaves,
        /// linhas de latitude/longitude e detalhes cartográficos.
        /// </summary>
        public static Texture2D GenerateProceduralMapTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "War_Procedural_Tactical_Map",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];

            // Cores do oceano (gradiente de sala de comando / ardósia marinho profundo)
            var oceanDeep = new Color(0.045f, 0.082f, 0.137f, 1f);      // #0B1523
            var oceanMid = new Color(0.075f, 0.125f, 0.196f, 1f);       // #132032
            var oceanTrench = new Color(0.030f, 0.055f, 0.090f, 1f);

            // Centros aproximados dos continentes normalizados [0, 1]
            // Mesa vai de X: -26 a +26 (largura 52), Z: -16 a +16 (profundidade 32)
            var continentBlobs = GetContinentBlobData();

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / height;
                float worldZ = Mathf.Lerp(-16f, 16f, v);

                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float worldX = Mathf.Lerp(-26f, 26f, u);

                    // 1. Base do Oceano com vinheta e leve gradiente radial
                    float distFromCenter = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                    Color current = Color.Lerp(oceanMid, oceanDeep, Mathf.Clamp01(distFromCenter * 1.3f));

                    // Leve textura de batimetria / ruído sutil de profundidade
                    float depthNoise = Mathf.PerlinNoise(u * 12f, v * 12f) * 0.04f;
                    current += new Color(depthNoise, depthNoise * 1.2f, depthNoise * 1.5f, 0f);

                    // 2. Projeção das silhuetas dos 6 Continentes com gradientes e tons temáticos
                    float landInfluence = 0f;
                    Color landColorAcc = Color.clear;

                    foreach (var blob in continentBlobs)
                    {
                        float dX = (worldX - blob.CenterX) / blob.RadiusX;
                        float dZ = (worldZ - blob.CenterZ) / blob.RadiusZ;
                        float distSq = dX * dX + dZ * dZ;

                        if (distSq < 1.6f)
                        {
                            // Ruído na costa para criar relevo orgânico irregular
                            float coastlineNoise = (Mathf.PerlinNoise(worldX * 0.45f + blob.NoiseOffset, worldZ * 0.45f + blob.NoiseOffset) - 0.5f) * 0.35f;
                            float effectiveDist = Mathf.Sqrt(distSq) + coastlineNoise;

                            if (effectiveDist < 1.0f)
                            {
                                float alpha = Mathf.SmoothStep(1.0f, 0.3f, effectiveDist);
                                landInfluence = Mathf.Max(landInfluence, alpha);
                                landColorAcc = Color.Lerp(landColorAcc, blob.ThemeColor, alpha * 0.75f);
                            }
                            else if (effectiveDist < 1.15f)
                            {
                                // Borda costeira sutil (halo azul-turquesa / linha de costa militar)
                                float shelfAlpha = Mathf.SmoothStep(1.15f, 1.0f, effectiveDist) * 0.22f;
                                current = Color.Lerp(current, new Color(0.12f, 0.32f, 0.42f, 1f), shelfAlpha);
                            }
                        }
                    }

                    if (landInfluence > 0f)
                    {
                        current = Color.Lerp(current, landColorAcc, landInfluence * 0.85f);
                    }

                    // 3. Grid tático náutico (Latitude / Longitude finas)
                    bool isGridX = (Mathf.Abs(worldX % 4f) < 0.08f);
                    bool isGridZ = (Mathf.Abs(worldZ % 4f) < 0.08f);
                    bool isEquator = (Mathf.Abs(worldZ) < 0.12f);
                    bool isPrimeMeridian = (Mathf.Abs(worldX) < 0.12f);

                    if (isEquator || isPrimeMeridian)
                    {
                        current = Color.Lerp(current, new Color(0.35f, 0.55f, 0.65f, 1f), 0.35f);
                    }
                    else if (isGridX || isGridZ)
                    {
                        current = Color.Lerp(current, new Color(0.20f, 0.35f, 0.45f, 1f), 0.16f);
                    }

                    // 4. Borda moldurada tática (faixa de margem com régua militar)
                    float edgeMarginX = Mathf.Min(u, 1f - u);
                    float edgeMarginY = Mathf.Min(v, 1f - v);
                    float edgeDist = Mathf.Min(edgeMarginX * width, edgeMarginY * height);

                    if (edgeDist < 30f)
                    {
                        // Moldura dourada / latão escovado escuro
                        float borderAlpha = Mathf.SmoothStep(30f, 0f, edgeDist);
                        var brassBorder = new Color(0.18f, 0.15f, 0.10f, 1f);
                        if (edgeDist < 8f)
                        {
                            brassBorder = new Color(0.48f, 0.38f, 0.20f, 1f); // Filete dourado
                        }
                        current = Color.Lerp(current, brassBorder, borderAlpha);
                    }

                    pixels[y * width + x] = current;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Shader FindSafeLitShader()
        {
            var isUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (isUrp)
            {
                var s = Shader.Find("Universal Render Pipeline/Lit");
                if (s != null) return s;
                s = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s != null) return s;
            }

            return Shader.Find("Standard") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Mobile/Diffuse");
        }

        private struct ContinentBlob
        {
            public float CenterX;
            public float CenterZ;
            public float RadiusX;
            public float RadiusZ;
            public Color ThemeColor;
            public float NoiseOffset;

            public ContinentBlob(float cx, float cz, float rx, float rz, Color col, float noise)
            {
                CenterX = cx;
                CenterZ = cz;
                RadiusX = rx;
                RadiusZ = rz;
                ThemeColor = col;
                NoiseOffset = noise;
            }
        }

        private static List<ContinentBlob> GetContinentBlobData()
        {
            // Silhuetas aproximadas alinhadas com as posições dos territórios do War
            return new List<ContinentBlob>
            {
                // América do Norte (Verde musgo militar suave)
                new ContinentBlob(-18.5f, 9.5f, 9.5f, 7.5f, new Color(0.12f, 0.19f, 0.15f, 1f), 10.5f),
                new ContinentBlob(-12f, 13.5f, 4.5f, 3.5f, new Color(0.13f, 0.21f, 0.17f, 1f), 22.1f), // Groenlândia

                // América do Sul (Oliva escuro / terra suave)
                new ContinentBlob(-13.5f, -6.5f, 5.5f, 7.5f, new Color(0.20f, 0.18f, 0.12f, 1f), 35.3f),

                // Europa (Azul ardósia tático sutil)
                new ContinentBlob(-0.5f, 9.0f, 7.5f, 6.0f, new Color(0.14f, 0.18f, 0.24f, 1f), 48.7f),

                // África (Bronze / ocre suave)
                new ContinentBlob(1.0f, -5.5f, 6.5f, 8.5f, new Color(0.22f, 0.17f, 0.11f, 1f), 61.2f),

                // Ásia (Ardósia quente / cobre suave)
                new ContinentBlob(15.5f, 8.5f, 12.0f, 8.5f, new Color(0.21f, 0.19f, 0.16f, 1f), 74.9f),
                new ContinentBlob(11.0f, 2.0f, 5.5f, 4.5f, new Color(0.20f, 0.17f, 0.14f, 1f), 81.3f), // Índia / Oriente

                // Oceania (Turquesa / esmeralda naval suave)
                new ContinentBlob(20.5f, -7.0f, 6.0f, 5.5f, new Color(0.10f, 0.22f, 0.21f, 1f), 93.4f)
            };
        }
    }
}
