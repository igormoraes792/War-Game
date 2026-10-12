#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Data;
using War.Core.Models;

namespace War2D.Presentation.Board
{
    /// <summary>
    /// Renderiza as conexões e rotas táticas entre os 42 territórios no espaço 2D.
    /// Distingue rotas terrestres sutis e passagens marítimas (pontilhadas/azul ciano náutico).
    /// Suporta highlight de vizinhos (dourado para aliados, vermelho para alvos de ataque).
    /// </summary>
    [DisallowMultipleComponent]
    public class ConnectionLines2DRenderer : MonoBehaviour
    {
        [Header("Estilo das Conexões Terrestres")]
        [SerializeField] private Color terrestrialColor = new Color(0.65f, 0.75f, 0.88f, 0.40f);
        [SerializeField] private float terrestrialWidth = 0.07f;

        [Header("Estilo das Passagens Marítimas")]
        [SerializeField] private Color seaRouteColor = new Color(0.28f, 0.82f, 0.98f, 0.70f);
        [SerializeField] private float seaRouteWidth = 0.09f;

        [Header("Cores de Destaque")]
        [SerializeField] private Color highlightAllyColor = new Color(1.0f, 0.85f, 0.25f, 0.95f);
        [SerializeField] private Color highlightEnemyColor = new Color(1.0f, 0.25f, 0.20f, 0.95f);

        private readonly List<Connection2DEntry> _connections = new();
        private readonly Dictionary<string, List<Connection2DEntry>> _byTerritory = new(StringComparer.OrdinalIgnoreCase);

        private class Connection2DEntry
        {
            public string TerritoryA = string.Empty;
            public string TerritoryB = string.Empty;
            public bool IsSeaRoute;
            public LineRenderer Line = null!;
            public Color DefaultColor;
            public float DefaultWidth;
        }

        public void BuildConnections(IReadOnlyDictionary<string, Vector2> positions)
        {
            ClearLines();

            var defs = ClassicMapData.GetAllDefinitions();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var sharedMat = new Material(lineShader) { name = "War2D_Line_Mat" };

            foreach (var def in defs)
            {
                var idA = def.Id;
                if (!positions.TryGetValue(idA, out var posA)) continue;

                foreach (var idB in def.Neighbors)
                {
                    if (!positions.TryGetValue(idB, out var posB)) continue;

                    var key = string.Compare(idA, idB, StringComparison.OrdinalIgnoreCase) < 0 ? $"{idA}_{idB}" : $"{idB}_{idA}";
                    if (processed.Contains(key)) continue;
                    processed.Add(key);

                    bool isTransPacific = (idA == TerritoryIds.Alasca && idB == TerritoryIds.Vladivostok) ||
                                          (idA == TerritoryIds.Vladivostok && idB == TerritoryIds.Alasca);

                    if (isTransPacific)
                    {
                        CreateTransPacificLines(idA, posA, idB, posB, sharedMat);
                        continue;
                    }

                    bool isSea = IsSeaRoute(idA, idB);
                    var lineGo = new GameObject($"Line2D_{key}");
                    lineGo.transform.SetParent(transform, false);

                    var lr = lineGo.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.positionCount = 2;
                    lr.sharedMaterial = sharedMat;

                    float width = isSea ? seaRouteWidth : terrestrialWidth;
                    lr.startWidth = width;
                    lr.endWidth = width;

                    Color col = isSea ? seaRouteColor : terrestrialColor;
                    lr.startColor = col;
                    lr.endColor = col;

                    lr.SetPosition(0, new Vector3(posA.x, posA.y, -0.05f));
                    lr.SetPosition(1, new Vector3(posB.x, posB.y, -0.05f));

                    var entry = new Connection2DEntry
                    {
                        TerritoryA = idA,
                        TerritoryB = idB,
                        IsSeaRoute = isSea,
                        Line = lr,
                        DefaultColor = col,
                        DefaultWidth = width
                    };

                    _connections.Add(entry);
                    AddIndex(idA, entry);
                    AddIndex(idB, entry);
                }
            }
        }

        private void CreateTransPacificLines(string idA, Vector2 posA, string idB, Vector2 posB, Material mat)
        {
            Vector2 alascaPos = idA == TerritoryIds.Alasca ? posA : posB;
            Vector2 vladiPos = idA == TerritoryIds.Vladivostok ? posA : posB;

            // Alasca para margem esquerda do mapa
            var lineA = new GameObject("Line2D_Pacific_Alasca").AddComponent<LineRenderer>();
            lineA.transform.SetParent(transform, false);
            lineA.sharedMaterial = mat;
            lineA.useWorldSpace = true;
            lineA.positionCount = 2;
            lineA.startWidth = seaRouteWidth;
            lineA.endWidth = seaRouteWidth;
            lineA.startColor = seaRouteColor;
            lineA.endColor = seaRouteColor;
            lineA.SetPosition(0, new Vector3(alascaPos.x, alascaPos.y, -0.05f));
            lineA.SetPosition(1, new Vector3(-25.5f, alascaPos.y, -0.05f));

            // Vladivostok para margem direita do mapa
            var lineB = new GameObject("Line2D_Pacific_Vladivostok").AddComponent<LineRenderer>();
            lineB.transform.SetParent(transform, false);
            lineB.sharedMaterial = mat;
            lineB.useWorldSpace = true;
            lineB.positionCount = 2;
            lineB.startWidth = seaRouteWidth;
            lineB.endWidth = seaRouteWidth;
            lineB.startColor = seaRouteColor;
            lineB.endColor = seaRouteColor;
            lineB.SetPosition(0, new Vector3(vladiPos.x, vladiPos.y, -0.05f));
            lineB.SetPosition(1, new Vector3(25.5f, vladiPos.y, -0.05f));
        }

        public void HighlightConnections(string territoryId, Func<string, bool> isAllyPredicate)
        {
            ResetAllHighlights();
            if (!_byTerritory.TryGetValue(territoryId, out var list)) return;

            foreach (var entry in list)
            {
                var otherId = entry.TerritoryA.Equals(territoryId, StringComparison.OrdinalIgnoreCase)
                    ? entry.TerritoryB
                    : entry.TerritoryA;

                bool isAlly = isAllyPredicate(otherId);
                Color hlColor = isAlly ? highlightAllyColor : highlightEnemyColor;

                entry.Line.startColor = hlColor;
                entry.Line.endColor = hlColor;
                entry.Line.startWidth = entry.DefaultWidth * 1.5f;
                entry.Line.endWidth = entry.DefaultWidth * 1.5f;
            }
        }

        public void ResetAllHighlights()
        {
            foreach (var entry in _connections)
            {
                entry.Line.startColor = entry.DefaultColor;
                entry.Line.endColor = entry.DefaultColor;
                entry.Line.startWidth = entry.DefaultWidth;
                entry.Line.endWidth = entry.DefaultWidth;
            }
        }

        private void AddIndex(string territoryId, Connection2DEntry entry)
        {
            if (!_byTerritory.TryGetValue(territoryId, out var list))
            {
                list = new List<Connection2DEntry>();
                _byTerritory[territoryId] = list;
            }
            list.Add(entry);
        }

        private static bool IsSeaRoute(string a, string b)
        {
            return (a == TerritoryIds.Brasil && b == TerritoryIds.Nigeria) ||
                   (a == TerritoryIds.Nigeria && b == TerritoryIds.Brasil) ||
                   (a == TerritoryIds.Groenlandia && b == TerritoryIds.Islandia) ||
                   (a == TerritoryIds.Islandia && b == TerritoryIds.Groenlandia) ||
                   (a == TerritoryIds.Inglaterra && b == TerritoryIds.Islandia) ||
                   (a == TerritoryIds.Islandia && b == TerritoryIds.Inglaterra) ||
                   (a == TerritoryIds.Madagascar && (b == TerritoryIds.Sudao || b == TerritoryIds.AfricaDoSul)) ||
                   ((a == TerritoryIds.Sudao || a == TerritoryIds.AfricaDoSul) && b == TerritoryIds.Madagascar) ||
                   (a == TerritoryIds.Australia && (b == TerritoryIds.NovaGuine || b == TerritoryIds.Borneo)) ||
                   ((a == TerritoryIds.NovaGuine || a == TerritoryIds.Borneo) && b == TerritoryIds.Australia);
        }

        private void ClearLines()
        {
            foreach (var c in _connections)
            {
                if (c.Line != null) DestroyImmediate(c.Line.gameObject);
            }
            _connections.Clear();
            _byTerritory.Clear();
        }
    }
}
