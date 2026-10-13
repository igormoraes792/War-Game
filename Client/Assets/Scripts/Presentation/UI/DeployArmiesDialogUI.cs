#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using War.Core.Models;
using War.Core.Game;
using War.Presentation.Game;

namespace War.Presentation.UI
{
    /// <summary>
    /// Mini HUD modal flutuante para escolha da quantidade de tropas a posicionar
    /// em um território durante a fase de Reforço (Reinforcement).
    /// </summary>
    [DisallowMultipleComponent]
    public class DeployArmiesDialogUI : MonoBehaviour
    {
        [Header("Contêiner Principal")]
        [Tooltip("Objeto raiz do modal que é ativado/desativado.")]
        [SerializeField] private GameObject? modalRoot;

        [Header("Textos Principais")]
        [SerializeField] private TMP_Text? titleText;
        [SerializeField] private TMP_Text? territoryInfoText;
        [SerializeField] private TMP_Text? availableArmiesText;
        [SerializeField] private TMP_Text? countText;

        [Header("Indicador Visual de Cor/Jogador")]
        [SerializeField] private Image? territoryColorBadge;

        [Header("Botões de Controle de Quantidade")]
        [SerializeField] private Button? decreaseButton;
        [SerializeField] private Button? increaseButton;
        [SerializeField] private Button? maxButton;

        [Header("Botões de Ação")]
        [SerializeField] private Button? confirmButton;
        [SerializeField] private Button? cancelButton;
        [SerializeField] private Button? closeButton;

        [Header("Fallback Text (UnityEngine.UI)")]
        [SerializeField] private Text? legacyTitleText;
        [SerializeField] private Text? legacyTerritoryInfoText;
        [SerializeField] private Text? legacyAvailableArmiesText;
        [SerializeField] private Text? legacyCountText;

        // Estado interno do diálogo
        private Territory? _targetTerritory;
        private int _maxAvailableArmies;
        private int _selectedCount = 1;
        private Action<int>? _onConfirm;
        private Action? _onCancel;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;
        public Territory? TargetTerritory => _targetTerritory;
        public int SelectedCount => _selectedCount;

        private void Awake()
        {
            if (modalRoot == null)
            {
                modalRoot = gameObject;
            }

            decreaseButton?.onClick.AddListener(OnDecreaseClicked);
            increaseButton?.onClick.AddListener(OnIncreaseClicked);
            maxButton?.onClick.AddListener(OnMaxClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
            closeButton?.onClick.AddListener(OnCancelClicked);
        }

        private void Update()
        {
            if (modalRoot != null && modalRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancelClicked();
            }
        }

        private void OnDestroy()
        {
            decreaseButton?.onClick.RemoveListener(OnDecreaseClicked);
            increaseButton?.onClick.RemoveListener(OnIncreaseClicked);
            maxButton?.onClick.RemoveListener(OnMaxClicked);
            confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            cancelButton?.onClick.RemoveListener(OnCancelClicked);
            closeButton?.onClick.RemoveListener(OnCancelClicked);
        }

        /// <summary>
        /// Abre o diálogo configurado para o território alvo e quantidade de tropas disponíveis.
        /// </summary>
        /// <param name="territory">Território destino do reforço.</param>
        /// <param name="availableArmies">Saldo de tropas de reforço ainda disponíveis para o jogador.</param>
        /// <param name="onConfirm">Callback invocado quando o jogador confirma a alocação com o número de tropas.</param>
        /// <param name="onCancel">Callback invocado quando o diálogo é cancelado ou fechado.</param>
        public void Open(Territory territory, int availableArmies, Action<int> onConfirm, Action? onCancel = null)
        {
            if (territory == null) return;

            _targetTerritory = territory;
            _maxAvailableArmies = Mathf.Max(0, availableArmies);
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // Define quantidade inicial como 1 (ou 0 se não houver tropas)
            _selectedCount = _maxAvailableArmies > 0 ? 1 : 0;

            // Configura textos informativos
            SetLabel(titleText, legacyTitleText, $"Posicionar Tropas em {territory.Name}");
            SetLabel(territoryInfoText, legacyTerritoryInfoText, $"{territory.Continent} • Tropas atuais no local: {territory.Armies}");
            SetLabel(availableArmiesText, legacyAvailableArmiesText, $"Tropas de reforço disponíveis: {_maxAvailableArmies}");

            if (territoryColorBadge != null && !string.IsNullOrEmpty(territory.OwnerId))
            {
                // Tenta extrair a cor do OwnerId (ex: player_red -> Red)
                if (Enum.TryParse<PlayerColor>(territory.OwnerId.Replace("player_", ""), true, out var color))
                {
                    territoryColorBadge.color = GamePresenter.ResolvePlayerColor(color);
                }
            }

            UpdateCounterVisuals();

            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Fecha o diálogo sem invocar confirmação.
        /// </summary>
        public void Close()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        #region Controles de Quantidade
        private void OnDecreaseClicked()
        {
            if (_selectedCount > 1)
            {
                _selectedCount--;
                UpdateCounterVisuals();
            }
        }

        private void OnIncreaseClicked()
        {
            if (_selectedCount < _maxAvailableArmies)
            {
                _selectedCount++;
                UpdateCounterVisuals();
            }
        }

        private void OnMaxClicked()
        {
            if (_maxAvailableArmies > 0)
            {
                _selectedCount = _maxAvailableArmies;
                UpdateCounterVisuals();
            }
        }

        private void OnConfirmClicked()
        {
            if (_selectedCount <= 0 || _selectedCount > _maxAvailableArmies) return;

            var chosenCount = _selectedCount;
            var confirmCallback = _onConfirm;

            Close();

            confirmCallback?.Invoke(chosenCount);
        }

        private void OnCancelClicked()
        {
            var cancelCallback = _onCancel;
            Close();
            cancelCallback?.Invoke();
        }

        private void UpdateCounterVisuals()
        {
            SetLabel(countText, legacyCountText, _selectedCount.ToString());

            if (decreaseButton != null)
            {
                decreaseButton.interactable = _selectedCount > 1;
            }

            if (increaseButton != null)
            {
                increaseButton.interactable = _selectedCount < _maxAvailableArmies;
            }

            if (maxButton != null)
            {
                maxButton.interactable = _selectedCount < _maxAvailableArmies && _maxAvailableArmies > 0;
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = _selectedCount > 0 && _selectedCount <= _maxAvailableArmies;
            }
        }
        #endregion

        private static void SetLabel(TMP_Text? tmp, Text? legacy, string value)
        {
            if (tmp != null) tmp.text = value;
            if (legacy != null) legacy.text = value;
        }
    }
}

