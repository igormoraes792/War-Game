#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Data;
using War.Core.Models;

namespace War.Presentation.Board
{
    /// <summary>
    /// Gerenciador e desenhista das linhas táticas de conexão entre os territórios do tabuleiro.
    /// Distingue rotas terrestres (cinza suave) e passagens marítimas (pontilhadas/azul ciano náutico).
    /// Suporta destaque dinâmico quando um território é selecionado (dourado para aliados, vermelho para alvos inimigos).
    /// </summary>
    [DisallowMultipleComponent]
    public class ConnectionLinesRenderer : MonoBehaviour
    {
        [Header("Estilo das Conexões Terrestres")]
        [SerializeField] private Color terrestrialColor = new Color(0.70f, 0.76f, 0.84f, 0.45f);
        [SerializeField] private float terrestrialWidth = 0.12f;

        [Header("Estilo das Passagens Marítimas")]
        [SerializeField] private Color seaRouteColor = new Color(0.25f, 0.75f, 0.95f, 0.75f);
        [SerializeField] private float seaRouteWidth = 0.15f;
        [SerializeField] private float seaDottedTileSize = 1.2f;

        [Header("Cores de Destaque")]
        [SerializeField] private Color highlightAllyColor = new Color(1.0f, 0.85f, 0.20f, 0.95f);
        [SerializeField] private Color highlightEnemyColor = new Color(1.0f, 0.25f, 0.20f, 0.95f);
        [SerializeField] private float highlightWidthMultiplier = 1.5f;

        [Header("Altura Y sobre a mesa")]
        [SerializeField] private float linesHeightY = 0.35f;

        // Materiais cacheados
        private Material? _terrestrialMaterial;
        private Material? _seaRouteMaterial;
        private Material? _highlightAllyMaterial;
        private Material? _highlightEnemyMaterial;
        private Texture2D? _dottedTexture;

        private readonly List<ConnectionEntry> _connections = new();
        private readonly Dictionary<string, List<ConnectionEntry>> _connectionsByTerritory = new(StringComparer.OrdinalIgnoreCase);

        private class ConnectionEntry
        {
            public string TerritoryA = string.Empty;
            public string TerritoryB = string.Empty;
            public bool IsSeaRoute;
            public LineRenderer LineRenderer = null!;
            public Color DefaultColor;
            public float DefaultWidth;
            public Material DefaultMaterial = null!;
            public bool IsSpecialWrap; // Ex: Alasca <-> Vladivostok
        }

        private void Awake()
        {
            EnsureMaterials();
        }

        private void OnDestroy()
        {
            if (_dottedTexture != null)
            {
                DestroyImmediate(_dottedTexture);
            }
        }

        /// <summary>
        /// Reconstrói todas as linhas de conexão a partir do grafo de adjacências de ClassicMapData
        /// e do dicionário de posições de cada território.
        /// </summary>
        public void BuildConnectionsFromMapData(IReadOnlyDictionary<string, Vector3> positions)
        {
            EnsureMaterials();
            ClearAllLines();

            var definitions = ClassicMapData.GetAllDefinitions();
            var processedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in definitions)
            {
                var idA = def.Id;
                if (!positions.TryGetValue(idA, out var posA)) continue;

                foreach (var idB in def.Neighbors)
                {
                    if (!positions.TryGetValue(idB, out var posB)) continue;

                    var pairKey = MakePairKey(idA, idB);
                    if (processedPairs.Contains(pairKey)) continue;
                    processedPairs.Add(pairKey);

                    bool isSea = IsSeaOrIntercontinentalRoute(idA, idB);
                    bool isTransPacific = IsTransPacificConnection(idA, idB);

                    if (isTransPacific)
                    {
                        CreateTransPacificLines(idA, posA, idB, posB);
                    }
                    else
                    {
                        CreateStandardLine(idA, posA, idB, posB, isSea);
                    }
                }
            }
        }

        /// <summary>
        /// Cria a linha padrão entre dois territórios adjacentes.
        /// </summary>
        private void CreateStandardLine(string idA, Vector3 posA, string idB, Vector3 posB, bool isSea)
        {
            var lineObj = new GameObject($"Line_{idA}_{idB}");
            lineObj.transform.SetParent(transform, false);

            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = isSea ? 5 : 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float width = isSea ? seaRouteWidth : terrestrialWidth;
            lr.startWidth = width;
            lr.endWidth = width;

            var defaultMat = isSea ? _seaRouteMaterial! : _terrestrialMaterial!;
            lr.sharedMaterial = defaultMat;

            var color = isSea ? seaRouteColor : terrestrialColor;
            lr.startColor = color;
            lr.endColor = color;

            var startPoint = new Vector3(posA.x, linesHeightY, posA.z);
            var endPoint = new Vector3(posB.x, linesHeightY, posB.z);
            float distance = Vector3.Distance(startPoint, endPoint);

            if (isSea)
            {
                // Rota marítima com leve arco estético
                lr.textureMode = LineTextureMode.Tile;

                var midPoint = (startPoint + endPoint) * 0.5f;
                // Leve arqueamento suave para a lateral ou norte
                var perpendicular = Vector3.Cross(endPoint - startPoint, Vector3.up).normalized;
                var arcPoint = midPoint + perpendicular * (distance * 0.08f) + Vector3.up * 0.02f;

                lr.SetPosition(0, startPoint);
                lr.SetPosition(1, Vector3.Lerp(startPoint, arcPoint, 0.5f));
                lr.SetPosition(2, arcPoint);
                lr.SetPosition(3, Vector3.Lerp(arcPoint, endPoint, 0.5f));
                lr.SetPosition(4, endPoint);
            }
            else
            {
                lr.SetPosition(0, startPoint);
                lr.SetPosition(1, endPoint);
            }

            var entry = new ConnectionEntry
            {
                TerritoryA = idA,
                TerritoryB = idB,
                IsSeaRoute = isSea,
                LineRenderer = lr,
                DefaultColor = color,
                DefaultWidth = width,
                DefaultMaterial = defaultMat
            };

            RegisterConnection(entry);
        }

        /// <summary>
        /// Rota Trans-Pacífico clássica (Alasca <-> Vladivostok).
        /// Em vez de cruzar grosseiramente o centro do mapa, traça duas conexões elegantes
        /// saindo do Alasca até a margem esquerda e de Vladivostok até a margem direita.
        /// </summary>
        private void CreateTransPacificLines(string idA, Vector3 posA, string idB, Vector3 posB)
        {
            // Segmento A: Alasca -> Borda Oeste (-27f, Z)
            var alascaPos = string.Equals(idA, TerritoryIds.Alasca, StringComparison.OrdinalIgnoreCase) ? posA : posB;
            var vladPos = string.Equals(idA, TerritoryIds.Vladivostok, StringComparison.OrdinalIgnoreCase) ? posA : posB;

            var lineObj = new GameObject($"Line_TransPacific_{idA}_{idB}");
            lineObj.transform.SetParent(transform, false);

            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 4;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.startWidth = seaRouteWidth;
            lr.endWidth = seaRouteWidth;
            lr.sharedMaterial = _seaRouteMaterial!;
            lr.startColor = seaRouteColor;
            lr.endColor = seaRouteColor;
            lr.textureMode = LineTextureMode.Tile;

            // Rota de Alasca até o limite oeste
            var startAlasca = new Vector3(alascaPos.x, linesHeightY, alascaPos.z);
            var westEdge = new Vector3(-26.5f, linesHeightY, alascaPos.z + 0.5f);

            // Rota da borda leste até Vladivostok
            var eastEdge = new Vector3(26.5f, linesHeightY, vladPos.z + 0.5f);
            var endVlad = new Vector3(vladPos.x, linesHeightY, vladPos.z);

            lr.SetPosition(0, startAlasca);
            lr.SetPosition(1, westEdge);
            lr.SetPosition(2, eastEdge);
            lr.SetPosition(3, endVlad);

            var entry = new ConnectionEntry
            {
                TerritoryA = idA,
                TerritoryB = idB,
                IsSeaRoute = true,
                IsSpecialWrap = true,
                LineRenderer = lr,
                DefaultColor = seaRouteColor,
                DefaultWidth = seaRouteWidth,
                DefaultMaterial = _seaRouteMaterial!
            };

            RegisterConnection(entry);
        }

        private void RegisterConnection(ConnectionEntry entry)
        {
            _connections.Add(entry);

            if (!_connectionsByTerritory.TryGetValue(entry.TerritoryA, out var listA))
            {
                listA = new List<ConnectionEntry>();
                _connectionsByTerritory[entry.TerritoryA] = listA;
            }
            listA.Add(entry);

            if (!_connectionsByTerritory.TryGetValue(entry.TerritoryB, out var listB))
            {
                listB = new List<ConnectionEntry>();
                _connectionsByTerritory[entry.TerritoryB] = listB;
            }
            listB.Add(entry);
        }

        /// <summary>
        /// Ilumina dinamicamente as conexões que partem de um território selecionado.
        /// As linhas para territórios aliados ficam em tom dourado/ciano brilhante;
        /// as linhas para alvos de ataque inimigos ficam em vermelho vibrante.
        /// </summary>
        public void HighlightConnectionsFor(string? territoryId, Func<string, bool>? isEnemyPredicate)
        {
            if (string.IsNullOrEmpty(territoryId))
            {
                ClearHighlights();
                return;
            }

            EnsureMaterials();

            // Percorre todas as conexões: atenua as desconectadas e destaca as vizinhas
            foreach (var conn in _connections)
            {
                bool connectsToSelected = string.Equals(conn.TerritoryA, territoryId, StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(conn.TerritoryB, territoryId, StringComparison.OrdinalIgnoreCase);

                if (connectsToSelected)
                {
                    string targetId = string.Equals(conn.TerritoryA, territoryId, StringComparison.OrdinalIgnoreCase)
                        ? conn.TerritoryB
                        : conn.TerritoryA;

                    bool isEnemy = isEnemyPredicate != null && isEnemyPredicate(targetId);

                    var highlightMat = isEnemy ? _highlightEnemyMaterial! : _highlightAllyMaterial!;
                    var highlightColor = isEnemy ? highlightEnemyColor : highlightAllyColor;
                    float width = conn.DefaultWidth * highlightWidthMultiplier;

                    conn.LineRenderer.sharedMaterial = highlightMat;
                    conn.LineRenderer.startColor = highlightColor;
                    conn.LineRenderer.endColor = highlightColor;
                    conn.LineRenderer.startWidth = width;
                    conn.LineRenderer.endWidth = width;
                }
                else
                {
                    // Atenua levemente linhas não envolvidas para foco tático
                    var dimmedColor = conn.DefaultColor;
                    dimmedColor.a = 0.18f;

                    conn.LineRenderer.sharedMaterial = conn.DefaultMaterial;
                    conn.LineRenderer.startColor = dimmedColor;
                    conn.LineRenderer.endColor = dimmedColor;
                    conn.LineRenderer.startWidth = conn.DefaultWidth * 0.8f;
                    conn.LineRenderer.endWidth = conn.DefaultWidth * 0.8f;
                }
            }
        }

        /// <summary>
        /// Restaura todas as conexões para seu estilo padrão.
        /// </summary>
        public void ClearHighlights()
        {
            foreach (var conn in _connections)
            {
                if (conn.LineRenderer == null) continue;

                conn.LineRenderer.sharedMaterial = conn.DefaultMaterial;
                conn.LineRenderer.startColor = conn.DefaultColor;
                conn.LineRenderer.endColor = conn.DefaultColor;
                conn.LineRenderer.startWidth = conn.DefaultWidth;
                conn.LineRenderer.endWidth = conn.DefaultWidth;
            }
        }

        /// <summary>
        /// Remove todas as linhas instanciadas.
        /// </summary>
        public void ClearAllLines()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            _connections.Clear();
            _connectionsByTerritory.Clear();
        }

        /// <summary>
        /// Determina se a rota entre os territórios é marítima ou intercontinental no War clássico.
        /// </summary>
        public static bool IsSeaOrIntercontinentalRoute(string idA, string idB)
        {
            var defA = ClassicMapData.GetDefinition(idA);
            var defB = ClassicMapData.GetDefinition(idB);

            if (defA == null || defB == null) return false;

            // Continentes diferentes são sempre travessias marítimas/intercontinentais
            if (defA.Continent != defB.Continent) return true;

            // Ilhas e travessias marítimas intra-continente no War
            var islands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TerritoryIds.Islandia,
                TerritoryIds.Inglaterra,
                TerritoryIds.Madagascar,
                TerritoryIds.Japao,
                TerritoryIds.Sumatra,
                TerritoryIds.Borneo,
                TerritoryIds.NovaGuine,
                TerritoryIds.Australia
            };

            return islands.Contains(idA) || islands.Contains(idB);
        }

        private static bool IsTransPacificConnection(string idA, string idB)
        {
            return (string.Equals(idA, TerritoryIds.Alasca, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(idB, TerritoryIds.Vladivostok, StringComparison.OrdinalIgnoreCase)) ||
                   (string.Equals(idB, TerritoryIds.Alasca, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(idA, TerritoryIds.Vladivostok, StringComparison.OrdinalIgnoreCase));
        }

        private static string MakePairKey(string idA, string idB)
        {
            return string.Compare(idA, idB, StringComparison.OrdinalIgnoreCase) < 0
                ? $"{idA}_{idB}"
                : $"{idB}_{idA}";
        }

        private void EnsureMaterials()
        {
            if (_terrestrialMaterial != null && _seaRouteMaterial != null &&
                _highlightAllyMaterial != null && _highlightEnemyMaterial != null)
            {
                return;
            }

            var shader = FindLineShader();

            _terrestrialMaterial = new Material(shader)
            {
                name = "War_Terrestrial_Line_Mat"
            };
            SetMaterialColorProperties(_terrestrialMaterial, terrestrialColor);

            if (_dottedTexture == null)
            {
                _dottedTexture = CreateDottedTexture();
            }

            _seaRouteMaterial = new Material(shader)
            {
                name = "War_SeaRoute_Dotted_Mat",
                mainTexture = _dottedTexture
            };
            SetMaterialColorProperties(_seaRouteMaterial, seaRouteColor);

            _highlightAllyMaterial = new Material(shader)
            {
                name = "War_Highlight_Ally_Line_Mat"
            };
            SetMaterialColorProperties(_highlightAllyMaterial, highlightAllyColor);

            _highlightEnemyMaterial = new Material(shader)
            {
                name = "War_Highlight_Enemy_Line_Mat"
            };
            SetMaterialColorProperties(_highlightEnemyMaterial, highlightEnemyColor);
        }

        private static void SetMaterialColorProperties(Material mat, Color col)
        {
            mat.enableInstancing = true;
            mat.color = col;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", col);
            }
        }

        private static Shader FindLineShader()
        {
            var isUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (isUrp)
            {
                var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit != null) return urpUnlit;
            }

            return Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        }

        private static Texture2D CreateDottedTexture()
        {
            // Textura 32x4 gerando padrão tracejado/pontilhado náutico
            int w = 32;
            int h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "War_Dotted_Pattern",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 60% traço opaco, 40% espaço transparente
                    bool isDash = (x % 16) < 10;
                    pixels[y * w + x] = isDash ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
