#nullable enable
using System;
using UnityEngine;
using TMPro;
using War.Presentation.UI;

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

        [Tooltip("GameObject raiz contendo a plaqueta e texto do nome do país para controle de zoom.")]
        [SerializeField] private GameObject? nameTagRoot;

        [Tooltip("Renderer do emblema/badge do pin tático.")]
        [SerializeField] private MeshRenderer? pinBadgeRenderer;

        [Tooltip("Renderer do anel/borda do pin tático.")]
        [SerializeField] private MeshRenderer? pinRimRenderer;

        [Header("Configuração de Cores")]
        [SerializeField] private Color defaultNeutralColor = new Color(0.24f, 0.26f, 0.30f, 1f);
        [SerializeField] private Color highlightTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color selectedTint = new Color(1f, 0.88f, 0.25f, 1f);

        // Estados internos
        private Color _ownerColor;
        private bool _isHighlighted;
        private bool _isSelected;
        private bool _isDimmed;
        private int _currentArmyCount = 1;
        private MaterialPropertyBlock? _propertyBlock;
        private MaterialPropertyBlock? _pinPropertyBlock;
        private MaterialPropertyBlock? _rimPropertyBlock;

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
        public bool IsDimmed => _isDimmed;
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

            if (nameTagRoot == null && tacticalPinRoot != null)
            {
                var tagChild = tacticalPinRoot.Find("NameTag_Root");
                if (tagChild != null) nameTagRoot = tagChild.gameObject;
                else
                {
                    var plaqueChild = tacticalPinRoot.Find("Plaque_Background");
                    if (plaqueChild != null) nameTagRoot = plaqueChild.gameObject;
                }
            }

            if (pinRimRenderer == null && tacticalPinRoot != null)
            {
                var rimChild = tacticalPinRoot.Find("Pin_Rim") ?? tacticalPinRoot.Find("Pin_Pedestal");
                if (rimChild != null) pinRimRenderer = rimChild.GetComponent<MeshRenderer>();
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

            // Garante que o material compartilhado tenha GPU Instancing ativo
            if (territoryRenderer != null && territoryRenderer.sharedMaterial != null)
            {
                territoryRenderer.sharedMaterial.enableInstancing = true;
            }

            if (pinBadgeRenderer != null && pinBadgeRenderer.sharedMaterial != null)
            {
                pinBadgeRenderer.sharedMaterial.enableInstancing = true;
            }

            if (pinRimRenderer != null && pinRimRenderer.sharedMaterial != null)
            {
                pinRimRenderer.sharedMaterial.enableInstancing = true;
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
            if (_isHighlighted && GameHUD.IsModalOpen())
            {
                SetHighlighted(false);
                OnHoverExit?.Invoke(this);
            }

            // Elevação física 3D suave da peça do país (estilo relevo em tela de galeria)
            float targetElevation = _isSelected ? 0.32f : (_isHighlighted && !_isDimmed ? 0.15f : 0f);
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
                float targetPinScale = _isSelected ? 1.25f : (_isHighlighted ? 1.15f : 1.0f);
                if (Mathf.Abs(_currentPinScale - targetPinScale) > 0.001f)
                {
                    _currentPinScale = Mathf.Lerp(_currentPinScale, targetPinScale, Time.deltaTime * 14f);
                    tacticalPinRoot.localScale = _initialPinScale * _currentPinScale;
                }
            }

            // Controle de visibilidade do nome do país baseado no nível de zoom (como no WAR real):
            // O nome fica visível na distância padrão de jogo (<= 48f) ou quando passamos o mouse / selecionamos,
            // e oculta suavemente apenas no zoom out estratégico máximo (> 48f).
            if (nameTagRoot != null)
            {
                float camDist = War.Presentation.CameraControl.TacticalCameraController.CurrentDistance;
                bool shouldShowName = (camDist <= 48.0f) || _isHighlighted || _isSelected;
                if (nameTagRoot.activeSelf != shouldShowName)
                {
                    nameTagRoot.SetActive(shouldShowName);
                }
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
        /// Escurece o território quando outro território do tabuleiro está selecionado,
        /// criando efeito dramático de foco onde apenas o país selecionado sobressai.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            if (_isDimmed == dimmed) return;
            _isDimmed = dimmed;
            ApplyVisualState();
        }

        /// <summary>
        /// Recalcula e aplica a cor resultante com base no estado atual (Owner, Hover, Selection, Dimmed),
        /// mantendo a cor autêntica do país no destaque e escurecendo o restante do mapa.
        /// </summary>
        private void ApplyVisualState()
        {
            Color targetColor;
            Color emissionColor = Color.black;

            if (_isSelected)
            {
                targetColor = Color.Lerp(_ownerColor, Color.white, 0.20f);
                emissionColor = new Color(_ownerColor.r * 0.35f + 0.10f, _ownerColor.g * 0.35f + 0.10f, _ownerColor.b * 0.35f + 0.10f, 1f);
            }
            else if (_isHighlighted)
            {
                // Hover e Destaque de Continente: Iluminação tática rica e brilho na cor autêntica
                targetColor = Color.Lerp(_ownerColor, Color.white, 0.28f);
                emissionColor = new Color(_ownerColor.r * 0.45f + 0.12f, _ownerColor.g * 0.45f + 0.12f, _ownerColor.b * 0.45f + 0.12f, 1f);
            }
            else if (_isDimmed)
            {
                // Escurece o restante do mapa para criar contraste dramático de foco
                targetColor = new Color(_ownerColor.r * 0.32f, _ownerColor.g * 0.32f, _ownerColor.b * 0.32f, 1f);
                emissionColor = Color.black;
            }
            else
            {
                targetColor = _ownerColor;
                emissionColor = Color.black;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _propertyBlock.SetColor(BaseColorProp, targetColor);
            _propertyBlock.SetColor(LegacyColorProp, targetColor);
            _propertyBlock.SetColor(EmissionColorProp, emissionColor);

            if (territoryRenderer != null)
            {
                territoryRenderer.SetPropertyBlock(_propertyBlock);
            }

            // Atualiza o token/bolinha tática com alto contraste:
            // 1) O disco central onde fica o número é Preto/Grafite Profundo (#13161F) para contraste máximo
            if (pinBadgeRenderer != null)
            {
                if (_pinPropertyBlock == null)
                {
                    _pinPropertyBlock = new MaterialPropertyBlock();
                }

                Color badgeBg = _isSelected 
                    ? new Color(0.18f, 0.22f, 0.28f, 1f) 
                    : (_isDimmed ? new Color(0.06f, 0.07f, 0.09f, 1f) : new Color(0.10f, 0.12f, 0.16f, 1f));
                Color badgeEmission = _isSelected ? new Color(0.25f, 0.22f, 0.10f, 1f) : Color.black;

                _pinPropertyBlock.SetColor(BaseColorProp, badgeBg);
                _pinPropertyBlock.SetColor(LegacyColorProp, badgeBg);
                _pinPropertyBlock.SetColor(EmissionColorProp, badgeEmission);
                pinBadgeRenderer.SetPropertyBlock(_pinPropertyBlock);
            }

            // 2) O anel/borda externa do token recebe a cor vibrante do jogador (separação perfeita)
            if (pinRimRenderer != null)
            {
                if (_rimPropertyBlock == null)
                {
                    _rimPropertyBlock = new MaterialPropertyBlock();
                }

                Color rimColor = _isSelected 
                    ? Color.Lerp(_ownerColor, Color.white, 0.40f) 
                    : (_isHighlighted 
                        ? Color.Lerp(_ownerColor, Color.white, 0.60f) 
                        : (_isDimmed ? _ownerColor * 0.45f : _ownerColor));
                Color rimEmission = _isSelected 
                    ? rimColor * 0.45f 
                    : (_isHighlighted ? rimColor * 0.70f : Color.black);

                _rimPropertyBlock.SetColor(BaseColorProp, rimColor);
                _rimPropertyBlock.SetColor(LegacyColorProp, rimColor);
                _rimPropertyBlock.SetColor(EmissionColorProp, rimEmission);
                pinRimRenderer.SetPropertyBlock(_rimPropertyBlock);
            }

            // Número da tropa sempre BRANCO PURO e de altíssimo contraste contra o fundo grafite
            Color textCol = _isDimmed && !_isSelected ? new Color(0.65f, 0.65f, 0.65f, 0.8f) : Color.white;
            if (armyCountText != null) armyCountText.color = textCol;
            if (armyCountLegacyText != null) armyCountLegacyText.color = textCol;
        }

        #region Interação com o Mouse
        private void OnMouseEnter()
        {
            if (GameHUD.IsModalOpen()) return;
            SetHighlighted(true);
            OnHoverEnter?.Invoke(this);
        }

        private void OnMouseExit()
        {
            if (_isHighlighted)
            {
                SetHighlighted(false);
                OnHoverExit?.Invoke(this);
            }
        }

        private void OnMouseDown()
        {
            if (GameHUD.IsModalOpen()) return;
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

