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
            if (generatedTexture == null)
            {
                generatedTexture = GenerateProceduralMapTexture(textureWidth, textureHeight);
            }

            var tex = generatedTexture;

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                var shader = FindSafeLitShader();
                if (boardMaterial == null || boardMaterial.shader != shader)
                {
                    boardMaterial = new Material(shader)
                    {
                        name = "War_MapBoard_Procedural_Mat",
                        enableInstancing = true
                    };
                }
                else
                {
                    boardMaterial.enableInstancing = true;
                }

                boardMaterial.mainTexture = tex;
                if (boardMaterial.HasProperty("_BaseMap"))
                {
                    boardMaterial.SetTexture("_BaseMap", tex);
                }
                if (boardMaterial.HasProperty("_Smoothness"))
                {
                    boardMaterial.SetFloat("_Smoothness", 0.15f);
                }

                meshRenderer.sharedMaterial = boardMaterial;
            }

            return tex;
        }

        /// <summary>
        /// Cria a textura completa do mapa-múndi estilo arte em tela (Fine Art Canvas Map)
        /// idêntica à referência: oceanos em azul celeste suave (#8EBDE0 / #A7CFEC),
        /// plataformas continentais em azul translúcido (#BCE0F5),
        /// textura sutil de linho de tela e margens límpidas de galeria.
        /// </summary>
        public static Texture2D GenerateProceduralMapTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "War_Canvas_Art_World_Map",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];

            // Paleta autêntica inspirada na referência da tela (Canvas Wall Art):
            // Azul mar celeste vibrante e profundo dos oceanos
            var oceanMain = new Color(0.29f, 0.54f, 0.74f, 1f);     // #4A8ABC - Azul celeste náutico autêntico
            var oceanDeep = new Color(0.23f, 0.46f, 0.67f, 1f);     // #3B75AB - Azul oceânico mais profundo
            var oceanShelf = new Color(0.52f, 0.75f, 0.90f, 1f);    // #85BFE6 - Halo costeiro suave
            var antarcticaIce = new Color(0.85f, 0.91f, 0.96f, 1f); // #D9E8F5 - Calota polar antártica

            var blobs = GetContinentBlobData();

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / height;
                float worldZ = Mathf.Lerp(-18f, 18f, v);

                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float worldX = Mathf.Lerp(-29f, 29f, u);

                    // 1. Gradiente oceânico suave de latitude (águas equatoriais vs polares)
                    float latGradient = Mathf.SmoothStep(0f, 1f, Mathf.Abs(v - 0.48f) * 1.6f);
                    Color col = Color.Lerp(oceanMain, oceanDeep, latGradient * 0.35f);

                    // 2. Plataformas continentais: auréola costeira sutil apenas contornando a terra
                    float maxShelfInfluence = 0f;
                    foreach (var blob in blobs)
                    {
                        float dx = (worldX - blob.CenterX) / blob.RadiusX;
                        float dz = (worldZ - blob.CenterZ) / blob.RadiusZ;
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);

                        // Ruído orgânico sutil
                        float noise = (Mathf.PerlinNoise(worldX * 0.16f + blob.NoiseOffset, worldZ * 0.16f + blob.NoiseOffset) - 0.5f) * 0.12f;
                        float distWithNoise = dist + noise;

                        if (distWithNoise >= 0.92f && distWithNoise <= 1.38f)
                        {
                            float shelf = 1f - Mathf.Abs(distWithNoise - 1.15f) / 0.23f;
                            shelf = Mathf.Clamp01(shelf);
                            if (shelf > maxShelfInfluence) maxShelfInfluence = shelf;
                        }
                    }

                    if (maxShelfInfluence > 0f)
                    {
                        col = Color.Lerp(col, oceanShelf, maxShelfInfluence * 0.45f);
                    }

                    // 3. Faixa de gelo da Antártida no polo sul (margem inferior discreta)
                    if (v < 0.050f)
                    {
                        float iceNoise = (Mathf.PerlinNoise(worldX * 0.22f + 55f, worldZ * 0.25f + 12f) - 0.5f) * 0.015f;
                        float iceEdge = 0.032f + iceNoise;
                        if (v < iceEdge)
                        {
                            col = antarcticaIce;
                        }
                        else if (v < iceEdge + 0.018f)
                        {
                            float tIce = (v - iceEdge) / 0.018f;
                            col = Color.Lerp(antarcticaIce, col, tIce);
                        }
                    }

                    // 4. Linhas cartográficas náuticas refinadas (trópicos, equador e meridianos discretos)
                    bool isEquator = (Mathf.Abs(worldZ + 0.5f) < 0.07f);
                    bool isTropic = (Mathf.Abs(Mathf.Abs(worldZ + 0.5f) - 6.8f) < 0.055f);
                    bool isMeridian = (Mathf.Abs(worldX % 8f) < 0.045f);

                    if (isEquator)
                    {
                        col = Color.Lerp(col, new Color(0.20f, 0.42f, 0.62f, 1f), 0.35f);
                    }
                    else if (isTropic || isMeridian)
                    {
                        col = Color.Lerp(col, new Color(0.22f, 0.45f, 0.65f, 1f), 0.22f);
                    }

                    // 5. Textura tátil sutil de linho de tela artística (Canvas Weave)
                    float weaveX = Mathf.Sin(x * 1.570796f);
                    float weaveY = Mathf.Sin(y * 1.570796f);
                    float weave = (weaveX * weaveY) * 0.012f;
                    col.r = Mathf.Clamp01(col.r + weave);
                    col.g = Mathf.Clamp01(col.g + weave);
                    col.b = Mathf.Clamp01(col.b + weave);

                    pixels[y * width + x] = col;
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
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit != null) return unlit;
                var simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (simpleLit != null) return simpleLit;
            }

            return Shader.Find("Unlit/Texture") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
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
            return new List<ContinentBlob>
            {
                // América do Norte
                new ContinentBlob(-18.5f, 9.5f, 9.5f, 7.5f, new Color(0.35f, 0.45f, 0.38f, 1f), 10.5f),
                new ContinentBlob(-7.9f, 11.7f, 4.2f, 4.0f, new Color(0.78f, 0.52f, 0.22f, 1f), 22.1f), // Groenlândia
                // América do Sul
                new ContinentBlob(-12.5f, -6.5f, 5.8f, 7.8f, new Color(0.75f, 0.38f, 0.35f, 1f), 35.3f),
                // Europa
                new ContinentBlob(-0.8f, 7.0f, 6.5f, 5.8f, new Color(0.45f, 0.55f, 0.65f, 1f), 48.7f),
                // África
                new ContinentBlob(2.2f, -6.5f, 6.8f, 8.2f, new Color(0.78f, 0.62f, 0.32f, 1f), 61.2f),
                // Ásia
                new ContinentBlob(14.5f, 6.8f, 11.5f, 8.5f, new Color(0.82f, 0.68f, 0.38f, 1f), 74.9f),
                // Índia / Sudeste Asiático
                new ContinentBlob(12.5f, -0.8f, 4.8f, 4.2f, new Color(0.78f, 0.42f, 0.32f, 1f), 81.3f),
                // Oceania
                new ContinentBlob(19.5f, -8.5f, 5.5f, 5.2f, new Color(0.42f, 0.58f, 0.42f, 1f), 93.4f)
            };
        }
    }
}
