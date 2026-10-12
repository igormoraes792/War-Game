#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Data;

namespace War2D.Presentation.Board
{
    /// <summary>
    /// Utilitário gerador de malhas e colisores 2D ortográficos para os territórios de WAR.
    /// Gera malhas 2D no plano XY com coordenadas UV perfeitas e pontos para PolygonCollider2D.
    /// </summary>
    public static class Territory2DMeshBuilder
    {
        public const float PolygonInsetAmount = 0.045f;

        private static readonly Dictionary<string, Vector2[]> TerritoryPolygons = new(StringComparer.OrdinalIgnoreCase);

        static Territory2DMeshBuilder()
        {
            RegisterAllPolygons();
        }

        /// <summary>
        /// Cria uma malha circular plana 2D perfeita para fichas e medalhões militares.
        /// </summary>
        public static Mesh CreateCircleMesh(float radius, int segments = 28)
        {
            var mesh = new Mesh { name = $"Mesh2D_Circle_{radius:F2}" };
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = -Vector3.forward;

            float angleStep = 2f * Mathf.PI / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(cos * radius, sin * radius, 0f);
                uvs[i + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                normals[i + 1] = -Vector3.forward;

                int next = (i + 1) % segments;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            return mesh;
        }

        /// <summary>
        /// Gera uma malha plana 2D voltada para a câmera (plano XY, normal -Z) para o território.
        /// </summary>
        public static Mesh CreateTerritory2DMesh(string territoryId)
        {
            var rawPoly = GetTerritoryPolygon(territoryId);
            var poly = InsetPolygon(rawPoly, PolygonInsetAmount);
            int n = poly.Length;

            var mesh = new Mesh
            {
                name = $"Mesh2D_Territory_{territoryId}"
            };

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (poly[i].x < minX) minX = poly[i].x;
                if (poly[i].x > maxX) maxX = poly[i].x;
                if (poly[i].y < minY) minY = poly[i].y;
                if (poly[i].y > maxY) maxY = poly[i].y;
            }
            float sizeX = Mathf.Max(0.01f, maxX - minX);
            float sizeY = Mathf.Max(0.01f, maxY - minY);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(poly[i].x, poly[i].y, 0f));
                normals.Add(-Vector3.forward);
                uvs.Add(new Vector2((poly[i].x - minX) / sizeX, (poly[i].y - minY) / sizeY));
            }

            var tris = TriangulatePolygon(poly);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int i0 = tris[i];
                int i1 = tris[i + 1];
                int i2 = tris[i + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);

                if (cross.z <= 0f)
                {
                    triangles.Add(i0);
                    triangles.Add(i1);
                    triangles.Add(i2);
                }
                else
                {
                    triangles.Add(i0);
                    triangles.Add(i2);
                    triangles.Add(i1);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Obtém os vértices 2D locais do polígono de fronteira de um território.
        /// </summary>
        public static Vector2[] GetTerritoryPolygon(string territoryId)
        {
            if (TerritoryPolygons.TryGetValue(territoryId, out var poly))
            {
                return poly;
            }

            // Fallback elegante (octógono regular de raio 1.6f)
            return CreateRegularPolygon(8, 1.6f);
        }

        /// <summary>
        /// Calcula o centroide 2D (centro geométrico de massa) de um polígono.
        /// </summary>
        public static Vector2 GetPolygonCentroid(Vector2[] poly)
        {
            if (poly == null || poly.Length == 0) return Vector2.zero;

            float signedArea = 0f;
            float cx = 0f;
            float cy = 0f;

            for (int i = 0; i < poly.Length; i++)
            {
                int next = (i + 1) % poly.Length;
                float cross = (poly[i].x * poly[next].y - poly[next].x * poly[i].y);
                signedArea += cross;
                cx += (poly[i].x + poly[next].x) * cross;
                cy += (poly[i].y + poly[next].y) * cross;
            }

            signedArea *= 0.5f;
            if (Mathf.Abs(signedArea) < 1e-4f)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < poly.Length; i++) sum += poly[i];
                return sum / poly.Length;
            }

            return new Vector2(cx / (6f * signedArea), cy / (6f * signedArea));
        }

        /// <summary>
        /// Aplica um pequeno recuo/inset em relação ao centroide do polígono.
        /// Cria folgas limpas entre peças de quebra-cabeça vizinhas, eliminando Z-fighting nas arestas compartilhadas.
        /// </summary>
        public static Vector2[] InsetPolygon(Vector2[] poly, float insetAmount)
        {
            if (poly == null || poly.Length == 0) return Array.Empty<Vector2>();
            if (insetAmount <= 0.0001f) return poly;

            Vector2 centroid = GetPolygonCentroid(poly);
            var insetPoly = new Vector2[poly.Length];

            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 diff = poly[i] - centroid;
                float dist = diff.magnitude;

                if (dist > 0.05f)
                {
                    // Limita o recuo a no máximo 14% da distância ao centro para não colapsar pontas estreitas
                    float effectiveInset = Mathf.Min(insetAmount, dist * 0.14f);
                    insetPoly[i] = poly[i] - (diff / dist) * effectiveInset;
                }
                else
                {
                    insetPoly[i] = poly[i];
                }
            }

            return insetPoly;
        }

        /// <summary>
        /// Triangulação por Ear-Clipping para qualquer polígono simples 2D.
        /// </summary>
        public static List<int> TriangulatePolygon(Vector2[] poly)
        {
            var triangles = new List<int>();
            int n = poly.Length;
            if (n < 3) return triangles;

            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area += (a.x * b.y - b.x * a.y);
            }

            var indices = new List<int>(n);
            if (area < 0f)
            {
                for (int i = n - 1; i >= 0; i--) indices.Add(i);
            }
            else
            {
                for (int i = 0; i < n; i++) indices.Add(i);
            }

            int safetyCounter = 0;
            while (indices.Count > 3 && safetyCounter < 300)
            {
                safetyCounter++;
                bool earFound = false;

                for (int i = 0; i < indices.Count; i++)
                {
                    int prevIdx = indices[(i - 1 + indices.Count) % indices.Count];
                    int currIdx = indices[i];
                    int nextIdx = indices[(i + 1) % indices.Count];

                    Vector2 a = poly[prevIdx];
                    Vector2 b = poly[currIdx];
                    Vector2 c = poly[nextIdx];

                    float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                    if (cross <= 1e-6f) continue;

                    bool isEar = true;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int testIdx = indices[j];
                        if (testIdx == prevIdx || testIdx == currIdx || testIdx == nextIdx) continue;

                        Vector2 p = poly[testIdx];
                        if (IsPointInsideTriangle(p, a, b, c))
                        {
                            isEar = false;
                            break;
                        }
                    }

                    if (isEar)
                    {
                        triangles.Add(prevIdx);
                        triangles.Add(currIdx);
                        triangles.Add(nextIdx);
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    if (indices.Count >= 3)
                    {
                        triangles.Add(indices[0]);
                        triangles.Add(indices[1]);
                        triangles.Add(indices[2]);
                        indices.RemoveAt(1);
                    }
                    else break;
                }
            }

            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }

            return triangles;
        }

        private static bool IsPointInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float cross1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            float cross2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            float cross3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);

            bool hasNeg = (cross1 < 0) || (cross2 < 0) || (cross3 < 0);
            bool hasPos = (cross1 > 0) || (cross2 > 0) || (cross3 > 0);

            return !(hasNeg && hasPos);
        }

        private static Vector2[] CreateRegularPolygon(int sides, float radius)
        {
            var pts = new Vector2[sides];
            float angleStep = Mathf.PI * 2f / sides;
            for (int i = 0; i < sides; i++)
            {
                float a = i * angleStep;
                pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            return pts;
        }

        /// <summary>
        /// Registra os 42 polígonos representativos das fronteiras geográficas oficiais do WAR.
        /// Extraídos do mapa vetorial de alta precisão (encaixe perfeito estilo quebra-cabeça 3D).
        /// </summary>
        private static void RegisterAllPolygons()
        {
            // América do Sul (4)
            TerritoryPolygons[TerritoryIds.Venezuela] = new Vector2[]
            {
                new(-2.45f, -0.72f), new(-1.98f, 0.73f), new(-0.35f, 1.32f),
                new(-0.22f, 1.08f), new(1.11f, 1.02f), new(2.23f, 0.20f),
                new(3.45f, -0.05f), new(2.34f, -0.70f), new(1.57f, -0.68f),
                new(1.36f, -0.09f), new(0.61f, -0.32f), new(0.55f, -0.68f),
                new(-0.28f, -0.64f), new(-0.45f, -1.34f), new(-0.92f, -1.50f)
            };

            TerritoryPolygons[TerritoryIds.Peru] = new Vector2[]
            {
                new(-1.00f, 2.60f), new(-0.07f, 1.71f), new(1.18f, 0.90f),
                new(0.76f, 0.32f), new(1.79f, -0.08f), new(1.51f, -0.31f),
                new(1.10f, -1.30f), new(0.80f, -2.20f), new(-0.10f, -2.20f),
                new(-0.70f, -1.20f), new(-1.20f, 0.20f), new(-1.40f, 1.60f)
            };

            TerritoryPolygons[TerritoryIds.Brasil] = new Vector2[]
            {
                new(0.52f, 2.74f), new(-0.59f, 2.09f), new(-1.37f, 2.11f),
                new(-1.57f, 2.69f), new(-2.33f, 2.47f), new(-2.68f, 2.00f),
                new(-3.16f, 2.17f), new(-3.38f, 1.45f), new(-4.13f, 1.25f),
                new(-4.55f, 0.67f), new(-3.52f, 0.27f), new(-3.80f, 0.04f),
                new(-3.64f, -0.22f), new(-2.74f, 0.09f), new(-1.46f, -0.50f),
                new(-0.03f, -2.79f), new(-0.55f, -3.47f), new(0.29f, -4.22f),
                new(0.89f, -3.42f), new(1.05f, -2.55f), new(2.37f, -2.08f),
                new(2.83f, -1.38f), new(3.09f, 0.01f), new(3.65f, 0.44f),
                new(3.52f, 1.00f), new(3.05f, 1.28f), new(0.56f, 1.92f),
                new(0.78f, 2.23f)
            };

            TerritoryPolygons[TerritoryIds.Chile] = new Vector2[]
            {
                new(-1.30f, 3.80f), new(-0.40f, 3.80f), new(-0.10f, 2.40f),
                new(0.30f, 0.80f), new(0.70f, -0.80f), new(1.30f, -2.30f),
                new(1.80f, -3.80f), new(1.40f, -3.80f), new(0.50f, -2.60f),
                new(-0.10f, -1.10f), new(-0.50f, 0.60f), new(-0.90f, 2.20f)
            };

            TerritoryPolygons[TerritoryIds.Argentina] = new Vector2[]
            {
                new(-0.40f, -4.40f), new(-0.90f, -2.90f), new(-1.50f, -1.40f),
                new(-1.90f, 0.20f), new(-2.30f, 1.80f), new(-2.60f, 3.20f),
                new(-2.30f, 4.10f), new(-1.89f, 5.09f), new(-1.73f, 4.83f),
                new(-0.83f, 5.14f), new(0.45f, 4.55f), new(1.88f, 2.26f),
                new(1.36f, 1.58f), new(1.50f, 1.00f), new(2.00f, 0.60f),
                new(1.40f, -0.80f), new(0.70f, -2.30f), new(0.20f, -3.50f)
            };

            TerritoryPolygons[TerritoryIds.Uruguai] = new Vector2[]
            {
                new(-0.44f, 0.48f), new(0.10f, 0.25f), new(0.40f, -0.27f),
                new(0.30f, -0.70f), new(0.00f, -0.60f), new(-0.30f, -0.10f)
            };

            // América do Norte (8)
            TerritoryPolygons[TerritoryIds.Alasca] = new Vector2[]
            {
                new(1.93f, -2.30f), new(1.49f, -1.38f), new(0.42f, -0.84f),
                new(0.02f, -1.08f), new(-0.26f, -1.02f), new(-0.13f, -0.77f),
                new(-0.46f, -0.82f), new(-1.85f, -1.80f), new(-2.06f, -1.69f),
                new(-1.22f, -1.00f), new(-1.82f, -1.00f), new(-2.34f, -0.52f),
                new(-1.93f, 0.09f), new(-1.27f, 0.24f), new(-1.66f, 0.24f),
                new(-1.77f, 0.67f), new(-0.94f, 0.84f), new(-1.26f, 1.46f),
                new(-0.05f, 1.82f), new(1.41f, 1.34f), new(1.33f, -0.57f),
                new(1.85f, -0.64f), new(2.35f, -1.82f)
            };

            TerritoryPolygons[TerritoryIds.Mackenzie] = new Vector2[]
            {
                // Fronteira sul com a Califórnia (paralelo 49 até costa do Pacífico)
                new(-2.58f, -2.82f), new(-1.00f, -2.83f), new(0.71f, -2.86f),
                // Fronteira sul/leste com Ottawa contornando a Baía de Hudson
                new(0.98f, 0.05f), new(2.22f, 0.01f), new(2.02f, -0.70f),
                new(2.52f, -1.23f), new(3.82f, -1.39f),
                // Encaixe contíguo com o noroeste de Labrador (preenchendo o vazio acima de Ottawa)
                new(4.12f, -1.51f), new(4.66f, -1.15f), new(4.50f, -0.54f),
                new(4.86f, -0.16f), new(4.76f, 0.23f),
                // Litoral ártico setentrional autêntico (Estreito de Hudson, Boothia, Victoria, Beaufort)
                new(4.40f, 1.00f), new(3.40f, 1.80f), new(2.40f, 1.30f),
                new(1.40f, 2.60f), new(0.40f, 1.70f), new(-0.60f, 2.70f),
                new(-1.70f, 1.80f), new(-2.70f, 2.50f),
                // Fronteira oeste com o Alasca (Yukon)
                new(-3.75f, 2.13f), new(-3.83f, 0.22f), new(-3.31f, 0.15f),
                new(-2.81f, -1.03f),
                // Costa do Pacífico (Colúmbia Britânica até a Califórnia)
                new(-3.20f, -1.60f), new(-3.00f, -2.30f)
            };



            TerritoryPolygons[TerritoryIds.Groenlandia] = new Vector2[]
            {
                new(0.01f, -2.05f), new(-0.50f, -3.43f), new(-1.29f, -2.65f),
                new(-1.49f, -1.89f), new(-1.19f, -1.35f), new(-1.71f, -1.19f),
                new(-1.36f, -1.09f), new(-1.83f, -0.28f), new(-2.33f, 0.10f),
                new(-3.39f, 0.02f), new(-3.43f, 0.31f), new(-3.08f, 0.37f),
                new(-3.39f, 0.56f), new(-2.74f, 0.67f), new(-2.83f, 1.01f),
                new(-2.05f, 1.40f), new(-1.17f, 1.47f), new(-0.08f, 2.28f),
                new(1.55f, 2.16f), new(1.93f, 1.98f), new(1.30f, 1.71f),
                new(1.72f, 1.58f), new(2.85f, 1.88f), new(2.45f, 0.90f),
                new(2.67f, 0.10f), new(2.41f, -0.27f), new(2.06f, -0.19f),
                new(2.48f, -0.75f), new(2.20f, -0.94f), new(1.94f, -0.64f),
                new(2.06f, -1.08f)
            };

            TerritoryPolygons[TerritoryIds.Ottawa] = new Vector2[]
            {
                new(-0.10f, 2.08f), new(-0.30f, 1.37f), new(0.20f, 0.84f),
                new(1.50f, 0.68f), new(1.18f, 0.51f), new(1.58f, -0.46f),
                new(1.50f, -1.60f), new(1.91f, -1.46f), new(2.14f, -1.71f),
                new(1.04f, -2.38f), new(0.51f, -0.97f), new(-0.24f, -1.08f),
                new(-0.27f, -0.77f), new(-1.61f, -0.79f), new(-1.34f, 2.12f)
            };

            TerritoryPolygons[TerritoryIds.Labrador] = new Vector2[]
            {
                new(-1.72f, -1.69f), new(-1.41f, 0.47f), new(-0.87f, 0.83f),
                new(-1.03f, 1.44f), new(-0.67f, 1.82f), new(-0.77f, 2.21f),
                new(-0.05f, 2.15f), new(0.34f, 1.22f), new(1.04f, 1.62f),
                new(1.37f, 0.75f), new(2.27f, 0.16f), new(1.33f, -0.70f),
                new(-0.00f, -1.00f), new(0.15f, -1.20f), new(0.62f, -0.98f),
                new(0.76f, -1.69f), new(1.19f, -1.63f), new(0.77f, -2.08f),
                new(0.14f, -1.32f), new(-0.26f, -1.73f)
            };

            TerritoryPolygons[TerritoryIds.California] = new Vector2[]
            {
                new(-1.81f, 1.74f), new(-2.07f, 0.77f), new(-2.24f, -0.63f),
                new(-1.80f, -1.67f), new(-1.18f, -1.76f), new(-0.76f, -2.03f),
                new(-0.12f, -2.03f), new(0.35f, -2.35f), new(0.75f, -2.37f),
                new(0.60f, -1.85f), new(0.95f, -1.76f), new(1.07f, -1.11f),
                new(1.49f, -0.97f), new(1.52f, -0.58f), new(2.37f, -0.50f),
                new(2.40f, 1.73f)
            };

            TerritoryPolygons[TerritoryIds.NovaYork] = new Vector2[]
            {
                new(-1.14f, 2.44f), new(-0.72f, 2.44f), new(-0.69f, 2.12f),
                new(0.06f, 2.24f), new(0.59f, 0.82f), new(1.33f, 1.36f),
                new(2.62f, 1.61f), new(2.81f, 1.98f), new(3.16f, 1.38f),
                new(3.60f, 1.25f), new(3.17f, 0.83f), new(3.16f, 1.27f),
                new(2.62f, 1.00f), new(2.50f, 0.44f), new(1.65f, -0.25f),
                new(1.80f, -0.65f), new(0.76f, -1.50f), new(0.90f, -2.39f),
                new(0.65f, -2.51f), new(0.16f, -1.65f), new(-0.86f, -1.53f),
                new(-2.24f, -1.77f), new(-2.97f, -2.35f), new(-2.94f, -1.14f),
                new(-2.59f, -1.04f), new(-2.47f, -0.40f), new(-2.05f, -0.26f),
                new(-1.97f, 0.17f), new(-1.17f, 0.21f)
            };

            TerritoryPolygons[TerritoryIds.Mexico] = new Vector2[]
            {
                new(-2.18f, 2.10f), new(0.40f, 1.29f), new(0.21f, 0.62f),
                new(0.57f, -0.30f), new(1.86f, 0.04f), new(1.39f, -0.81f),
                new(1.65f, -1.25f), new(1.21f, -2.15f), new(1.84f, -2.10f),
                new(1.79f, -2.79f), new(0.95f, -2.26f), new(0.44f, -1.00f),
                new(-0.60f, -0.65f), new(-0.66f, 0.01f), new(-1.46f, 0.15f)
            };

            // Europa (7)
            TerritoryPolygons[TerritoryIds.Islandia] = new Vector2[]
            {
                new(-1.10f, 0.66f), new(-0.63f, 0.61f), new(-0.54f, 0.29f),
                new(-0.37f, 0.54f), new(0.15f, 0.47f), new(0.73f, 0.82f),
                new(1.26f, 0.37f), new(1.36f, -0.03f), new(0.85f, -0.47f),
                new(0.35f, -0.41f), new(0.36f, -0.68f), new(-0.04f, -0.86f),
                new(-1.03f, -0.67f), new(-0.88f, -0.32f), new(-1.34f, -0.12f),
                new(-1.03f, 0.15f), new(-1.38f, 0.34f)
            };

            TerritoryPolygons[TerritoryIds.Inglaterra] = new Vector2[]
            {
                new(-0.44f, -0.18f), new(-0.67f, -0.96f), new(-0.33f, -1.18f),
                new(-0.81f, -1.71f), new(1.04f, -1.36f), new(1.34f, -0.71f),
                new(1.17f, -0.47f), new(0.73f, -0.56f), new(0.33f, 0.61f),
                new(-0.21f, 0.74f), new(0.23f, 1.36f), new(0.13f, 1.58f),
                new(-0.30f, 1.49f), new(-0.19f, 2.03f), new(-0.69f, 1.99f),
                new(-1.18f, 1.41f), new(-0.57f, 0.60f), new(-0.63f, 0.19f),
                new(-0.19f, -0.08f), new(-0.21f, -0.35f)
            };

            TerritoryPolygons[TerritoryIds.Franca] = new Vector2[]
            {
                new(1.86f, 1.49f), new(1.54f, 0.84f), new(1.79f, 0.13f),
                new(1.48f, -0.08f), new(0.96f, 0.09f), new(0.55f, -0.57f),
                new(0.79f, -1.33f), new(0.35f, -1.91f), new(-0.18f, -2.31f),
                new(-0.97f, -1.88f), new(-1.55f, -1.92f), new(-1.89f, -1.62f),
                new(-1.57f, -0.69f), new(-1.93f, -0.04f), new(-1.58f, 0.25f),
                new(-1.11f, 0.03f), new(-0.26f, 0.34f), new(-0.31f, 0.87f),
                new(-0.96f, 1.53f), new(-0.28f, 1.56f), new(1.10f, 2.43f)
            };

            TerritoryPolygons[TerritoryIds.Alemanha] = new Vector2[]
            {
                new(1.26f, 1.60f), new(1.89f, 1.40f), new(2.14f, 0.26f),
                new(1.38f, -0.57f), new(1.10f, -1.78f), new(0.48f, -1.78f),
                new(0.34f, -1.07f), new(-0.59f, -1.58f), new(-1.63f, -1.61f),
                new(-2.33f, -0.70f), new(-0.92f, 1.19f), new(-0.95f, 2.09f),
                new(-0.64f, 2.22f), new(-0.40f, 1.43f)
            };

            TerritoryPolygons[TerritoryIds.Suecia] = new Vector2[]
            {
                new(1.86f, -0.98f), new(0.58f, -0.86f), new(0.83f, 0.02f),
                new(0.60f, 0.19f), new(0.06f, -0.52f), new(0.17f, -1.39f),
                new(-0.69f, -2.36f), new(-1.08f, -2.16f), new(-1.22f, -1.45f),
                new(-1.86f, -1.67f), new(-2.27f, -0.48f), new(-1.22f, 0.29f),
                new(-0.51f, 1.48f), new(0.88f, 2.14f), new(1.46f, 2.13f),
                new(1.83f, 1.85f)
            };

            TerritoryPolygons[TerritoryIds.Polonia] = new Vector2[]
            {
                new(1.09f, 1.88f), new(1.69f, 1.64f), new(2.01f, 0.30f),
                new(1.21f, -1.24f), new(0.83f, -1.37f), new(1.21f, -2.24f),
                new(0.49f, -2.14f), new(-0.02f, -0.67f), new(-0.77f, -0.24f),
                new(-0.74f, -0.65f), new(-0.49f, -0.61f), new(-0.12f, -1.19f),
                new(-0.68f, -1.21f), new(-0.66f, -1.82f), new(-1.32f, -2.11f),
                new(-1.59f, -1.74f), new(-1.11f, -1.83f), new(-0.84f, -1.43f),
                new(-1.81f, -0.39f), new(-2.19f, -0.38f), new(-2.19f, 0.87f),
                new(-0.93f, 1.12f), new(-0.16f, 1.61f), new(0.05f, 0.89f),
                new(0.67f, 0.89f)
            };

            TerritoryPolygons[TerritoryIds.RussiaEuropeia] = new Vector2[]
            {
                new(-2.41f, 4.14f), new(-2.30f, 1.15f), new(-3.21f, 1.02f),
                new(-2.89f, 0.31f), new(-3.32f, 0.51f), new(-3.56f, -0.02f),
                new(-3.49f, -0.40f), new(-2.86f, -0.61f), new(-2.61f, -1.75f),
                new(-3.38f, -2.66f), new(-2.62f, -3.03f), new(-2.32f, -4.17f),
                new(-1.81f, -3.79f), new(-1.25f, -4.37f), new(-0.93f, -4.04f),
                new(-1.16f, -3.75f), new(-0.31f, -3.50f), new(-0.70f, -3.98f),
                new(0.46f, -4.94f), new(0.35f, -5.38f), new(1.38f, -5.21f),
                new(1.66f, -4.25f), new(0.99f, -3.38f), new(1.39f, -2.77f),
                new(0.96f, -1.72f), new(1.27f, -1.21f), new(2.36f, -0.86f),
                new(2.85f, -1.01f), new(3.50f, -0.47f), new(3.52f, 0.38f),
                new(3.09f, 0.68f), new(3.27f, 4.02f), new(1.49f, 4.04f),
                new(0.34f, 3.40f), new(0.14f, 3.85f), new(-0.23f, 3.81f),
                new(-0.01f, 3.20f), new(-1.03f, 3.01f), new(-1.14f, 2.57f),
                new(-1.91f, 3.28f), new(-0.71f, 3.34f), new(-0.51f, 3.64f),
                new(-1.05f, 3.97f)
            };

            // África (6)
            TerritoryPolygons[TerritoryIds.Argelia] = new Vector2[]
            {
                new(1.57f, 2.70f), new(0.76f, 3.56f), new(-2.20f, 2.87f),
                new(-3.19f, 0.78f), new(-3.27f, -0.81f), new(-1.65f, -2.55f),
                new(0.47f, -2.38f), new(0.63f, -2.64f), new(1.39f, -2.72f),
                new(1.46f, -3.27f), new(2.27f, -3.27f), new(2.41f, -2.33f),
                new(3.63f, -1.47f), new(3.69f, -0.13f), new(2.84f, 0.53f),
                new(1.40f, 0.87f), new(1.14f, 1.93f)
            };

            TerritoryPolygons[TerritoryIds.Egito] = new Vector2[]
            {
                new(0.22f, -1.34f), new(-0.17f, -1.29f), new(-0.61f, -0.87f),
                new(-2.04f, -0.53f), new(-2.31f, 0.53f), new(-1.88f, 1.30f),
                new(-1.01f, 0.97f), new(-0.14f, 1.12f), new(1.56f, 0.71f),
                new(2.05f, 0.88f), new(2.09f, 0.25f), new(1.76f, 0.39f),
                new(2.51f, -0.96f), new(0.53f, -0.98f)
            };

            TerritoryPolygons[TerritoryIds.Sudao] = new Vector2[]
            {
                new(-2.22f, 0.82f), new(-2.19f, 2.35f), new(-1.88f, 2.71f),
                new(0.08f, 2.74f), new(1.31f, 0.59f), new(2.90f, 0.73f),
                new(2.49f, -0.35f), new(0.68f, -2.13f), new(0.78f, -3.20f),
                new(0.06f, -3.51f), new(-0.06f, -4.29f), new(-0.35f, -3.24f),
                new(-0.91f, -2.76f), new(-1.01f, -1.94f), new(-0.02f, -0.75f),
                new(-1.31f, -0.49f)
            };

            TerritoryPolygons[TerritoryIds.Congo] = new Vector2[]
            {
                new(1.43f, -1.40f), new(1.29f, -0.54f), new(2.28f, 0.66f),
                new(0.99f, 0.91f), new(0.08f, 2.21f), new(-1.14f, 1.37f),
                new(-1.16f, 0.44f), new(-2.22f, 0.55f), new(-2.51f, -0.07f),
                new(-1.82f, -0.75f), new(-0.82f, -0.92f), new(-0.72f, -1.26f),
                new(-0.09f, -1.16f), new(0.12f, -1.86f), new(1.11f, -2.20f),
                new(1.12f, -1.58f)
            };

            TerritoryPolygons[TerritoryIds.AfricaDoSul] = new Vector2[]
            {
                new(-2.20f, 2.87f), new(-1.20f, 2.69f), new(-1.10f, 2.36f),
                new(-0.47f, 2.45f), new(-0.26f, 1.76f), new(0.83f, 1.39f),
                new(0.74f, 2.03f), new(1.23f, 2.21f), new(1.86f, 0.72f),
                new(1.98f, 1.51f), new(2.85f, 1.84f), new(2.87f, 0.82f),
                new(2.10f, 0.10f), new(1.05f, -2.47f), new(-0.81f, -3.04f),
                new(-1.17f, -2.86f), new(-1.75f, -0.48f), new(-2.30f, 0.52f),
                new(-1.94f, 1.53f)
            };

            TerritoryPolygons[TerritoryIds.Madagascar] = new Vector2[]
            {
                new(0.71f, -0.19f), new(1.01f, 1.13f), new(1.17f, 1.02f),
                new(1.22f, 1.62f), new(1.03f, 1.93f), new(0.09f, 0.91f),
                new(-0.70f, 0.81f), new(-0.83f, 0.49f), new(-0.54f, 0.17f),
                new(-0.74f, 0.09f), new(-0.81f, -0.41f), new(-1.11f, -0.83f),
                new(-0.89f, -1.54f), new(-0.09f, -1.50f)
            };

            // Ásia (9)
            TerritoryPolygons[TerritoryIds.ArabiaSaudita] = new Vector2[]
            {
                new(-1.20f, 1.40f), new(0.40f, 1.60f), new(1.50f, 0.80f),
                new(1.60f, -0.50f), new(1.10f, -1.50f), new(-0.20f, -1.60f),
                new(-1.10f, -0.80f), new(-1.40f, 0.50f)
            };

            TerritoryPolygons[TerritoryIds.Ira] = new Vector2[]
            {
                new(-1.50f, 1.10f), new(0.10f, 1.40f), new(1.40f, 0.90f),
                new(1.60f, -0.20f), new(1.20f, -1.20f), new(-0.30f, -1.30f),
                new(-1.30f, -0.50f)
            };

            TerritoryPolygons[TerritoryIds.Cazaquistao] = new Vector2[]
            {
                new(2.64f, -0.15f), new(2.20f, 1.02f), new(1.23f, 1.19f),
                new(-0.14f, 2.42f), new(-0.89f, 1.96f), new(-1.38f, 2.11f),
                new(-2.47f, 1.76f), new(-2.78f, 1.25f), new(-2.31f, 0.17f),
                new(-1.70f, 0.42f), new(-1.91f, -0.51f), new(-1.49f, -0.84f),
                new(-1.08f, -2.13f), new(-0.48f, -2.13f), new(0.18f, -2.62f),
                new(1.17f, -1.88f), new(2.09f, -1.86f), new(1.86f, -0.91f)
            };

            TerritoryPolygons[TerritoryIds.RussiaAsiatica] = new Vector2[]
            {
                // Fronteira oeste com a Rússia Europeia (Montes Urais de norte a sul)
                new(-6.55f, 2.03f), new(-6.73f, -1.31f), new(-6.30f, -1.61f),
                new(-6.32f, -2.46f), new(-6.97f, -3.00f), new(-7.46f, -2.85f),
                // Fronteira sudoeste com o Cazaquistão (Petropavlovsk, Omsk, Pavlodar, Altai)
                new(-6.20f, -2.54f), new(-4.83f, -3.20f), new(-3.42f, -3.60f),
                new(-2.50f, -3.00f),
                // Fronteira sul com a Mongólia e Norte da China (Montes Sayans, Baikal, Selenga, Amur)
                new(-2.09f, -2.41f), new(-0.27f, -2.45f), new(0.44f, -2.15f),
                new(0.94f, -1.30f), new(1.52f, -1.25f), new(3.35f, -3.20f),
                new(4.00f, -4.00f),
                // Litoral do Pacífico (Vladivostok, Mar de Okhotsk, Península de Kamchatka, Chukotka)
                new(5.30f, -3.30f), new(6.00f, -1.60f), new(7.30f, 0.00f),
                new(8.30f, 1.70f), new(7.80f, 2.60f),
                // Litoral ártico setentrional autêntico (Chukotka, Sibéria Oriental, Delta do Lena, Taimir, Yamal)
                new(6.80f, 3.40f), new(5.60f, 3.20f), new(4.50f, 3.30f),
                new(3.40f, 3.60f), new(2.50f, 3.30f), new(1.60f, 3.00f),
                new(0.80f, 3.80f), new(0.00f, 4.60f), new(-1.00f, 3.90f),
                new(-1.80f, 3.20f), new(-2.80f, 3.60f), new(-3.60f, 2.80f),
                new(-4.50f, 3.70f), new(-5.60f, 2.90f)
            };



            TerritoryPolygons[TerritoryIds.Mongolia] = new Vector2[]
            {
                new(-2.79f, 1.19f), new(-2.59f, -0.56f), new(-1.62f, -1.47f),
                new(1.62f, -1.77f), new(1.97f, -1.47f), new(1.10f, -1.19f),
                new(1.53f, -0.68f), new(2.44f, -1.25f), new(2.52f, -1.76f),
                new(2.85f, -1.59f), new(2.95f, -1.18f), new(2.26f, -0.52f),
                new(2.65f, 0.29f), new(0.82f, 2.32f), new(0.24f, 2.26f),
                new(-0.02f, 1.06f), new(-0.26f, 1.35f), new(-0.97f, 1.05f)
            };

            TerritoryPolygons[TerritoryIds.China] = new Vector2[]
            {
                new(3.15f, 0.91f), new(3.70f, 0.11f), new(3.83f, -1.22f),
                new(2.46f, -3.09f), new(1.68f, -2.27f), new(0.95f, -2.71f),
                new(0.12f, -1.94f), new(-0.17f, -2.15f), new(-0.37f, -1.71f),
                new(-1.15f, -1.80f), new(-1.26f, -1.48f), new(-2.96f, -0.93f),
                new(-2.82f, 0.13f), new(-3.39f, 0.38f), new(-3.68f, 0.22f),
                new(-3.98f, 0.92f), new(-3.23f, 1.73f), new(-3.27f, 2.56f),
                new(-2.50f, 3.54f), new(-1.64f, 3.22f), new(-1.69f, 2.78f),
                new(-1.19f, 2.65f), new(-0.02f, 1.23f)
            };

            TerritoryPolygons[TerritoryIds.India] = new Vector2[]
            {
                new(-0.31f, 3.03f), new(-2.12f, 2.43f), new(-2.56f, 2.01f),
                new(-2.64f, 1.38f), new(-2.04f, -0.23f), new(-1.67f, -0.19f),
                new(-1.01f, -0.74f), new(-0.97f, -1.17f), new(-0.60f, -1.09f),
                new(-0.65f, -1.96f), new(0.55f, -4.17f), new(1.01f, -3.63f),
                new(1.67f, -0.91f), new(2.30f, -0.55f), new(2.91f, 0.50f),
                new(2.71f, 0.94f), new(1.93f, 0.85f), new(1.82f, 1.17f),
                new(0.12f, 1.72f), new(0.26f, 2.78f)
            };

            TerritoryPolygons[TerritoryIds.Vietna] = new Vector2[]
            {
                new(1.11f, 0.98f), new(0.89f, 1.12f), new(0.68f, 0.51f),
                new(1.33f, 0.07f), new(1.65f, -1.29f), new(1.11f, -1.94f),
                new(0.88f, -1.40f), new(0.34f, -1.16f), new(0.18f, -1.41f),
                new(0.61f, -2.32f), new(0.32f, -2.39f), new(-0.12f, -1.91f),
                new(-0.36f, -0.68f), new(-1.10f, -0.42f), new(-1.88f, 0.90f),
                new(-1.02f, 1.97f), new(-0.16f, 1.21f), new(0.78f, 1.56f)
            };

            TerritoryPolygons[TerritoryIds.Japao] = new Vector2[]
            {
                new(-1.25f, -2.40f), new(-1.56f, -2.02f), new(0.02f, 0.11f),
                new(-0.30f, 1.37f), new(-0.11f, 1.93f), new(-0.45f, 2.48f),
                new(-0.01f, 2.20f), new(0.85f, 2.23f), new(1.04f, 1.95f),
                new(0.68f, 1.83f), new(0.59f, 1.40f), new(-0.03f, 1.32f),
                new(0.73f, 0.70f), new(0.98f, -0.65f), new(-0.08f, -1.51f),
                new(-0.72f, -1.40f), new(-0.59f, -1.83f)
            };

            // Oceania (5)
            TerritoryPolygons[TerritoryIds.NovaGuine] = new Vector2[]
            {
                new(-0.86f, 0.57f), new(-0.72f, 1.08f), new(-0.26f, 1.17f),
                new(1.14f, 0.37f), new(1.65f, -0.25f), new(1.42f, -0.41f),
                new(1.82f, -1.25f), new(0.70f, -0.80f), new(0.36f, -1.10f),
                new(-0.72f, -0.84f), new(-0.92f, 0.11f), new(-1.85f, 0.16f),
                new(-1.49f, 0.45f), new(-1.91f, 1.11f), new(-1.08f, 1.09f)
            };

            TerritoryPolygons[TerritoryIds.Australia] = new Vector2[]
            {
                new(1.33f, -3.06f), new(2.75f, -2.71f), new(3.31f, -1.27f),
                new(3.62f, -1.15f), new(3.75f, -0.03f), new(3.01f, 1.36f),
                new(2.48f, 1.52f), new(2.36f, 2.51f), new(1.97f, 2.46f),
                new(1.80f, 3.23f), new(1.51f, 2.07f), new(1.22f, 2.00f),
                new(0.52f, 2.39f), new(0.81f, 3.00f), new(-0.00f, 3.15f),
                new(-0.64f, 2.86f), new(-0.80f, 2.39f), new(-1.12f, 2.56f),
                new(-2.78f, 1.22f), new(-3.92f, 0.78f), new(-4.12f, -0.26f),
                new(-3.58f, -1.47f), new(-3.84f, -2.13f), new(-3.31f, -2.40f),
                new(-2.28f, -1.89f), new(-1.60f, -1.94f), new(-1.49f, -1.60f),
                new(-0.77f, -1.35f), new(-0.13f, -1.43f), new(0.23f, -2.01f),
                new(0.67f, -1.47f), new(0.46f, -2.16f), new(0.75f, -2.01f)
            };

            TerritoryPolygons[TerritoryIds.NovaZelandia] = new Vector2[]
            {
                new(0.80f, 2.10f), new(1.10f, 1.40f), new(0.60f, 0.20f),
                new(0.10f, -0.80f), new(-0.50f, -2.00f), new(-0.90f, -1.70f),
                new(-0.30f, 0.10f), new(0.20f, 1.80f)
            };

            TerritoryPolygons[TerritoryIds.IlhasSalomao] = new Vector2[]
            {
                new(1.20f, 0.80f), new(0.80f, 0.90f), new(-0.20f, 0.40f),
                new(-1.10f, -0.20f), new(-0.90f, -0.60f), new(0.10f, -0.30f),
                new(1.10f, 0.30f)
            };

            TerritoryPolygons[TerritoryIds.Fiji] = new Vector2[]
            {
                new(0.60f, 0.80f), new(0.90f, 0.20f), new(0.70f, -0.60f),
                new(-0.10f, -0.80f), new(-0.80f, -0.20f), new(-0.60f, 0.60f),
                new(0.10f, 0.90f)
            };

        }
    }

    /// <summary>
    /// Utilitário central para resolução de shaders compatíveis no cliente 2D.
    /// Detecta dinamicamente a pipeline ativa (Built-in ou URP) para nunca gerar materiais com shader rosa/magenta.
    /// </summary>
    public static class ShaderUtils2D
    {
        public static Shader GetTextureShader()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                var urp = Shader.Find("Universal Render Pipeline/Unlit");
                if (urp != null) return urp;
            }

            return Shader.Find("Unlit/Texture")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default")
                ?? Shader.Find("Mobile/Unlit (Supports Lightmap)")
                ?? Shader.Find("Legacy Shaders/Diffuse");
        }

        public static Shader GetColorShader()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                var urp = Shader.Find("Universal Render Pipeline/Unlit");
                if (urp != null) return urp;
            }

            return Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("UI/Default");
        }
    }
}
