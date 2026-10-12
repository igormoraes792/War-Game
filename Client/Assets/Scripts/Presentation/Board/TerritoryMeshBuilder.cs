#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Data;

namespace War.Presentation.Board
{
    /// <summary>
    /// Utilitário gerador de malhas 3D volumétricas para os territórios de WAR.
    /// Transforma polígonos de fronteira em peças físicas tridimensionais (estilo quebra-cabeça 3D de luxo),
    /// com topo plano, laterais recortadas e chanfro/extrusão suave com normais nítidas.
    /// </summary>
    public static class TerritoryMeshBuilder
    {
        public const float DefaultExtrusionHeight = 0.28f;

        private static readonly Dictionary<string, Vector2[]> TerritoryPolygons = new(StringComparer.OrdinalIgnoreCase);

        static TerritoryMeshBuilder()
        {
            RegisterAllPolygons();
        }

        /// <summary>
        /// Gera uma malha 3D completa (topo, fundo e paredes laterais) para o território especificado.
        /// </summary>
        public static Mesh CreateTerritoryMesh(string territoryId, float height = DefaultExtrusionHeight)
        {
            var poly = GetTerritoryPolygon(territoryId);
            int n = poly.Length;

            var mesh = new Mesh
            {
                name = $"Mesh_Territory_{territoryId}"
            };

            // Determina a caixa delimitadora do polígono para mapeamento UV proporcional
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

            // 1. Superfície Superior (Top Cap - Y = height)
            int topOffset = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(poly[i].x, height, poly[i].y));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2((poly[i].x - minX) / sizeX, (poly[i].y - minY) / sizeY));
            }

            var topTris = TriangulatePolygon(poly);
            for (int i = 0; i < topTris.Count; i += 3)
            {
                int i0 = topTris[i];
                int i1 = topTris[i + 1];
                int i2 = topTris[i + 2];

                Vector3 v0 = vertices[topOffset + i0];
                Vector3 v1 = vertices[topOffset + i1];
                Vector3 v2 = vertices[topOffset + i2];
                Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);

                if (cross.y >= 0f)
                {
                    triangles.Add(topOffset + i0);
                    triangles.Add(topOffset + i1);
                    triangles.Add(topOffset + i2);
                }
                else
                {
                    triangles.Add(topOffset + i0);
                    triangles.Add(topOffset + i2);
                    triangles.Add(topOffset + i1);
                }
            }

            // 2. Superfície Inferior (Bottom Cap - Y = 0)
            int botOffset = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(poly[i].x, 0f, poly[i].y));
                normals.Add(Vector3.down);
                uvs.Add(new Vector2((poly[i].x - minX) / sizeX, (poly[i].y - minY) / sizeY));
            }

            for (int i = 0; i < topTris.Count; i += 3)
            {
                int i0 = topTris[i];
                int i1 = topTris[i + 1];
                int i2 = topTris[i + 2];

                Vector3 v0 = vertices[topOffset + i0];
                Vector3 v1 = vertices[topOffset + i1];
                Vector3 v2 = vertices[topOffset + i2];
                Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);

                // Inverte enrolamento para face voltada para baixo (-Y)
                if (cross.y >= 0f)
                {
                    triangles.Add(botOffset + i0);
                    triangles.Add(botOffset + i2);
                    triangles.Add(botOffset + i1);
                }
                else
                {
                    triangles.Add(botOffset + i0);
                    triangles.Add(botOffset + i1);
                    triangles.Add(botOffset + i2);
                }
            }

            // 3. Paredes Laterais Recortadas (Extrusão 3D volumétrica)
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area += (a.x * b.y - b.x * a.y);
            }
            var ccwPoly = new Vector2[n];
            if (area < 0f)
            {
                for (int i = 0; i < n; i++) ccwPoly[i] = poly[n - 1 - i];
            }
            else
            {
                Array.Copy(poly, ccwPoly, n);
            }

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector2 pA = ccwPoly[i];
                Vector2 pB = ccwPoly[next];

                Vector2 edgeDir = (pB - pA).normalized;
                Vector3 wallNormal = new Vector3(edgeDir.y, 0f, -edgeDir.x).normalized;

                int sideBase = vertices.Count;
                vertices.Add(new Vector3(pA.x, 0f, pA.y));
                vertices.Add(new Vector3(pB.x, 0f, pB.y));
                vertices.Add(new Vector3(pB.x, height, pB.y));
                vertices.Add(new Vector3(pA.x, height, pA.y));

                normals.Add(wallNormal);
                normals.Add(wallNormal);
                normals.Add(wallNormal);
                normals.Add(wallNormal);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));

                triangles.Add(sideBase + 0);
                triangles.Add(sideBase + 3);
                triangles.Add(sideBase + 2);

                triangles.Add(sideBase + 0);
                triangles.Add(sideBase + 2);
                triangles.Add(sideBase + 1);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

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
        /// Registra os 42 polígonos representativos das fronteiras clássicas do WAR.
        /// </summary>
        private static void RegisterAllPolygons()
        {
            // América do Sul (4)
            TerritoryPolygons[TerritoryIds.Venezuela] = new Vector2[]
            {
                new(-1.6f, 0.9f), new(-0.2f, 1.4f), new(1.8f, 1.1f),
                new(1.7f, -0.6f), new(0.2f, -1.1f), new(-1.4f, -0.7f)
            };

            TerritoryPolygons[TerritoryIds.Peru] = new Vector2[]
            {
                new(-1.2f, 1.8f), new(0.4f, 1.4f), new(0.8f, -0.2f),
                new(0.2f, -1.8f), new(-0.8f, -1.9f), new(-1.3f, -0.2f)
            };

            TerritoryPolygons[TerritoryIds.Brasil] = new Vector2[]
            {
                new(-1.9f, 1.6f), new(0.6f, 2.0f), new(2.4f, 1.1f),
                new(2.6f, -0.5f), new(1.6f, -2.1f), new(-0.2f, -2.0f),
                new(-1.4f, -0.8f), new(-2.0f, 0.4f)
            };

            TerritoryPolygons[TerritoryIds.Argentina] = new Vector2[]
            {
                new(-1.2f, 1.9f), new(1.2f, 1.9f), new(0.9f, 0.2f),
                new(0.6f, -1.8f), new(0.1f, -2.7f), new(-0.6f, -2.5f),
                new(-0.9f, -0.4f)
            };

            // América do Norte (9)
            TerritoryPolygons[TerritoryIds.Alasca] = new Vector2[]
            {
                new(-2.1f, 0.2f), new(-0.8f, 1.4f), new(1.5f, 1.2f),
                new(1.6f, -0.9f), new(0.2f, -1.3f), new(-1.2f, -0.8f)
            };

            TerritoryPolygons[TerritoryIds.Mackenzie] = new Vector2[]
            {
                new(-1.8f, 1.3f), new(1.6f, 1.4f), new(1.7f, -0.7f),
                new(-0.2f, -1.4f), new(-1.7f, -1.0f)
            };

            TerritoryPolygons[TerritoryIds.Groenlandia] = new Vector2[]
            {
                new(-1.8f, 1.4f), new(0.4f, 1.8f), new(1.9f, 1.0f),
                new(1.4f, -0.8f), new(0.1f, -1.8f), new(-1.2f, -1.2f)
            };

            TerritoryPolygons[TerritoryIds.Vancouver] = new Vector2[]
            {
                new(-1.6f, 1.4f), new(1.4f, 1.2f), new(1.3f, -0.8f),
                new(-0.6f, -1.5f), new(-1.7f, -0.4f)
            };

            TerritoryPolygons[TerritoryIds.Ottawa] = new Vector2[]
            {
                new(-1.8f, 1.3f), new(1.5f, 1.4f), new(1.8f, -0.6f),
                new(0.4f, -1.5f), new(-1.5f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.Labrador] = new Vector2[]
            {
                new(-1.4f, 1.3f), new(1.5f, 0.9f), new(1.4f, -0.9f),
                new(-0.4f, -1.3f), new(-1.3f, -0.2f)
            };

            TerritoryPolygons[TerritoryIds.California] = new Vector2[]
            {
                new(-1.4f, 1.4f), new(1.2f, 1.1f), new(1.4f, -0.7f),
                new(0.2f, -1.5f), new(-1.3f, -1.2f)
            };

            TerritoryPolygons[TerritoryIds.NovaYork] = new Vector2[]
            {
                new(-1.6f, 1.2f), new(1.5f, 1.3f), new(1.7f, -0.5f),
                new(0.4f, -1.4f), new(-1.4f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.Mexico] = new Vector2[]
            {
                new(-1.6f, 1.3f), new(1.5f, 1.1f), new(1.1f, -0.4f),
                new(0.3f, -1.6f), new(-0.7f, -1.7f), new(-1.4f, -0.2f)
            };

            // Europa (7)
            TerritoryPolygons[TerritoryIds.Islandia] = new Vector2[]
            {
                new(-1.1f, 0.2f), new(0.0f, 1.0f), new(1.1f, 0.3f),
                new(0.6f, -0.8f), new(-0.7f, -0.8f)
            };

            TerritoryPolygons[TerritoryIds.Inglaterra] = new Vector2[]
            {
                new(-0.8f, 1.3f), new(0.7f, 1.2f), new(0.9f, -0.4f),
                new(0.2f, -1.3f), new(-0.7f, -0.9f)
            };

            TerritoryPolygons[TerritoryIds.Franca] = new Vector2[]
            {
                new(-1.2f, 1.2f), new(0.8f, 1.3f), new(1.3f, 0.1f),
                new(0.7f, -1.2f), new(-0.9f, -1.3f), new(-1.4f, -0.2f)
            };

            TerritoryPolygons[TerritoryIds.Alemanha] = new Vector2[]
            {
                new(-1.2f, 1.2f), new(1.2f, 1.3f), new(1.3f, -0.7f),
                new(-0.2f, -1.3f), new(-1.1f, -0.8f)
            };

            TerritoryPolygons[TerritoryIds.Suecia] = new Vector2[]
            {
                new(-0.9f, 1.6f), new(0.8f, 1.5f), new(1.0f, -0.4f),
                new(0.4f, -1.7f), new(-0.6f, -1.6f), new(-0.9f, 0.1f)
            };

            TerritoryPolygons[TerritoryIds.Polonia] = new Vector2[]
            {
                new(-1.2f, 1.3f), new(1.3f, 1.2f), new(1.4f, -0.6f),
                new(0.2f, -1.4f), new(-1.1f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.Moscou] = new Vector2[]
            {
                new(-1.7f, 1.4f), new(1.5f, 1.5f), new(1.9f, -0.4f),
                new(0.6f, -1.5f), new(-1.2f, -1.4f), new(-1.8f, -0.2f)
            };

            // África (6)
            TerritoryPolygons[TerritoryIds.Nigeria] = new Vector2[]
            {
                new(-1.8f, 1.4f), new(1.2f, 1.5f), new(1.6f, -0.3f),
                new(0.8f, -1.5f), new(-1.1f, -1.6f), new(-1.9f, -0.3f)
            };

            TerritoryPolygons[TerritoryIds.Egito] = new Vector2[]
            {
                new(-1.3f, 1.3f), new(1.4f, 1.2f), new(1.3f, -0.7f),
                new(-0.3f, -1.4f), new(-1.4f, -0.8f)
            };

            TerritoryPolygons[TerritoryIds.Sudao] = new Vector2[]
            {
                new(-1.4f, 1.4f), new(1.5f, 1.1f), new(1.7f, -0.5f),
                new(0.4f, -1.5f), new(-1.3f, -1.2f)
            };

            TerritoryPolygons[TerritoryIds.Congo] = new Vector2[]
            {
                new(-1.5f, 1.2f), new(1.4f, 1.3f), new(1.6f, -0.6f),
                new(-0.1f, -1.5f), new(-1.4f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.AfricaDoSul] = new Vector2[]
            {
                new(-1.3f, 1.4f), new(1.4f, 1.3f), new(1.1f, -0.4f),
                new(0.2f, -1.6f), new(-0.9f, -1.5f), new(-1.4f, -0.3f)
            };

            TerritoryPolygons[TerritoryIds.Madagascar] = new Vector2[]
            {
                new(-0.7f, 1.4f), new(0.7f, 1.2f), new(0.8f, -0.6f),
                new(0.2f, -1.5f), new(-0.6f, -1.2f)
            };

            // Ásia (12)
            TerritoryPolygons[TerritoryIds.OrienteMedio] = new Vector2[]
            {
                new(-1.4f, 1.3f), new(1.3f, 1.1f), new(1.4f, -0.4f),
                new(0.7f, -1.6f), new(-0.6f, -1.5f), new(-1.2f, -0.4f)
            };

            TerritoryPolygons[TerritoryIds.Aral] = new Vector2[]
            {
                new(-1.4f, 1.3f), new(1.4f, 1.4f), new(1.5f, -0.6f),
                new(0.1f, -1.4f), new(-1.3f, -1.0f)
            };

            TerritoryPolygons[TerritoryIds.Omsk] = new Vector2[]
            {
                new(-1.5f, 1.3f), new(1.4f, 1.4f), new(1.6f, -0.5f),
                new(0.2f, -1.5f), new(-1.4f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.Dudinka] = new Vector2[]
            {
                new(-1.6f, 1.3f), new(1.5f, 1.4f), new(1.7f, -0.5f),
                new(0.1f, -1.4f), new(-1.5f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.Siberia] = new Vector2[]
            {
                new(-1.7f, 1.3f), new(1.6f, 1.4f), new(1.8f, -0.4f),
                new(0.3f, -1.5f), new(-1.4f, -1.2f)
            };

            TerritoryPolygons[TerritoryIds.Tchita] = new Vector2[]
            {
                new(-1.5f, 1.3f), new(1.4f, 1.4f), new(1.6f, -0.6f),
                new(0.2f, -1.4f), new(-1.4f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.Mongolia] = new Vector2[]
            {
                new(-1.5f, 1.2f), new(1.4f, 1.3f), new(1.6f, -0.5f),
                new(0.1f, -1.4f), new(-1.4f, -1.0f)
            };

            TerritoryPolygons[TerritoryIds.China] = new Vector2[]
            {
                new(-1.9f, 1.5f), new(1.6f, 1.5f), new(2.2f, 0.2f),
                new(1.8f, -1.4f), new(-0.3f, -1.6f), new(-1.6f, -1.2f)
            };

            TerritoryPolygons[TerritoryIds.Vladivostok] = new Vector2[]
            {
                new(-1.3f, 1.6f), new(1.1f, 1.5f), new(1.5f, -0.2f),
                new(0.9f, -1.6f), new(-0.6f, -1.5f), new(-1.4f, -0.3f)
            };

            TerritoryPolygons[TerritoryIds.Japao] = new Vector2[]
            {
                new(-0.8f, 1.5f), new(0.7f, 1.3f), new(0.9f, -0.3f),
                new(0.3f, -1.6f), new(-0.5f, -1.3f), new(-0.7f, 0.1f)
            };

            TerritoryPolygons[TerritoryIds.India] = new Vector2[]
            {
                new(-1.5f, 1.4f), new(1.4f, 1.3f), new(1.1f, 0.2f),
                new(0.2f, -1.8f), new(-0.6f, -1.7f), new(-1.3f, 0.1f)
            };

            TerritoryPolygons[TerritoryIds.Vietna] = new Vector2[]
            {
                new(-1.2f, 1.3f), new(1.2f, 1.1f), new(1.1f, -0.4f),
                new(0.3f, -1.7f), new(-0.7f, -1.5f), new(-1.2f, -0.2f)
            };

            // Oceania (4)
            TerritoryPolygons[TerritoryIds.Sumatra] = new Vector2[]
            {
                new(-1.4f, 1.1f), new(0.2f, 1.3f), new(1.4f, 0.2f),
                new(1.2f, -1.0f), new(-0.4f, -1.2f), new(-1.5f, -0.1f)
            };

            TerritoryPolygons[TerritoryIds.Borneo] = new Vector2[]
            {
                new(-1.2f, 1.2f), new(1.2f, 1.3f), new(1.4f, -0.3f),
                new(0.4f, -1.3f), new(-1.1f, -1.1f)
            };

            TerritoryPolygons[TerritoryIds.NovaGuine] = new Vector2[]
            {
                new(-1.6f, 0.8f), new(0.5f, 1.1f), new(1.7f, 0.6f),
                new(1.4f, -0.7f), new(-0.3f, -0.9f), new(-1.5f, -0.4f)
            };

            TerritoryPolygons[TerritoryIds.Australia] = new Vector2[]
            {
                new(-2.2f, 1.5f), new(0.3f, 1.9f), new(2.2f, 1.4f),
                new(2.4f, -0.8f), new(1.2f, -2.1f), new(-1.2f, -2.0f),
                new(-2.3f, -0.6f)
            };
        }
    }
}
