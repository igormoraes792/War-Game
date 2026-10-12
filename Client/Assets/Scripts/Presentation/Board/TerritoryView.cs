#nullable enable
using System;
using UnityEngine;
using TMPro;

namespace War.Presentation.Board
{
    /// <summary>
    /// Componente MonoBehaviour que representa visualmente um território 3D no tabuleiro.
    /// Gerencia cores de dono, estados de seleção/hover, contador de tropas e eventos de interação do mouse.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class TerritoryView : MonoBehaviour
    {
        [Header("Identificação")]
        [Tooltip("Identificador único e padronizado do território (ex: 'brasil', 'alasca').")]
        [SerializeField] private string territoryId = string.Empty;

        [Header("Renderização e Visuais")]
        [Tooltip("MeshRenderer principal utilizado para coloração e destaque.")]
        [SerializeField] private MeshRenderer? territoryRenderer;

        [Tooltip("Ponto 3D onde tropas visuais são instanciadas ou posicionadas.")]
        [SerializeField] private Transform? armySpawnPoint;

        [Header("Interface e Textos 3D")]
        [Tooltip("Texto TextMeshPro para exibir a quantidade de exércitos no território.")]
        [SerializeField] private TMP_Text? armyCountText;

        [Tooltip("Texto opcional para exibir o nome do território.")]
        [SerializeField] private TMP_Text? territoryNameText;

        [Tooltip("Fallback nativo TextMesh 3D garantido.")]
        [SerializeField] private TextMesh? armyCountLegacyText;
        [SerializeField] private TextMesh? territoryNameLegacyText;

        [Header("Estandarte e Pin Tático")]
        [Tooltip("Transform raiz do estandarte/pin tático do território.")]
        [SerializeField] private Transform? tacticalPinRoot;

        [Tooltip("Renderer do emblema/badge do pin tático.")]
        [SerializeField] private MeshRenderer? pinBadgeRenderer;

        [Header("Configuração de Cores")]
        [SerializeField] private Color defaultNeutralColor = new Color(0.68f, 0.68f, 0.72f, 1f);
        [SerializeField] private Color highlightTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color selectedTint = new Color(1f, 0.88f, 0.25f, 1f);

        // Estados internos
        private Color _ownerColor;
        private bool _isHighlighted;
        private bool _isSelected;
        private int _currentArmyCount = 1;
        private Material? _runtimeMaterial;
        private Material? _runtimePinMaterial;

        // Animação 3D física de elevação e pin
        private Vector3 _initialLocalPosition;
        private Vector3 _initialPinScale = Vector3.one;
        private float _currentElevation;
        private float _currentPinScale = 1f;

        // Propriedade estática para shaders URP (_BaseColor) e Built-in (_Color)
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessProp = Shader.PropertyToID("_Smoothness");

        #region Eventos
        public event Action<TerritoryView>? OnClicked;
        public event Action<TerritoryView>? OnHoverEnter;
        public event Action<TerritoryView>? OnHoverExit;
        #endregion

        #region Propriedades Públicas
        public string TerritoryId
        {
            get => territoryId;
            set => territoryId = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public Transform ArmySpawnPoint => armySpawnPoint != null ? armySpawnPoint : transform;
        public MeshRenderer? TerritoryRenderer => territoryRenderer;
        public Transform? TacticalPinRoot => tacticalPinRoot;
        public Color CurrentOwnerColor => _ownerColor;
        public int CurrentArmyCount => _currentArmyCount;
        public bool IsHighlighted => _isHighlighted;
        public bool IsSelected => _isSelected;
        #endregion

        private void Awake()
        {
            if (territoryRenderer == null)
            {
                territoryRenderer = GetComponent<MeshRenderer>() ?? GetComponentInChildren<MeshRenderer>();
            }

            if (tacticalPinRoot == null)
            {
                var pinChild = transform.Find("Pin_TacticalRoot");
                if (pinChild != null) tacticalPinRoot = pinChild;
            }

            if (armySpawnPoint == null)
            {
                var spawnChild = transform.Find("ArmySpawnPoint");
                if (spawnChild == null && tacticalPinRoot != null) spawnChild = tacticalPinRoot.Find("ArmySpawnPoint");
                armySpawnPoint = spawnChild != null ? spawnChild : transform;
            }

            if (armyCountText == null)
            {
                armyCountText = GetComponentInChildren<TMP_Text>();
            }

            // Garante cópia de material com shader válido
            if (territoryRenderer != null)
            {
                var mat = territoryRenderer.material;
                if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                {
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit") 
                                 ?? Shader.Find("Standard") 
                                 ?? Shader.Find("Diffuse");
                }

                if (mat.HasProperty(SmoothnessProp))
                {
                    mat.SetFloat(SmoothnessProp, 0.55f);
                }

                mat.enableInstancing = true;
                _runtimeMaterial = mat;
            }

            if (pinBadgeRenderer != null)
            {
                _runtimePinMaterial = pinBadgeRenderer.material;
                if (_runtimePinMaterial != null)
                {
                    _runtimePinMaterial.enableInstancing = true;
                }
            }

            _initialLocalPosition = transform.localPosition;
            if (tacticalPinRoot != null)
            {
                _initialPinScale = tacticalPinRoot.localScale;
            }

            _ownerColor = defaultNeutralColor;
            ApplyVisualState();
        }

        private void Update()
        {
            // Elevação física 3D suave da peça do país (estilo maquete / mapa de comando de referência)
            float targetElevation = _isSelected ? 0.38f : (_isHighlighted ? 0.12f : 0f);
            if (Mathf.Abs(_currentElevation - targetElevation) > 0.001f)
            {
                _currentElevation = Mathf.Lerp(_currentElevation, targetElevation, Time.deltaTime * 14f);
                var pos = _initialLocalPosition;
                pos.y += _currentElevation;
                transform.localPosition = pos;
            }

            // Escala dinâmica suave do pin/estandarte tático
            if (tacticalPinRoot != null)
            {
                float targetPinScale = _isSelected ? 1.25f : (_isHighlighted ? 1.10f : 1.0f);
                if (Mathf.Abs(_currentPinScale - targetPinScale) > 0.001f)
                {
                    _currentPinScale = Mathf.Lerp(_currentPinScale, targetPinScale, Time.deltaTime * 14f);
                    tacticalPinRoot.localScale = _initialPinScale * _currentPinScale;
                }
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            if (_runtimePinMaterial != null)
            {
                Destroy(_runtimePinMaterial);
                _runtimePinMaterial = null;
            }
        }

        /// <summary>
        /// Define a cor do jogador proprietário do território.
        /// </summary>
        public void SetOwnerColor(Color color)
        {
            _ownerColor = color;
            ApplyVisualState();
        }

        /// <summary>
        /// Atualiza o número de tropas exibido visualmente.
        /// </summary>
        public void SetArmyCount(int count)
        {
            _currentArmyCount = Mathf.Max(0, count);
            var str = _currentArmyCount.ToString();
            if (armyCountText != null)
            {
                armyCountText.text = str;
            }
            if (armyCountLegacyText != null)
            {
                armyCountLegacyText.text = str;
            }
        }

        /// <summary>
        /// Define o nome de exibição do território na interface visual.
        /// </summary>
        public void SetTerritoryName(string displayName)
        {
            if (territoryNameText != null)
            {
                territoryNameText.text = displayName;
            }
            if (territoryNameLegacyText != null)
            {
                territoryNameLegacyText.text = displayName;
            }
        }

        /// <summary>
        /// Ativa ou desativa o destaque visual de hover (passagem do mouse).
        /// </summary>
        public void SetHighlighted(bool highlight)
        {
            if (_isHighlighted == highlight) return;
            _isHighlighted = highlight;
            ApplyVisualState();
        }

        /// <summary>
        /// Ativa ou desativa a indicação de seleção ativa do território.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            ApplyVisualState();
        }

        /// <summary>
        /// Recalcula e aplica a cor resultante com base no estado atual (Owner, Hover, Selection),
        /// acentuando o brilho físico volumétrico e emissão suave nas bordas.
        /// </summary>
        private void ApplyVisualState()
        {
            if (_runtimeMaterial == null && territoryRenderer != null)
            {
                _runtimeMaterial = territoryRenderer.material;
                if (_runtimeMaterial != null)
                {
                    _runtimeMaterial.enableInstancing = true;
                }
            }

            if (_runtimeMaterial == null) return;

            Color targetColor;
            Color emissionColor = Color.black;

            if (_isSelected)
            {
                // Dourado tático misturado à cor do dono com emissão brilhante de borda (referência Argentina)
                targetColor = Color.Lerp(_ownerColor, selectedTint, 0.60f);
                emissionColor = new Color(0.75f, 0.65f, 0.22f, 1f);
            }
            else if (_isHighlighted)
            {
                // Brilho acetinado de seleção com clareamento suave
                targetColor = Color.Lerp(_ownerColor, highlightTint, 0.35f);
                emissionColor = new Color(0.30f, 0.32f, 0.36f, 1f);
            }
            else
            {
                targetColor = _ownerColor;
                emissionColor = Color.black;
            }

            if (_runtimeMaterial.HasProperty(BaseColorProp))
            {
                _runtimeMaterial.SetColor(BaseColorProp, targetColor);
            }
            else if (_runtimeMaterial.HasProperty(LegacyColorProp))
            {
                _runtimeMaterial.SetColor(LegacyColorProp, targetColor);
            }
            else
            {
                _runtimeMaterial.color = targetColor;
            }

            if (_runtimeMaterial.HasProperty(EmissionColorProp))
            {
                if (emissionColor != Color.black)
                {
                    _runtimeMaterial.EnableKeyword("_EMISSION");
                    _runtimeMaterial.SetColor(EmissionColorProp, emissionColor);
                }
                else
                {
                    _runtimeMaterial.SetColor(EmissionColorProp, Color.black);
                }
            }

            // Atualiza também o badge do pin tático se presente (brilha em branco/ouro quando selecionado)
            if (_runtimePinMaterial != null)
            {
                Color pinColor;
                Color pinEmission = Color.black;

                if (_isSelected)
                {
                    pinColor = Color.Lerp(_ownerColor, Color.white, 0.85f);
                    pinEmission = new Color(0.8f, 0.75f, 0.35f, 1f);
                }
                else if (_isHighlighted)
                {
                    pinColor = Color.Lerp(_ownerColor, Color.white, 0.40f);
                    pinEmission = new Color(0.2f, 0.2f, 0.2f, 1f);
                }
                else
                {
                    pinColor = Color.Lerp(_ownerColor, Color.white, 0.15f);
                }

                if (_runtimePinMaterial.HasProperty(BaseColorProp))
                    _runtimePinMaterial.SetColor(BaseColorProp, pinColor);
                else
                    _runtimePinMaterial.color = pinColor;

                if (_runtimePinMaterial.HasProperty(EmissionColorProp))
                {
                    if (pinEmission != Color.black)
                    {
                        _runtimePinMaterial.EnableKeyword("_EMISSION");
                        _runtimePinMaterial.SetColor(EmissionColorProp, pinEmission);
                    }
                    else
                    {
                        _runtimePinMaterial.SetColor(EmissionColorProp, Color.black);
                    }
                }
            }
        }

        #region Interação com o Mouse
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

        /// <summary>
        /// Permite simulação manual ou via Raycast externo de clique.
        /// </summary>
        public void TriggerClick()
        {
            OnClicked?.Invoke(this);
        }

        /// <summary>
        /// Permite simulação manual de hover externo.
        /// </summary>
        public void TriggerHover(bool isHovered)
        {
            SetHighlighted(isHovered);
            if (isHovered) OnHoverEnter?.Invoke(this);
            else OnHoverExit?.Invoke(this);
        }
        #endregion
    }
}

