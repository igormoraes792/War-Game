#nullable enable
using System;
using UnityEngine;
using TMPro;

namespace War2D.Presentation.Board
{
    /// <summary>
    /// Componente MonoBehaviour que representa visualmente e interativamente um território 2D no mapa de WAR.
    /// Utiliza malha plana precisa, PolygonCollider2D para detecção milimétrica do mouse,
    /// ficha militar clássica circular e tipografia vetorial TextMeshPro SDF.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class Territory2DView : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string territoryId = string.Empty;

        [Header("Renderização 2D")]
        [SerializeField] private MeshRenderer? territoryRenderer;
        [SerializeField] private LineRenderer? borderOutlineRenderer;
        [SerializeField] private PolygonCollider2D? polygonCollider;

        [Header("Ficha Militar e Textos")]
        [SerializeField] private Transform? pinTokenRoot;
        [SerializeField] private SpriteRenderer? pinRimRenderer;
        [SerializeField] private SpriteRenderer? pinBadgeRenderer;
        [SerializeField] private TMP_Text? armyCountText;
        [SerializeField] private TMP_Text? territoryNameText;
        [SerializeField] private GameObject? nameTagRoot;

        [Header("Cores")]
        [SerializeField] private Color defaultNeutralColor = new Color(0.24f, 0.26f, 0.30f, 1f);
        [SerializeField] private Color borderColor = new Color(0.12f, 0.15f, 0.20f, 0.95f);
        [SerializeField] private Color borderHighlightColor = new Color(1.0f, 0.88f, 0.30f, 1.0f);

        // Estados internos
        private Color _ownerColor;
        private bool _isHighlighted;
        private bool _isSelected;
        private bool _isDimmed;
        private int _currentArmyCount = 1;

        private MaterialPropertyBlock? _propertyBlock;
        private Vector3 _initialScale = Vector3.one;
        private float _currentHoverScale = 1.0f;

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        public event Action<Territory2DView>? OnClicked;
        public event Action<Territory2DView>? OnHoverEnter;
        public event Action<Territory2DView>? OnHoverExit;

        public string TerritoryId
        {
            get => territoryId;
            set => territoryId = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public Color CurrentOwnerColor => _ownerColor;
        public int CurrentArmyCount => _currentArmyCount;
        public bool IsHighlighted => _isHighlighted;
        public bool IsSelected => _isSelected;
        public bool IsDimmed => _isDimmed;

        private void Awake()
        {
            _initialScale = transform.localScale;
            _ownerColor = defaultNeutralColor;
            _propertyBlock = new MaterialPropertyBlock();

            if (polygonCollider == null)
            {
                polygonCollider = GetComponent<PolygonCollider2D>();
            }

            if (territoryRenderer == null)
            {
                territoryRenderer = GetComponent<MeshRenderer>();
            }

            if (territoryRenderer != null)
            {
                var mat = territoryRenderer.sharedMaterial;
                bool isInvalid = mat == null || mat.shader == null || !mat.shader.isSupported ||
                    (mat.shader.name.StartsWith("Universal Render Pipeline") && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null);

                if (isInvalid)
                {
                    var safeShader = ShaderUtils2D.GetColorShader();
                    territoryRenderer.sharedMaterial = new Material(safeShader)
                    {
                        name = "Territorio2D_Safe_Mat",
                        color = defaultNeutralColor,
                        enableInstancing = true
                    };
                }
            }
        }

        private void Start()
        {
            ApplyVisualState();
        }

        private void Update()
        {
            // Interpolação de micro-escala suave ao passar o mouse (1.04x) sem distorcer o mapa
            float targetScale = _isSelected ? 1.05f : (_isHighlighted && !_isDimmed ? 1.035f : 1.0f);
            if (Mathf.Abs(_currentHoverScale - targetScale) > 0.001f)
            {
                _currentHoverScale = Mathf.Lerp(_currentHoverScale, targetScale, Time.deltaTime * 18f);
                transform.localScale = _initialScale * _currentHoverScale;
            }
        }

        public void SetOwnerColor(Color color)
        {
            _ownerColor = color;
            ApplyVisualState();
        }

        public void SetArmyCount(int count)
        {
            _currentArmyCount = Mathf.Max(0, count);
            var str = _currentArmyCount.ToString();
            if (armyCountText != null)
            {
                armyCountText.text = str;
            }
        }

        public void SetTerritoryName(string displayName)
        {
            if (territoryNameText != null)
            {
                territoryNameText.text = displayName;
            }
        }

        public void SetHighlighted(bool highlight)
        {
            if (_isHighlighted == highlight) return;
            _isHighlighted = highlight;
            ApplyVisualState();
        }

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            ApplyVisualState();
        }

        public void SetDimmed(bool dimmed)
        {
            if (_isDimmed == dimmed) return;
            _isDimmed = dimmed;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (territoryRenderer == null) return;
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

            // Cálculo da cor do país: NUNCA empalidece. Quando hover, satura e intensifica a cor do jogador!
            Color displayColor = _ownerColor;

            if (_isDimmed)
            {
                displayColor = Color.Lerp(_ownerColor, new Color(0.06f, 0.08f, 0.12f, 1f), 0.55f);
            }
            else if (_isSelected)
            {
                displayColor = Color.Lerp(_ownerColor, Color.white, 0.15f);
            }
            else if (_isHighlighted)
            {
                // Intensifica o tom mantendo a cor forte do jogador
                displayColor = Color.Lerp(_ownerColor, Color.white, 0.08f);
            }

            territoryRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorProp, displayColor);
            _propertyBlock.SetColor(ColorProp, displayColor);
            territoryRenderer.SetPropertyBlock(_propertyBlock);

            // Borda do território
            if (borderOutlineRenderer != null)
            {
                Color curBorder = _isSelected
                    ? borderHighlightColor
                    : (_isHighlighted ? Color.Lerp(borderColor, borderHighlightColor, 0.75f) : borderColor);

                borderOutlineRenderer.startColor = curBorder;
                borderOutlineRenderer.endColor = curBorder;
                borderOutlineRenderer.startWidth = (_isSelected || _isHighlighted) ? 0.09f : 0.055f;
                borderOutlineRenderer.endWidth = borderOutlineRenderer.startWidth;
            }

            // Ficha militar (aro na cor do jogador)
            if (pinRimRenderer != null)
            {
                pinRimRenderer.color = _ownerColor;
            }
        }

        #region Eventos do Mouse
        private void OnMouseEnter()
        {
            SetHighlighted(true);
            OnHoverEnter?.Invoke(this);
        }

        private void OnMouseExit()
        {
            SetHighlighted(false);
            OnHoverExit?.Invoke(this);
        }

        private void OnMouseDown()
        {
            OnClicked?.Invoke(this);
        }
        #endregion
    }
}

