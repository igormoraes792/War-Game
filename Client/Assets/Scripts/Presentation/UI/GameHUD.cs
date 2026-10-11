#nullable enable
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using War.Core.Game;
using War.Presentation.Game;

namespace War.Presentation.UI
{
    /// <summary>
    /// Gerenciador principal da interface em tela (HUD uGUI) para a partida de WAR.
    /// Exibe informações de jogador ativo, fase da rodada, tropas de reforço,
    /// controle de turnos/fases, modal de missão e barra de status em tempo real.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameHUD : MonoBehaviour
    {
        [Header("Conexão Core / MVP")]
        [SerializeField] private GamePresenter? presenter;

        [Header("Modais da Interface")]
        [SerializeField] private ObjectiveModalUI? objectiveModal;
        [SerializeField] private CombatPanelUI? combatPanel;
        [SerializeField] private DeployArmiesDialogUI? deployDialog;

        [Header("Topo - Jogador Ativo")]
        [SerializeField] private Image? playerColorBadge;
        [SerializeField] private TMP_Text? playerNameText;

        [Header("Topo - Fase Atual")]
        [SerializeField] private TMP_Text? phaseNameText;
        [SerializeField] private TMP_Text? phaseInstructionText;

        [Header("Topo - Reforços / Tropas")]
        [SerializeField] private GameObject? armiesBadgeRoot;
        [SerializeField] private TMP_Text? armiesToDeployText;

        [Header("Topo - Botões de Ação")]
        [SerializeField] private Button? advancePhaseButton;
        [SerializeField] private TMP_Text? advancePhaseButtonText;
        [SerializeField] private Button? myMissionButton;

        [Header("Inferior - Barra de Status")]
        [SerializeField] private GameObject? statusBarRoot;
        [SerializeField] private TMP_Text? statusMessageText;

        [Header("Fallback Text (UnityEngine.UI)")]
        [SerializeField] private Text? legacyPlayerNameText;
        [SerializeField] private Text? legacyPhaseNameText;
        [SerializeField] private Text? legacyPhaseInstructionText;
        [SerializeField] private Text? legacyArmiesToDeployText;
        [SerializeField] private Text? legacyAdvancePhaseButtonText;
        [SerializeField] private Text? legacyStatusMessageText;

        private Coroutine? _statusFadeRoutine;

        public DeployArmiesDialogUI? DeployDialog => deployDialog;

        private void Awake()
        {
            if (presenter == null)
            {
                presenter = FindFirstObjectByType<GamePresenter>();
            }

            if (objectiveModal == null)
            {
                objectiveModal = FindFirstObjectByType<ObjectiveModalUI>();
            }

            if (combatPanel == null)
            {
                combatPanel = FindFirstObjectByType<CombatPanelUI>();
            }

            if (deployDialog == null)
            {
                deployDialog = FindFirstObjectByType<DeployArmiesDialogUI>();
            }

            if (advancePhaseButton != null)
            {
                advancePhaseButton.onClick.AddListener(OnAdvancePhaseClicked);
            }

            if (myMissionButton != null)
            {
                myMissionButton.onClick.AddListener(OnMyMissionClicked);
            }
        }

        private void Start()
        {
            if (presenter != null)
            {
                BindPresenter(presenter);
            }
        }

        private void OnDestroy()
        {
            UnbindPresenter();

            if (advancePhaseButton != null)
            {
                advancePhaseButton.onClick.RemoveListener(OnAdvancePhaseClicked);
            }

            if (myMissionButton != null)
            {
                myMissionButton.onClick.RemoveListener(OnMyMissionClicked);
            }
        }

        /// <summary>
        /// Conecta o HUD aos eventos do GamePresenter.
        /// </summary>
        public void BindPresenter(GamePresenter newPresenter)
        {
            UnbindPresenter();

            presenter = newPresenter;

            if (presenter != null)
            {
                presenter.OnPhaseChanged += HandlePhaseChanged;
                presenter.OnGameStatusMessage += HandleGameStatusMessage;
                presenter.OnPlayerArmiesChanged += HandlePlayerArmiesChanged;
                presenter.OnGameOver += HandleGameOver;

                if (presenter.ActivePlayer != null)
                {
                    HandlePhaseChanged(presenter.CurrentPhase, presenter.ActivePlayer);
                    HandlePlayerArmiesChanged(presenter.ActivePlayer);
                }
            }
        }

        private void UnbindPresenter()
        {
            if (presenter != null)
            {
                presenter.OnPhaseChanged -= HandlePhaseChanged;
                presenter.OnGameStatusMessage -= HandleGameStatusMessage;
                presenter.OnPlayerArmiesChanged -= HandlePlayerArmiesChanged;
                presenter.OnGameOver -= HandleGameOver;
            }
        }

        #region Tratamento de Ações do Usuário
        private void OnAdvancePhaseClicked()
        {
            if (presenter == null) return;

            if (presenter.CurrentPhase == TurnPhase.Fortification)
            {
                presenter.EndTurn();
            }
            else
            {
                presenter.AdvancePhase();
            }
        }

        private void OnMyMissionClicked()
        {
            if (objectiveModal == null || presenter == null) return;

            var activePlayer = presenter.ActivePlayer;
            if (activePlayer != null)
            {
                objectiveModal.ToggleObjective(activePlayer, presenter.GameState);
            }
        }
        #endregion

        #region Tratamento de Eventos do Presenter
        private void HandlePhaseChanged(TurnPhase phase, Player activePlayer)
        {
            // 1. Atualiza dados do jogador ativo
            var playerColor = GamePresenter.ResolvePlayerColor(activePlayer.Color);
            if (playerColorBadge != null)
            {
                playerColorBadge.color = playerColor;
            }

            SetLabel(playerNameText, legacyPlayerNameText, activePlayer.Name);

            // 2. Atualiza fase e instruções
            string phaseTitle;
            string phaseDesc;
            string buttonLabel;

            switch (phase)
            {
                case TurnPhase.Reinforcement:
                    phaseTitle = "FASE DE REFORÇO";
                    phaseDesc = "Clique em um território seu para escolher quantas tropas alocar.";
                    buttonLabel = "Avançar para Ataque";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(true);
                    break;

                case TurnPhase.Attack:
                    deployDialog?.Close();
                    phaseTitle = "FASE DE ATAQUE";
                    phaseDesc = "Selecione um território seu (com 2+ tropas) e um território inimigo vizinho.";
                    buttonLabel = "Avançar para Fortificação";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(false);
                    break;

                case TurnPhase.Fortification:
                    deployDialog?.Close();
                    phaseTitle = "FASE DE FORTIFICAÇÃO";
                    phaseDesc = "Remaneje tropas entre territórios conectados seus ou encerre a rodada.";
                    buttonLabel = "Encerrar Turno";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(false);
                    break;

                case TurnPhase.GameOver:
                    deployDialog?.Close();
                    phaseTitle = "FIM DE JOGO";
                    phaseDesc = "Partida encerrada!";
                    buttonLabel = "Fim de Jogo";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(false);
                    if (advancePhaseButton != null) advancePhaseButton.interactable = false;
                    break;

                default:
                    phaseTitle = phase.ToString().ToUpperInvariant();
                    phaseDesc = "Aguardando próxima ação...";
                    buttonLabel = "Avançar";
                    break;
            }

            SetLabel(phaseNameText, legacyPhaseNameText, phaseTitle);
            SetLabel(phaseInstructionText, legacyPhaseInstructionText, phaseDesc);
            SetLabel(advancePhaseButtonText, legacyAdvancePhaseButtonText, buttonLabel);

            // 3. Atualiza tropas de reforço
            HandlePlayerArmiesChanged(activePlayer);
        }

        private void HandlePlayerArmiesChanged(Player player)
        {
            var text = $"Tropas para posicionar: {player.ArmiesToDeploy}";
            SetLabel(armiesToDeployText, legacyArmiesToDeployText, text);
        }

        private void HandleGameStatusMessage(string message)
        {
            SetLabel(statusMessageText, legacyStatusMessageText, message);

            if (statusBarRoot != null && !statusBarRoot.activeSelf)
            {
                statusBarRoot.SetActive(true);
            }

            if (_statusFadeRoutine != null)
            {
                StopCoroutine(_statusFadeRoutine);
            }
            _statusFadeRoutine = StartCoroutine(AnimateStatusPulse());
        }

        private IEnumerator AnimateStatusPulse()
        {
            if (statusMessageText != null)
            {
                var originalColor = statusMessageText.color;
                var flashColor = new Color(1f, 0.95f, 0.5f, 1f);

                for (var t = 0f; t < 0.25f; t += Time.deltaTime)
                {
                    statusMessageText.color = Color.Lerp(originalColor, flashColor, t / 0.25f);
                    yield return null;
                }
                for (var t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    statusMessageText.color = Color.Lerp(flashColor, originalColor, t / 0.35f);
                    yield return null;
                }
                statusMessageText.color = originalColor;
            }
        }

        private void HandleGameOver(Player winner)
        {
            if (advancePhaseButton != null)
            {
                advancePhaseButton.interactable = false;
            }

            HandleGameStatusMessage($"★ VITÓRIA DEFINITIVA! O vencedor da partida é {winner.Name}! ★");
        }
        #endregion

        private static void SetLabel(TMP_Text? tmp, Text? legacy, string value)
        {
            if (tmp != null) tmp.text = value;
            if (legacy != null) legacy.text = value;
        }
    }
}
