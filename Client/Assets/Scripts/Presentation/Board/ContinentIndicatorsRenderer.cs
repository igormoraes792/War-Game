#nullable enable
using System.Collections.Generic;
using UnityEngine;
using War.Core.Models;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza marcadores cartográficos militares para os 6 continentes nos oceanos do tabuleiro,
    /// inspirados no design clássico do tabuleiro de WAR.
    /// Inclui cartelas táticas elegantes com brasão e bônus de tropas, linhas náuticas com inflexão
    /// e âncoras de alvo na costa, além de hover interativo que destaca o continente inteiro.
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
            public Vector3 CardPosition;
            public Vector3 TargetPosition;

            public ContinentCallout(string name, ContinentType continent, int armyBonus, Color themeColor, Vector3 pos, Vector3 targetPos)
            {
                Name = name;
                Continent = continent;
                ArmyBonus = armyBonus;
                ThemeColor = themeColor;
                CardPosition = pos;
                TargetPosition = targetPos;
            }
        }

        [Header("Configurações Cartográficas")]
        [SerializeField] private bool showIndicators = true;
        [SerializeField] private BoardView? boardView;

        private readonly List<GameObject> _spawnedIndicators = new();
        private static Material? _lineMaterial;
        private static Material? _cardMaterial;
        private static Material? _accentMaterial;

        // Posições nos cantos abertos dos oceanos e pontos âncora litorâneos de cada continente
        public static readonly ContinentCallout[] ClassicContinents = new ContinentCallout[]
        {
            new ContinentCallout("AMÉRICA DO NORTE", ContinentType.NorthAmerica, 5, new Color(0.94f, 0.48f, 0.15f),
                new Vector3(-25.5f, 0.05f, 13.0f), new Vector3(-17.0f, 0.15f, 8.5f)),

            new ContinentCallout("AMÉRICA DO SUL", ContinentType.SouthAmerica, 2, new Color(0.35f, 0.80f, 0.25f),
                new Vector3(-23.5f, 0.05f, -9.5f), new Vector3(-13.5f, 0.15f, -5.0f)),

            new ContinentCallout("EUROPA", ContinentType.Europe, 5, new Color(0.18f, 0.76f, 0.94f),
                new Vector3(1.5f, 0.05f, 15.0f), new Vector3(1.5f, 0.15f, 9.8f)),

            new ContinentCallout("ÁFRICA", ContinentType.Africa, 3, new Color(0.88f, 0.22f, 0.58f),
                new Vector3(2.5f, 0.05f, -14.5f), new Vector3(2.5f, 0.15f, -8.0f)),

            new ContinentCallout("ÁSIA", ContinentType.Asia, 7, new Color(0.95f, 0.74f, 0.12f),
                new Vector3(23.5f, 0.05f, 14.5f), new Vector3(16.5f, 0.15f, 8.5f)),

            new ContinentCallout("OCEANIA", ContinentType.Oceania, 2, new Color(0.90f, 0.25f, 0.25f),
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
        /// Reconstrói todas as cartelas militares e linhas táticas dos continentes.
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

            EnsureMaterials();

            foreach (var continent in ClassicContinents)
            {
                var cardRoot = new GameObject($"ContinentBadge_{continent.Name}");
                cardRoot.transform.SetParent(transform, false);
                cardRoot.transform.position = continent.CardPosition;

                // 1. Placa/Cartela de fundo escuro estilo mesa de comando militar
                var plateObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plateObj.name = "Badge_Plate";
                plateObj.transform.SetParent(cardRoot.transform, false);
                plateObj.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                plateObj.transform.localScale = new Vector3(7.4f, 0.025f, 2.3f);

                var plateCol = plateObj.GetComponent<Collider>();
                if (plateCol != null) DestroyImmediate(plateCol);

                var plateRend = plateObj.GetComponent<MeshRenderer>();
                if (plateRend != null && _cardMaterial != null)
                {
                    plateRend.sharedMaterial = _cardMaterial;
                    plateRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // 1.1 Faixa / Acento lateral na cor tema do continente
                var accentObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                accentObj.name = "Badge_AccentBar";
                accentObj.transform.SetParent(cardRoot.transform, false);
                accentObj.transform.localPosition = new Vector3(-3.55f, 0.035f, 0f);
                accentObj.transform.localScale = new Vector3(0.30f, 0.03f, 2.3f);

                var accentCol = accentObj.GetComponent<Collider>();
                if (accentCol != null) DestroyImmediate(accentCol);

                var accentRend = accentObj.GetComponent<MeshRenderer>();
                Material? customAccentMat = null;
                if (accentRend != null && _accentMaterial != null)
                {
                    customAccentMat = new Material(_accentMaterial)
                    {
                        color = continent.ThemeColor,
                        enableInstancing = true
                    };
                    accentRend.material = customAccentMat;
                    accentRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // 2. Textos Cartográficos Militares (Nome e Bônus em 2 linhas organizadas)
                var nameTextObj = new GameObject("Text_ContinentName");
                nameTextObj.transform.SetParent(cardRoot.transform, false);
                nameTextObj.transform.localPosition = new Vector3(0.2f, 0.05f, 0.40f);
                nameTextObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                nameTextObj.transform.localScale = Vector3.one * 0.13f;

                var nameTextMesh = nameTextObj.AddComponent<TextMesh>();
                nameTextMesh.text = $"◎  {continent.Name}";
                nameTextMesh.fontSize = 44;
                nameTextMesh.characterSize = 0.22f;
                nameTextMesh.anchor = TextAnchor.MiddleCenter;
                nameTextMesh.alignment = TextAlignment.Center;
                nameTextMesh.color = Color.white;
                nameTextMesh.fontStyle = FontStyle.Bold;

                var bonusTextObj = new GameObject("Text_ArmyBonus");
                bonusTextObj.transform.SetParent(cardRoot.transform, false);
                bonusTextObj.transform.localPosition = new Vector3(0.2f, 0.05f, -0.45f);
                bonusTextObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bonusTextObj.transform.localScale = Vector3.one * 0.12f;

                var bonusTextMesh = bonusTextObj.AddComponent<TextMesh>();
                bonusTextMesh.text = $"+{continent.ArmyBonus} EXÉRCITOS POR RODADA";
                bonusTextMesh.fontSize = 38;
                bonusTextMesh.characterSize = 0.20f;
                bonusTextMesh.anchor = TextAnchor.MiddleCenter;
                bonusTextMesh.alignment = TextAlignment.Center;
                bonusTextMesh.color = Color.Lerp(continent.ThemeColor, Color.white, 0.35f);
                bonusTextMesh.fontStyle = FontStyle.Bold;

                // 3. Linha Tática com Inflexão de 3 Pontos (Estilo Carta Náutica Militar)
                var lineGo = new GameObject($"Line_{continent.Name}");
                lineGo.transform.SetParent(cardRoot.transform, false);

                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 3;
                lr.startWidth = 0.08f;
                lr.endWidth = 0.05f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                if (_lineMaterial != null) lr.sharedMaterial = _lineMaterial;

                var defaultLineColor = continent.ThemeColor;
                defaultLineColor.a = 0.50f;
                lr.startColor = defaultLineColor;
                lr.endColor = defaultLineColor;

                // Calcula traçado com ponto de inflexão
                Vector3 startPos = continent.CardPosition + (continent.TargetPosition - continent.CardPosition).normalized * 3.8f;
                startPos.y = 0.08f;
                Vector3 endPos = continent.TargetPosition;
                endPos.y = 0.12f;

                // Ponto médio com quebra angular para traço militar elegante
                Vector3 midPos = Vector3.Lerp(startPos, endPos, 0.45f);
                midPos.y = 0.10f;

                lr.SetPosition(0, startPos);
                lr.SetPosition(1, midPos);
                lr.SetPosition(2, endPos);

                // 4. Âncora de Alvo Litorânea (Anchor Disc no ponto de chegada da linha no continente)
                var anchorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                anchorObj.name = "Target_Anchor";
                anchorObj.transform.SetParent(cardRoot.transform, false);
                anchorObj.transform.position = endPos;
                anchorObj.transform.localScale = new Vector3(0.55f, 0.02f, 0.55f);

                var anchorCol = anchorObj.GetComponent<Collider>();
                if (anchorCol != null) DestroyImmediate(anchorCol);

                var anchorRend = anchorObj.GetComponent<MeshRenderer>();
                if (anchorRend != null && customAccentMat != null)
                {
                    anchorRend.sharedMaterial = customAccentMat;
                    anchorRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // 5. BoxCollider e Gatilho Interativo de Hover
                var col = cardRoot.AddComponent<BoxCollider>();
                col.size = new Vector3(7.6f, 1.0f, 2.5f);
                col.center = Vector3.zero;

                var trigger = cardRoot.AddComponent<ContinentWatermarkHoverTrigger>();
                trigger.Init(continent.Continent, boardView, nameTextMesh, bonusTextMesh, lr, anchorRend, continent.ThemeColor);

                _spawnedIndicators.Add(cardRoot);
            }
        }

        private static void EnsureMaterials()
        {
            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                           ?? Shader.Find("Sprites/Default") 
                           ?? Shader.Find("Unlit/Color");

            var litShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");

            if (_lineMaterial == null)
            {
                _lineMaterial = new Material(unlitShader)
                {
                    name = "Continent_Indicator_Line_Mat",
                    enableInstancing = true
                };
            }

            if (_cardMaterial == null)
            {
                _cardMaterial = new Material(litShader)
                {
                    name = "Continent_Card_Base_Mat",
                    color = new Color(0.07f, 0.09f, 0.14f, 0.92f),
                    enableInstancing = true
                };
                if (_cardMaterial.HasProperty("_Smoothness")) _cardMaterial.SetFloat("_Smoothness", 0.5f);
            }

            if (_accentMaterial == null)
            {
                _accentMaterial = new Material(litShader)
                {
                    name = "Continent_Accent_Mat",
                    enableInstancing = true
                };
                _accentMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    /// <summary>
    /// Componente que escuta eventos de mouse sobre a cartela do continente
    /// e destaca automaticamente todos os territórios da região no BoardView.
    /// </summary>
    public class ContinentWatermarkHoverTrigger : MonoBehaviour
    {
        private ContinentType _continent;
        private BoardView? _boardView;
        private TextMesh? _nameTextMesh;
        private TextMesh? _bonusTextMesh;
        private LineRenderer? _lineRenderer;
        private MeshRenderer? _anchorRenderer;
        private Color _themeColor;

        public void Init(ContinentType continent, BoardView? boardView, TextMesh? nameText, TextMesh? bonusText, LineRenderer? lr, MeshRenderer? anchorRend, Color themeColor)
        {
            _continent = continent;
            _boardView = boardView;
            _nameTextMesh = nameText;
            _bonusTextMesh = bonusText;
            _lineRenderer = lr;
            _anchorRenderer = anchorRend;
            _themeColor = themeColor;
        }

        private void OnMouseEnter()
        {
            // Ilumina os textos na cor tema vibrante
            if (_nameTextMesh != null)
            {
                _nameTextMesh.color = Color.Lerp(_themeColor, Color.white, 0.60f);
            }
            if (_bonusTextMesh != null)
            {
                _bonusTextMesh.color = Color.white;
            }

            // Realça a linha indicadora tática
            if (_lineRenderer != null)
            {
                var brightLine = _themeColor;
                brightLine.a = 1.0f;
                _lineRenderer.startColor = brightLine;
                _lineRenderer.endColor = brightLine;
                _lineRenderer.startWidth = 0.14f;
                _lineRenderer.endWidth = 0.09f;
            }

            // Destaca todos os territórios pertencentes ao continente
            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, true);
            }
        }

        private void OnMouseExit()
        {
            // Restaura textos suaves
            if (_nameTextMesh != null)
            {
                _nameTextMesh.color = Color.white;
            }
            if (_bonusTextMesh != null)
            {
                _bonusTextMesh.color = Color.Lerp(_themeColor, Color.white, 0.35f);
            }

            // Restaura linha sutil
            if (_lineRenderer != null)
            {
                var defaultLine = _themeColor;
                defaultLine.a = 0.50f;
                _lineRenderer.startColor = defaultLine;
                _lineRenderer.endColor = defaultLine;
                _lineRenderer.startWidth = 0.08f;
                _lineRenderer.endWidth = 0.05f;
            }

            // Remove o destaque dos territórios do continente
            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, false);
            }
        }
    }
}
