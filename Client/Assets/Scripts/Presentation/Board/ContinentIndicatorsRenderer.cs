#nullable enable
using System.Collections.Generic;
using UnityEngine;
using War.Core.Models;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza marcadores cartográficos náuticos para os 6 continentes nos oceanos do tabuleiro,
    /// inspirados no design oficial do jogo WAR.
    /// Tipografia clássica náutica (sem cubos 3D ou caixas flutuantes), linhas táticas com inflexão
    /// e suporte a hover para destacar o continente inteiro.
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
            public Vector3 TextPosition;
            public Vector3 TargetPosition;

            public ContinentCallout(string name, ContinentType continent, int armyBonus, Color themeColor, Vector3 textPos, Vector3 targetPos)
            {
                Name = name;
                Continent = continent;
                ArmyBonus = armyBonus;
                ThemeColor = themeColor;
                TextPosition = textPos;
                TargetPosition = targetPos;
            }
        }

        [Header("Configurações Cartográficas")]
        [SerializeField] private bool showIndicators = true;
        [SerializeField] private BoardView? boardView;

        private readonly List<GameObject> _spawnedIndicators = new();
        private static Material? _lineMaterial;

        // Posições estratégicas nos oceanos e pontos de ancoragem litorânea
        public static readonly ContinentCallout[] ClassicContinents = new ContinentCallout[]
        {
            new ContinentCallout("AMÉRICA DO NORTE", ContinentType.NorthAmerica, 5, new Color(0.95f, 0.48f, 0.15f),
                new Vector3(-25.0f, 0.05f, 12.5f), new Vector3(-17.0f, 0.15f, 8.5f)),

            new ContinentCallout("AMÉRICA DO SUL", ContinentType.SouthAmerica, 2, new Color(0.35f, 0.82f, 0.25f),
                new Vector3(-23.0f, 0.05f, -9.0f), new Vector3(-13.5f, 0.15f, -5.0f)),

            new ContinentCallout("EUROPA", ContinentType.Europe, 5, new Color(0.18f, 0.78f, 0.95f),
                new Vector3(1.5f, 0.05f, 15.0f), new Vector3(1.5f, 0.15f, 9.8f)),

            new ContinentCallout("ÁFRICA", ContinentType.Africa, 3, new Color(0.90f, 0.22f, 0.58f),
                new Vector3(2.5f, 0.05f, -14.5f), new Vector3(2.5f, 0.15f, -8.0f)),

            new ContinentCallout("ÁSIA", ContinentType.Asia, 7, new Color(0.95f, 0.76f, 0.12f),
                new Vector3(23.5f, 0.05f, 14.5f), new Vector3(16.5f, 0.15f, 8.5f)),

            new ContinentCallout("OCEANIA", ContinentType.Oceania, 2, new Color(0.92f, 0.25f, 0.25f),
                new Vector3(23.5f, 0.05f, -13.5f), new Vector3(19.0f, 0.15f, -7.5f))
        };

        private void Start()
        {
            if (boardView == null)
            {
                boardView = GetComponentInParent<BoardView>() ?? Object.FindAnyObjectByType<BoardView>();
            }

            if (_spawnedIndicators.Count == 0)
            {
                RebuildIndicators();
            }
        }

        /// <summary>
        /// Reconstrói as inscrições e linhas náuticas dos continentes (estilo WAR oficial).
        /// </summary>
        public void RebuildIndicators()
        {
            foreach (var go in _spawnedIndicators)
            {
                if (go != null) DestroyImmediate(go);
            }
            _spawnedIndicators.Clear();

            if (!showIndicators) return;

            if (boardView == null)
            {
                boardView = GetComponentInParent<BoardView>() ?? Object.FindAnyObjectByType<BoardView>();
            }

            EnsureLineMaterial();

            foreach (var continent in ClassicContinents)
            {
                var rootGo = new GameObject($"Continent_{continent.Name}");
                rootGo.transform.SetParent(transform, false);
                rootGo.transform.position = continent.TextPosition;

                // 1. Inscrição Cartográfica Náutica (Plana no oceano, estilo mapa de guerra)
                var textContainer = new GameObject("Text_Container");
                textContainer.transform.SetParent(rootGo.transform, false);
                textContainer.transform.localPosition = Vector3.zero;
                textContainer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                // 1.1 Sombra do Nome para contraste náutico absoluto
                var nameShadowObj = new GameObject("Name_Shadow");
                nameShadowObj.transform.SetParent(textContainer.transform, false);
                nameShadowObj.transform.localPosition = new Vector3(0.04f, 0.36f, 0f);
                nameShadowObj.transform.localScale = Vector3.one * 0.14f;

                var nameShadowMesh = nameShadowObj.AddComponent<TextMesh>();
                nameShadowMesh.text = $"◎  {continent.Name}";
                nameShadowMesh.fontSize = 44;
                nameShadowMesh.characterSize = 0.20f;
                nameShadowMesh.anchor = TextAnchor.MiddleCenter;
                nameShadowMesh.alignment = TextAlignment.Center;
                nameShadowMesh.color = new Color(0f, 0f, 0f, 0.85f);
                nameShadowMesh.fontStyle = FontStyle.Bold;

                // 1.2 Nome do Continente em Caixa Alta Nítida
                var nameObj = new GameObject("Text_Name");
                nameObj.transform.SetParent(textContainer.transform, false);
                nameObj.transform.localPosition = new Vector3(0f, 0.40f, 0f);
                nameObj.transform.localScale = Vector3.one * 0.14f;

                var nameTextMesh = nameObj.AddComponent<TextMesh>();
                nameTextMesh.text = $"◎  {continent.Name}";
                nameTextMesh.fontSize = 44;
                nameTextMesh.characterSize = 0.20f;
                nameTextMesh.anchor = TextAnchor.MiddleCenter;
                nameTextMesh.alignment = TextAlignment.Center;
                nameTextMesh.color = Color.white;
                nameTextMesh.fontStyle = FontStyle.Bold;

                // 1.3 Sombra do Bônus
                var bonusShadowObj = new GameObject("Bonus_Shadow");
                bonusShadowObj.transform.SetParent(textContainer.transform, false);
                bonusShadowObj.transform.localPosition = new Vector3(0.04f, -0.44f, 0f);
                bonusShadowObj.transform.localScale = Vector3.one * 0.12f;

                var bonusShadowMesh = bonusShadowObj.AddComponent<TextMesh>();
                bonusShadowMesh.text = $"+{continent.ArmyBonus} EXÉRCITOS";
                bonusShadowMesh.fontSize = 38;
                bonusShadowMesh.characterSize = 0.19f;
                bonusShadowMesh.anchor = TextAnchor.MiddleCenter;
                bonusShadowMesh.alignment = TextAlignment.Center;
                bonusShadowMesh.color = new Color(0f, 0f, 0f, 0.85f);
                bonusShadowMesh.fontStyle = FontStyle.Bold;

                // 1.4 Texto do Bônus em Destaque na Cor do Continente
                var bonusObj = new GameObject("Text_Bonus");
                bonusObj.transform.SetParent(textContainer.transform, false);
                bonusObj.transform.localPosition = new Vector3(0f, -0.40f, 0f);
                bonusObj.transform.localScale = Vector3.one * 0.12f;

                var bonusTextMesh = bonusObj.AddComponent<TextMesh>();
                bonusTextMesh.text = $"+{continent.ArmyBonus} EXÉRCITOS";
                bonusTextMesh.fontSize = 38;
                bonusTextMesh.characterSize = 0.19f;
                bonusTextMesh.anchor = TextAnchor.MiddleCenter;
                bonusTextMesh.alignment = TextAlignment.Center;
                bonusTextMesh.color = Color.Lerp(continent.ThemeColor, Color.white, 0.35f);
                bonusTextMesh.fontStyle = FontStyle.Bold;

                // 2. Linha Tática Náutica com Ângulo Suave
                var lineGo = new GameObject($"Line_{continent.Name}");
                lineGo.transform.SetParent(rootGo.transform, false);

                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 3;
                lr.startWidth = 0.08f;
                lr.endWidth = 0.05f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                if (_lineMaterial != null) lr.sharedMaterial = _lineMaterial;

                var defaultLineColor = continent.ThemeColor;
                defaultLineColor.a = 0.45f;
                lr.startColor = defaultLineColor;
                lr.endColor = defaultLineColor;

                // Traçado náutico do texto até a costa
                Vector3 startPos = continent.TextPosition + (continent.TargetPosition - continent.TextPosition).normalized * 3.5f;
                startPos.y = 0.06f;
                Vector3 endPos = continent.TargetPosition;
                endPos.y = 0.12f;

                Vector3 midPos = Vector3.Lerp(startPos, endPos, 0.45f);
                midPos.y = 0.08f;

                lr.SetPosition(0, startPos);
                lr.SetPosition(1, midPos);
                lr.SetPosition(2, endPos);

                // 3. Colisor para detecção de hover do mouse sobre o texto
                var col = rootGo.AddComponent<BoxCollider>();
                col.size = new Vector3(7.2f, 1.2f, 2.2f);
                col.center = Vector3.zero;

                var trigger = rootGo.AddComponent<ContinentWatermarkHoverTrigger>();
                trigger.Init(continent.Continent, boardView, nameTextMesh, bonusTextMesh, lr, continent.ThemeColor);

                _spawnedIndicators.Add(rootGo);
            }
        }

        private static void EnsureLineMaterial()
        {
            if (_lineMaterial == null)
            {
                var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                               ?? Shader.Find("Sprites/Default") 
                               ?? Shader.Find("Unlit/Color");

                _lineMaterial = new Material(unlitShader)
                {
                    name = "Continent_Line_Mat",
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
        private TextMesh? _nameTextMesh;
        private TextMesh? _bonusTextMesh;
        private LineRenderer? _lineRenderer;
        private Color _themeColor;

        public void Init(ContinentType continent, BoardView? boardView, TextMesh? nameText, TextMesh? bonusText, LineRenderer? lr, Color themeColor)
        {
            _continent = continent;
            _boardView = boardView;
            _nameTextMesh = nameText;
            _bonusTextMesh = bonusText;
            _lineRenderer = lr;
            _themeColor = themeColor;
        }

        private void OnMouseEnter()
        {
            if (_nameTextMesh != null)
            {
                _nameTextMesh.color = Color.Lerp(_themeColor, Color.white, 0.55f);
            }
            if (_bonusTextMesh != null)
            {
                _bonusTextMesh.color = Color.white;
            }

            if (_lineRenderer != null)
            {
                var brightLine = _themeColor;
                brightLine.a = 0.95f;
                _lineRenderer.startColor = brightLine;
                _lineRenderer.endColor = brightLine;
                _lineRenderer.startWidth = 0.13f;
                _lineRenderer.endWidth = 0.08f;
            }

            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, true);
            }
        }

        private void OnMouseExit()
        {
            if (_nameTextMesh != null)
            {
                _nameTextMesh.color = Color.white;
            }
            if (_bonusTextMesh != null)
            {
                _bonusTextMesh.color = Color.Lerp(_themeColor, Color.white, 0.35f);
            }

            if (_lineRenderer != null)
            {
                var defaultLine = _themeColor;
                defaultLine.a = 0.45f;
                _lineRenderer.startColor = defaultLine;
                _lineRenderer.endColor = defaultLine;
                _lineRenderer.startWidth = 0.08f;
                _lineRenderer.endWidth = 0.05f;
            }

            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, false);
            }
        }
    }
}
