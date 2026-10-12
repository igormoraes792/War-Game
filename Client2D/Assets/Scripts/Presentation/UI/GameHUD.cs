#nullable enable
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using War.Core.Game;

namespace War.Presentation.UI
{
    /// <summary>
    /// Gerenciador principal da interface em tela (HUD uGUI) para a partida de WAR 2D.
    /// Exibe informações de jogador ativo, fase da rodada, tropas de reforço,
    /// controle de turnos/fases, modal de missão e barra de status em tempo real.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameHUD : MonoBehaviour
    {
        [Header("Topo - Jogador Ativo")]
        [SerializeField] private Image? playerColorBadge;
        [SerializeField] private TMP_Text? playerNameText;

        [Header("Topo - Fase Atual")]
        [SerializeField] private TMP_Text? phaseNameText;
        [SerializeField] private TMP_Text? phaseInstructionText;

        [Header("Topo - Tropas de Reforço")]
        [SerializeField] private GameObject? armiesBadgeRoot;
        [SerializeField] private TMP_Text? armiesToDeployText;

        [Header("Controles e Ações")]
        [SerializeField] private Button? advancePhaseButton;
        [SerializeField] private TMP_Text? advancePhaseButtonText;
        [SerializeField] private Button? myMissionButton;

        [Header("Barra de Notificação e Status")]
        [SerializeField] private GameObject? statusBarRoot;
        [SerializeField] private TMP_Text? statusText;

        [Header("Fallback Text (UnityEngine.UI)")]
        [SerializeField] private Text? legacyPlayerNameText;
        [SerializeField] private Text? legacyPhaseNameText;
        [SerializeField] private Text? legacyPhaseInstructionText;
        [SerializeField] private Text? legacyArmiesToDeployText;
        [SerializeField] private Text? legacyAdvancePhaseButtonText;
        [SerializeField] private Text? legacyStatusMessageText;

        private Coroutine? _statusFadeRoutine;

        public event Action? OnAdvancePhaseClicked;
        public event Action? OnOpenMissionClicked;

        private void Awake()
        {
            if (advancePhaseButton != null)
            {
                advancePhaseButton.onClick.AddListener(HandleAdvancePhase);
            }

            if (myMissionButton != null)
            {
                myMissionButton.onClick.AddListener(HandleOpenMission);
            }
        }

        private void OnDestroy()
        {
            if (advancePhaseButton != null)
            {
                advancePhaseButton.onClick.RemoveListener(HandleAdvancePhase);
            }

            if (myMissionButton != null)
            {
                myMissionButton.onClick.RemoveListener(HandleOpenMission);
            }
        }

        private void HandleAdvancePhase()
        {
            OnAdvancePhaseClicked?.Invoke();
        }

        private void HandleOpenMission()
        {
            OnOpenMissionClicked?.Invoke();
        }

        /// <summary>
        /// Atualiza os elementos de status do HUD com os dados do jogador ativo e da fase atual.
        /// </summary>
        public void UpdateHUD(string playerName, Color playerColor, TurnPhase phase, int armiesToDeploy)
        {
            if (playerColorBadge != null)
            {
                playerColorBadge.color = playerColor;
            }

            SetLabel(playerNameText, legacyPlayerNameText, playerName);

            string phaseTitle;
            string phaseDesc;
            string btnLabel;

            switch (phase)
            {
                case TurnPhase.Reinforcement:
                    phaseTitle = "FASE DE REFORÇO";
                    phaseDesc = "Clique em um território seu para escolher quantas tropas alocar.";
                    btnLabel = "AVANÇAR PARA ATAQUE ▸";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(true);
                    break;

                case TurnPhase.Attack:
                    phaseTitle = "FASE DE ATAQUE";
                    phaseDesc = "Selecione um território seu (com 2+ tropas) e um território inimigo vizinho.";
                    btnLabel = "AVANÇAR PARA FORTIFICAÇÃO ▸";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(false);
                    break;

                case TurnPhase.Fortification:
                    phaseTitle = "FASE DE FORTIFICAÇÃO";
                    phaseDesc = "Remaneje tropas entre territórios conectados seus ou encerre a rodada.";
                    btnLabel = "ENCERRAR TURNO ⮑";
                    if (armiesBadgeRoot != null) armiesBadgeRoot.SetActive(false);
                    break;

                default:
                    phaseTitle = "FIM DE JOGO";
                    phaseDesc = "Partida encerrada!";
                    btnLabel = "FIM";
                    break;
            }

            SetLabel(phaseNameText, legacyPhaseNameText, phaseTitle);
            SetLabel(phaseInstructionText, legacyPhaseInstructionText, phaseDesc);
            SetLabel(advancePhaseButtonText, legacyAdvancePhaseButtonText, btnLabel);
            SetLabel(armiesToDeployText, legacyArmiesToDeployText, $"Tropas para posicionar: {armiesToDeploy}");
        }

        /// <summary>
        /// Exibe uma mensagem transitória ou evento importante na barra de status inferior.
        /// </summary>
        public void SetStatusMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (statusBarRoot != null)
            {
                statusBarRoot.SetActive(true);
            }

            SetLabel(statusText, legacyStatusMessageText, message);

            if (_statusFadeRoutine != null)
            {
                StopCoroutine(_statusFadeRoutine);
            }
            _statusFadeRoutine = StartCoroutine(AutoClearStatusMessage(5.5f));
        }

        private IEnumerator AutoClearStatusMessage(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (statusBarRoot != null)
            {
                statusBarRoot.SetActive(false);
            }
        }

        private static void SetLabel(TMP_Text? tmp, Text? legacy, string value)
        {
            if (tmp != null) tmp.text = value;
            if (legacy != null) legacy.text = value;
        }
    }
}
