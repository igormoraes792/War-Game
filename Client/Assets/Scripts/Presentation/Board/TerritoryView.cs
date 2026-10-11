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
        [Tooltip("Texto para exibir a quantidade de exércitos no território.")]
        [SerializeField] private TMP_Text? armyCountText;

        [Tooltip("Texto opcional para exibir o nome do território.")]
        [SerializeField] private TMP_Text? territoryNameText;

        [Header("Configuração de Cores")]
        [SerializeField] private Color defaultNeutralColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color highlightTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color selectedTint = new Color(1f, 0.92f, 0.2f, 1f);

        // Estados internos
        private Color _ownerColor;
        private bool _isHighlighted;
        private bool _isSelected;
        private int _currentArmyCount = 1;
        private Material? _runtimeMaterial;

        // Propriedade estática para shaders URP (_BaseColor) e Built-in (_Color)
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProp = Shader.PropertyToID("_Color");

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

            if (armySpawnPoint == null)
            {
                var spawnChild = transform.Find("ArmySpawnPoint");
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
                _runtimeMaterial = mat;
            }

            _ownerColor = defaultNeutralColor;
            ApplyVisualState();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
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
            if (armyCountText != null)
            {
                armyCountText.text = _currentArmyCount.ToString();
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
        /// Recalcula e aplica a cor resultante com base no estado atual (Owner, Hover, Selection).
        /// </summary>
        private void ApplyVisualState()
        {
            if (_runtimeMaterial == null && territoryRenderer != null)
            {
                _runtimeMaterial = territoryRenderer.material;
            }

            if (_runtimeMaterial == null) return;

            Color targetColor;

            if (_isSelected)
            {
                // Amarelo/Dourado misturado à cor do dono para claro destaque de seleção
                targetColor = Color.Lerp(_ownerColor, selectedTint, 0.65f);
            }
            else if (_isHighlighted)
            {
                // Clareia a cor base em 35% ao passar o mouse
                targetColor = Color.Lerp(_ownerColor, highlightTint, 0.35f);
            }
            else
            {
                targetColor = _ownerColor;
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

