#nullable enable
using System.Collections.Generic;
using UnityEngine;
using War.Core.Models;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza marcadores cartográficos interativos para os 6 continentes nos cantos do tabuleiro.
    /// Inclui linhas indicadoras sutis apontando para as massas continentais e suporte a hover
    /// para destacar automaticamente todos os territórios da região ao passar o mouse.
    /// </summary>
    public class ContinentIndicatorsRenderer : MonoBehaviour
    {
        [System.Serializable]
        public struct ContinentCallout
        {
            public string Name;
            public ContinentType Continent;
            public int ArmyBonus;
            public Color ThemeColor;
            public Vector3 WatermarkPosition;
            public Vector3 TargetPosition;

            public ContinentCallout(string name, ContinentType continent, int armyBonus, Color themeColor, Vector3 pos, Vector3 targetPos)
            {
                Name = name;
                Continent = continent;
                ArmyBonus = armyBonus;
                ThemeColor = themeColor;
                WatermarkPosition = pos;
                TargetPosition = targetPos;
            }
        }

        [Header("Configurações Cartográficas")]
        [SerializeField] private bool showWatermarks = true;
        [SerializeField] private BoardView? boardView;

        private readonly List<GameObject> _spawnedWatermarks = new();
        private static Material? _lineMaterial;

        // Posições nos cantos abertos dos oceanos e pontos âncora centrais de cada continente
        public static readonly ContinentCallout[] ClassicContinents = new ContinentCallout[]
        {
            new ContinentCallout("AMÉRICA DO NORTE", ContinentType.NorthAmerica, 5, new Color(0.92f, 0.45f, 0.14f),
                new Vector3(-24.5f, 0.05f, 13.5f), new Vector3(-15.0f, 0.20f, 8.5f)),

            new ContinentCallout("AMÉRICA DO SUL", ContinentType.SouthAmerica, 2, new Color(0.35f, 0.78f, 0.25f),
                new Vector3(-23.0f, 0.05f, -11.5f), new Vector3(-8.5f, 0.20f, -4.5f)),

            new ContinentCallout("EUROPA", ContinentType.Europe, 5, new Color(0.18f, 0.75f, 0.92f),
                new Vector3(1.5f, 0.05f, 14.0f), new Vector3(1.5f, 0.20f, 8.5f)),

            new ContinentCallout("ÁFRICA", ContinentType.Africa, 3, new Color(0.85f, 0.22f, 0.55f),
                new Vector3(2.5f, 0.05f, -13.5f), new Vector3(3.0f, 0.20f, -2.5f)),

            new ContinentCallout("ÁSIA", ContinentType.Asia, 7, new Color(0.92f, 0.72f, 0.10f),
                new Vector3(23.5f, 0.05f, 13.5f), new Vector3(15.0f, 0.20f, 6.5f)),

            new ContinentCallout("OCEANIA", ContinentType.Oceania, 2, new Color(0.88f, 0.25f, 0.25f),
                new Vector3(23.0f, 0.05f, -13.5f), new Vector3(19.0f, 0.20f, -5.5f))
        };

        private void Start()
        {
            if (boardView == null)
            {
                boardView = GetComponentInParent<BoardView>() ?? Object.FindAnyObjectByType<BoardView>();
            }

            if (_spawnedWatermarks.Count == 0)
            {
                RebuildIndicators();
            }
        }

        /// <summary>
        /// Reconstrói as marcas cartográficas dos continentes com linhas indicadoras e gatilhos de hover.
        /// </summary>
        public void RebuildIndicators()
        {
            foreach (var go in _spawnedWatermarks)
            {
                if (go != null) DestroyImmediate(go);
            }
            _spawnedWatermarks.Clear();

            if (!showWatermarks) return;

            if (boardView == null)
            {
                boardView = GetComponentInParent<BoardView>() ?? Object.FindAnyObjectByType<BoardView>();
            }

            EnsureLineMaterial();

            foreach (var continent in ClassicContinents)
            {
                var labelGo = new GameObject($"Watermark_{continent.Name}");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.position = continent.WatermarkPosition;
                labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Plano contra a mesa

                // Texto 3D da marca d'água
                var textMesh = labelGo.AddComponent<TextMesh>();
                textMesh.text = $"{continent.Name}  (+{continent.ArmyBonus})";
                textMesh.fontSize = 32;
                textMesh.characterSize = 0.09f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;

                var defaultColor = Color.Lerp(continent.ThemeColor, Color.white, 0.25f);
                defaultColor.a = 0.75f;
                textMesh.color = defaultColor;
                textMesh.fontStyle = FontStyle.Bold;

                // BoxCollider para detectar hover do mouse nativamente
                var col = labelGo.AddComponent<BoxCollider>();
                col.size = new Vector3(9.5f, 2.0f, 0.5f);
                col.center = Vector3.zero;

                // Linha indicadora sutil conectando o rótulo ao centro da massa continental
                var lineGo = new GameObject($"Line_{continent.Name}");
                lineGo.transform.SetParent(labelGo.transform, false);
                lineGo.transform.localRotation = Quaternion.identity;

                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.startWidth = 0.07f;
                lr.endWidth = 0.04f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                if (_lineMaterial != null) lr.sharedMaterial = _lineMaterial;

                var defaultLineColor = continent.ThemeColor;
                defaultLineColor.a = 0.38f;
                lr.startColor = defaultLineColor;
                lr.endColor = defaultLineColor;

                // Calcula direção e traça a linha de indicação
                Vector3 startPos = continent.WatermarkPosition + (continent.TargetPosition - continent.WatermarkPosition).normalized * 3.5f;
                startPos.y = 0.08f;
                Vector3 endPos = continent.TargetPosition;
                endPos.y = 0.12f;

                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);

                // Gatilho de Hover interativo
                var trigger = labelGo.AddComponent<ContinentWatermarkHoverTrigger>();
                trigger.Init(continent.Continent, boardView, textMesh, lr, continent.ThemeColor, defaultColor);

                _spawnedWatermarks.Add(labelGo);
            }
        }

        private static void EnsureLineMaterial()
        {
            if (_lineMaterial == null)
            {
                var s = Shader.Find("Universal Render Pipeline/Unlit") 
                     ?? Shader.Find("Sprites/Default") 
                     ?? Shader.Find("Unlit/Color");

                _lineMaterial = new Material(s)
                {
                    name = "Continent_Indicator_Line_Mat",
                    enableInstancing = true
                };
            }
        }
    }

    /// <summary>
    /// Componente que escuta eventos de mouse sobre o rótulo do continente
    /// e destaca automaticamente todos os territórios da região no BoardView.
    /// </summary>
    public class ContinentWatermarkHoverTrigger : MonoBehaviour
    {
        private ContinentType _continent;
        private BoardView? _boardView;
        private TextMesh? _textMesh;
        private LineRenderer? _lineRenderer;
        private Color _themeColor;
        private Color _defaultTextColor;

        public void Init(ContinentType continent, BoardView? boardView, TextMesh? textMesh, LineRenderer? lr, Color themeColor, Color defaultTextColor)
        {
            _continent = continent;
            _boardView = boardView;
            _textMesh = textMesh;
            _lineRenderer = lr;
            _themeColor = themeColor;
            _defaultTextColor = defaultTextColor;
        }

        private void OnMouseEnter()
        {
            // Ilumina o texto do continente na cor tema vibrante
            if (_textMesh != null)
            {
                var brightText = Color.Lerp(_themeColor, Color.white, 0.40f);
                brightText.a = 1.0f;
                _textMesh.color = brightText;
            }

            // Realça a linha indicadora
            if (_lineRenderer != null)
            {
                var brightLine = _themeColor;
                brightLine.a = 0.95f;
                _lineRenderer.startColor = brightLine;
                _lineRenderer.endColor = brightLine;
                _lineRenderer.startWidth = 0.13f;
                _lineRenderer.endWidth = 0.08f;
            }

            // Destaca todos os territórios pertencentes ao continente
            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, true);
            }
        }

        private void OnMouseExit()
        {
            // Restaura texto suave
            if (_textMesh != null)
            {
                _textMesh.color = _defaultTextColor;
            }

            // Restaura linha sutil
            if (_lineRenderer != null)
            {
                var defaultLine = _themeColor;
                defaultLine.a = 0.38f;
                _lineRenderer.startColor = defaultLine;
                _lineRenderer.endColor = defaultLine;
                _lineRenderer.startWidth = 0.07f;
                _lineRenderer.endWidth = 0.04f;
            }

            // Remove o destaque dos territórios do continente
            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, false);
            }
        }
    }
}
