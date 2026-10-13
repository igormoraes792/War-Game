#nullable enable
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using War.Core.Models;
using War.Presentation.UI;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza marcadores cartográficos náuticos oficiais para os 6 continentes nos oceanos do tabuleiro,
    /// inspirados no design clássico do jogo de tabuleiro WAR (Grow).
    /// Utiliza cartelas náuticas refinadas com moldura colorida, tipografia vetorial TextMeshPro SDF de alta nitidez,
    /// linhas de ancoragem litorânea com materiais URP seguros (zero magenta) e destaque interativo ao passar o mouse.
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

        // Posições estratégicas nos oceanos e pontos de ancoragem litorânea oficiais
        public static readonly ContinentCallout[] ClassicContinents = new ContinentCallout[]
        {
            // América do Norte: no Pacífico Norte a oeste da Califórnia / Vancouver
            new ContinentCallout("AMÉRICA DO NORTE", ContinentType.NorthAmerica, 5, new Color(0.38f, 0.52f, 0.42f),
                new Vector3(-24.0f, 0.05f, 4.5f), new Vector3(-18.0f, 0.15f, 6.5f)),

            // América do Sul: no Pacífico Sul a oeste do Peru / Chile
            new ContinentCallout("AMÉRICA DO SUL", ContinentType.SouthAmerica, 2, new Color(0.78f, 0.38f, 0.36f),
                new Vector3(-19.5f, 0.05f, -8.0f), new Vector3(-13.5f, 0.15f, -6.5f)),

            // Europa: no Oceano Ártico aberto entre Groenlândia e Escandinávia (espaço vazio sem sobreposição)
            new ContinentCallout("EUROPA", ContinentType.Europe, 5, new Color(0.32f, 0.55f, 0.65f),
                new Vector3(-2.0f, 0.05f, 15.0f), new Vector3(-0.5f, 0.15f, 9.5f)),

            // África: no Atlântico Sul a sudoeste do Congo / África do Sul
            new ContinentCallout("ÁFRICA", ContinentType.Africa, 3, new Color(0.78f, 0.60f, 0.32f),
                new Vector3(-4.5f, 0.05f, -12.5f), new Vector3(0.5f, 0.15f, -8.0f)),

            // Ásia: no Pacífico Norte a leste do Japão e norte da Oceania (espaço aberto sem sobreposição)
            new ContinentCallout("ÁSIA", ContinentType.Asia, 7, new Color(0.85f, 0.68f, 0.28f),
                new Vector3(25.0f, 0.05f, 6.0f), new Vector3(20.5f, 0.15f, 5.0f)),

            // Oceania: no Oceano Índico a sudoeste da Austrália (100% desobstruído da Austrália)
            new ContinentCallout("OCEANIA", ContinentType.Oceania, 2, new Color(0.42f, 0.60f, 0.45f),
                new Vector3(13.8f, 0.05f, -14.2f), new Vector3(18.5f, 0.15f, -11.0f))
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
        /// Reconstrói as cartelas cartográficas náuticas dos continentes (estilo oficial WAR).
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

            var safeShader = GetSafeShader();
            var sdfFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            foreach (var continent in ClassicContinents)
            {
                var rootGo = new GameObject($"Continent_{continent.Name}");
                rootGo.transform.SetParent(transform, false);
                rootGo.transform.position = continent.TextPosition;

                // 1. Cartela Base Náutica 3D (Elegante placa de comando com moldura na cor do continente)
                var cartoucheObj = new GameObject("Cartouche_Root");
                cartoucheObj.transform.SetParent(rootGo.transform, false);
                cartoucheObj.transform.localPosition = Vector3.zero;

                // 1.1 Moldura exterior da cartela (acabamento canvas de galeria com tom do continente)
                var rimObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rimObj.name = "Cartouche_Border";
                rimObj.transform.SetParent(cartoucheObj.transform, false);
                rimObj.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                rimObj.transform.localScale = new Vector3(5.2f, 0.03f, 1.7f);

                var rimColor = Color.Lerp(continent.ThemeColor, new Color(0.85f, 0.88f, 0.92f), 0.40f);
                var rimMat = new Material(safeShader)
                {
                    name = $"Mat_CartoucheRim_{continent.Name}",
                    color = rimColor,
                    enableInstancing = true
                };
                if (rimMat.HasProperty("_BaseColor")) rimMat.SetColor("_BaseColor", rimColor);
                if (rimMat.HasProperty("_Smoothness")) rimMat.SetFloat("_Smoothness", 0.15f);

                var rimRend = rimObj.GetComponent<MeshRenderer>();
                if (rimRend != null)
                {
                    rimRend.sharedMaterial = rimMat;
                    rimRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var rimCol = rimObj.GetComponent<Collider>();
                if (rimCol != null) DestroyImmediate(rimCol);

                // 1.2 Fundo interior da cartela (pergaminho/canvas claro estilo galeria de arte)
                var plateObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plateObj.name = "Cartouche_Plate";
                plateObj.transform.SetParent(cartoucheObj.transform, false);
                plateObj.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                plateObj.transform.localScale = new Vector3(4.9f, 0.035f, 1.45f);

                var plateColor = new Color(0.97f, 0.96f, 0.94f, 0.96f); // Ivory canvas
                var plateMat = new Material(safeShader)
                {
                    name = $"Mat_CartouchePlate_{continent.Name}",
                    color = plateColor,
                    enableInstancing = true
                };
                if (plateMat.HasProperty("_BaseColor")) plateMat.SetColor("_BaseColor", plateColor);
                if (plateMat.HasProperty("_Smoothness")) plateMat.SetFloat("_Smoothness", 0.10f);

                var plateRend = plateObj.GetComponent<MeshRenderer>();
                if (plateRend != null)
                {
                    plateRend.sharedMaterial = plateMat;
                    plateRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var plateCol = plateObj.GetComponent<Collider>();
                if (plateCol != null) DestroyImmediate(plateCol);

                // 2. Tipografia Vetorial TextMeshPro SDF de Alta Nitidez
                var textContainer = new GameObject("Text_Container");
                textContainer.transform.SetParent(cartoucheObj.transform, false);
                textContainer.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                textContainer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                // 2.1 Nome do Continente em Tipografia Editorial Elegante
                var nameObj = new GameObject("Text_Name");
                nameObj.transform.SetParent(textContainer.transform, false);
                nameObj.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                nameObj.transform.localRotation = Quaternion.identity;

                var nameTmp = nameObj.AddComponent<TextMeshPro>();
                nameTmp.text = continent.Name;
                nameTmp.fontSize = 2.6f;
                nameTmp.fontStyle = FontStyles.Bold;
                nameTmp.alignment = TextAlignmentOptions.Center;
                nameTmp.color = new Color(0.12f, 0.15f, 0.20f, 1f); // Grafite escuro nobre de alta nitidez
                nameTmp.outlineWidth = 0.0f;
                nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
                if (sdfFont != null) nameTmp.font = sdfFont;
                var nameRt = nameObj.GetComponent<RectTransform>();
                if (nameRt != null) nameRt.sizeDelta = new Vector2(4.8f, 0.8f);

                // 2.2 Texto do Bônus de Tropas (Ex: "+2 EXÉRCITOS / RODADA")
                var bonusObj = new GameObject("Text_Bonus");
                bonusObj.transform.SetParent(textContainer.transform, false);
                bonusObj.transform.localPosition = new Vector3(0f, -0.22f, 0f);
                bonusObj.transform.localRotation = Quaternion.identity;

                var bonusTmp = bonusObj.AddComponent<TextMeshPro>();
                bonusTmp.text = $"+{continent.ArmyBonus} EXÉRCITOS / RODADA";
                bonusTmp.fontSize = 1.7f;
                bonusTmp.fontStyle = FontStyles.Bold;
                bonusTmp.alignment = TextAlignmentOptions.Center;
                bonusTmp.color = Color.Lerp(continent.ThemeColor, new Color(0.15f, 0.20f, 0.28f), 0.40f);
                bonusTmp.outlineWidth = 0.0f;
                bonusTmp.textWrappingMode = TextWrappingModes.NoWrap;
                if (sdfFont != null) bonusTmp.font = sdfFont;
                var bonusRt = bonusObj.GetComponent<RectTransform>();
                if (bonusRt != null) bonusRt.sizeDelta = new Vector2(4.8f, 0.6f);

                // 3. Linha Náutica Tática até o Litoral do Continente (Material URP Seguro)
                var lineGo = new GameObject($"Line_{continent.Name}");
                lineGo.transform.SetParent(rootGo.transform, false);

                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 3;
                lr.startWidth = 0.07f;
                lr.endWidth = 0.04f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                // Material dedicado com cor exata do continente (garante que NUNCA fique magenta)
                var lineMat = new Material(safeShader)
                {
                    name = $"Mat_Line_{continent.Name}",
                    color = continent.ThemeColor,
                    enableInstancing = true
                };
                if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", continent.ThemeColor);
                lr.sharedMaterial = lineMat;

                var lineColor = continent.ThemeColor;
                lineColor.a = 0.65f;
                lr.startColor = lineColor;
                lr.endColor = lineColor;

                // Traçado náutico elegante da borda da cartela até o litoral
                Vector3 toTarget = (continent.TargetPosition - continent.TextPosition).normalized;
                Vector3 startPos = continent.TextPosition + toTarget * 2.6f;
                startPos.y = 0.06f;
                Vector3 endPos = continent.TargetPosition;
                endPos.y = 0.12f;

                Vector3 midPos = Vector3.Lerp(startPos, endPos, 0.45f);
                midPos.y = 0.08f;

                lr.SetPosition(0, startPos);
                lr.SetPosition(1, midPos);
                lr.SetPosition(2, endPos);

                // 4. Ancoragem Litorânea (Pequeno anel náutico no litoral indicando o continente)
                var anchorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                anchorObj.name = $"Anchor_{continent.Name}";
                anchorObj.transform.SetParent(rootGo.transform, false);
                anchorObj.transform.position = endPos;
                anchorObj.transform.localScale = new Vector3(0.40f, 0.02f, 0.40f);

                var anchorRend = anchorObj.GetComponent<MeshRenderer>();
                if (anchorRend != null)
                {
                    anchorRend.sharedMaterial = rimMat;
                    anchorRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var anchorCol = anchorObj.GetComponent<Collider>();
                if (anchorCol != null) DestroyImmediate(anchorCol);

                // 5. Colisor para detecção de hover do mouse sobre a cartela (com altura e profundidade generosas)
                var col = rootGo.AddComponent<BoxCollider>();
                col.size = new Vector3(6.0f, 2.0f, 2.6f);
                col.center = new Vector3(0f, 0.5f, 0f);

                var trigger = rootGo.AddComponent<ContinentWatermarkHoverTrigger>();
                trigger.Init(continent.Continent, boardView, nameTmp, bonusTmp, lr, cartoucheObj.transform, continent.ThemeColor);

                _spawnedIndicators.Add(rootGo);
            }
        }

        private static Shader GetSafeShader()
        {
            var isUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (isUrp)
            {
                var urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit != null) return urpLit;

                var urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpSimpleLit != null) return urpSimpleLit;

                var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit != null) return urpUnlit;
            }

            return Shader.Find("Standard") ?? Shader.Find("Diffuse") ?? Shader.Find("Unlit/Color");
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
        private TMP_Text? _nameTmp;
        private TMP_Text? _bonusTmp;
        private LineRenderer? _lineRenderer;
        private Transform? _cartoucheRoot;
        private Color _themeColor;
        private Vector3 _initialCartouchePos;

        private bool _isHovered;

        public void Init(
            ContinentType continent,
            BoardView? boardView,
            TMP_Text? nameText,
            TMP_Text? bonusText,
            LineRenderer? lr,
            Transform? cartoucheRoot,
            Color themeColor)
        {
            _continent = continent;
            _boardView = boardView;
            _nameTmp = nameText;
            _bonusTmp = bonusText;
            _lineRenderer = lr;
            _cartoucheRoot = cartoucheRoot;
            _themeColor = themeColor;

            if (_cartoucheRoot != null)
            {
                _initialCartouchePos = _cartoucheRoot.localPosition;
            }
        }

        private void Update()
        {
            if (_isHovered && GameHUD.IsModalOpen())
            {
                OnMouseExit();
            }
        }

        private void OnMouseEnter()
        {
            if (GameHUD.IsModalOpen()) return;

            _isHovered = true;
            if (_boardView == null)
            {
                _boardView = Object.FindAnyObjectByType<BoardView>();
            }

            if (_nameTmp != null)
            {
                _nameTmp.color = _themeColor;
            }
            if (_bonusTmp != null)
            {
                _bonusTmp.color = new Color(0.15f, 0.20f, 0.25f);
            }

            if (_lineRenderer != null)
            {
                var brightLine = _themeColor;
                brightLine.a = 1.0f;
                _lineRenderer.startColor = brightLine;
                _lineRenderer.endColor = brightLine;
                _lineRenderer.startWidth = 0.12f;
                _lineRenderer.endWidth = 0.08f;
            }

            if (_cartoucheRoot != null)
            {
                var p = _initialCartouchePos;
                p.y += 0.12f;
                _cartoucheRoot.localPosition = p;
            }

            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, true);
            }
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            if (_boardView == null)
            {
                _boardView = Object.FindAnyObjectByType<BoardView>();
            }

            if (_nameTmp != null)
            {
                _nameTmp.color = new Color(0.12f, 0.15f, 0.20f, 1f);
            }
            if (_bonusTmp != null)
            {
                _bonusTmp.color = Color.Lerp(_themeColor, new Color(0.15f, 0.20f, 0.28f), 0.40f);
            }

            if (_lineRenderer != null)
            {
                var defaultLine = _themeColor;
                defaultLine.a = 0.65f;
                _lineRenderer.startColor = defaultLine;
                _lineRenderer.endColor = defaultLine;
                _lineRenderer.startWidth = 0.08f;
                _lineRenderer.endWidth = 0.05f;
            }

            if (_cartoucheRoot != null)
            {
                _cartoucheRoot.localPosition = _initialCartouchePos;
            }

            if (_boardView != null)
            {
                _boardView.HighlightContinent(_continent, false);
            }
        }
    }
}
