#nullable enable
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using War.Core.Game;
using War.Core.Models;
using War.Core.Objectives;
using War.Presentation.Game;

namespace War.Presentation.UI
{
    /// <summary>
    /// Modal pop-up que exibe o Objetivo Secreto militar do jogador ativo,
    /// incluindo título, descrição estratégica e acompanhamento detalhado de progresso.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveModalUI : MonoBehaviour
    {
        [Header("Contêiner Principal")]
        [Tooltip("Objeto raiz do modal que é ativado/desativado.")]
        [SerializeField] private GameObject? modalRoot;

        [Header("Elementos de Interface")]
        [SerializeField] private TMP_Text? titleText;
        [SerializeField] private TMP_Text? descriptionText;
        [SerializeField] private TMP_Text? playerNameText;
        [SerializeField] private TMP_Text? progressText;
        [SerializeField] private Image? playerColorBadge;
        [Tooltip("Botão [Fechar Missão] inferior")]
        [SerializeField] private Button? closeButton;
        [Tooltip("Botão [X] superior")]
        [SerializeField] private Button? closeTopButton;

        [Header("Fallback Text (UnityEngine.UI)")]
        [SerializeField] private Text? legacyTitleText;
        [SerializeField] private Text? legacyDescriptionText;
        [SerializeField] private Text? legacyPlayerNameText;
        [SerializeField] private Text? legacyProgressText;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        private void Awake()
        {
            if (modalRoot == null)
            {
                modalRoot = gameObject;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            if (closeTopButton != null)
            {
                closeTopButton.onClick.AddListener(Close);
            }
        }

        private void Update()
        {
            if (modalRoot != null && modalRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
            if (closeTopButton != null)
            {
                closeTopButton.onClick.RemoveListener(Close);
            }
        }

        /// <summary>
        /// Exibe o objetivo secreto do jogador especificado.
        /// </summary>
        public void ShowObjective(Player player, GameState? gameState = null)
        {
            if (player == null) return;

            SetLabel(titleText, legacyTitleText, player.SecretObjective?.Title ?? "Objetivo Não Atribuído");
            SetLabel(descriptionText, legacyDescriptionText, player.SecretObjective?.Description ?? "Aguardando distribuição de missões secretas pelo comando central.");
            SetLabel(playerNameText, legacyPlayerNameText, $"Missão de: {player.Name}");

            if (playerColorBadge != null)
            {
                playerColorBadge.color = GamePresenter.ResolvePlayerColor(player.Color);
            }

            var progressStr = FormatProgress(player, gameState);
            SetLabel(progressText, legacyProgressText, progressStr);

            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Alterna a visibilidade do modal.
        /// </summary>
        public void ToggleObjective(Player player, GameState? gameState = null)
        {
            if (modalRoot != null && modalRoot.activeSelf)
            {
                Close();
            }
            else
            {
                ShowObjective(player, gameState);
            }
        }

        /// <summary>
        /// Fecha e esconde o modal.
        /// </summary>
        public void Close()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private static string FormatProgress(Player player, GameState? gameState)
        {
            if (player.SecretObjective == null)
            {
                return "Status: Aguardando início formal da partida.";
            }

            if (gameState == null)
            {
                return "Status: Em andamento.";
            }

            var objective = player.SecretObjective;
            var isFulfilled = objective.CheckVictoryCondition(gameState, player);

            if (isFulfilled)
            {
                return "★ STATUS: CONDIÇÃO DE VITÓRIA ATINGIDA! ★\nAvance o turno ou conquiste seu objetivo para consagrar a vitória!";
            }

            if (objective is TerritoryCountObjective territoryCountObjective)
            {
                var ownedCount = gameState.GetTerritoryCount(player.Id);
                var required = territoryCountObjective.RequiredTerritories;
                var pct = Mathf.Clamp01((float)ownedCount / required) * 100f;
                return $"Progresso de Territórios:\n• Dominados: {ownedCount} / {required} ({pct:0}%)";
            }

            if (objective is TerritoriesWithArmiesObjective armiesObjective)
            {
                var minArmies = armiesObjective.MinimumArmiesPerTerritory;
                var reqCount = armiesObjective.RequiredTerritories;
                var matchingCount = gameState.Territories.Values.Count(t =>
                    string.Equals(t.OwnerId, player.Id, StringComparison.OrdinalIgnoreCase) &&
                    t.Armies >= minArmies);

                return $"Progresso de Territórios Fortificados:\n• Territórios com {minArmies}+ exércitos: {matchingCount} / {reqCount}";
            }

            if (objective is ContinentConquestObjective continentObjective)
            {
                var lines = new System.Text.StringBuilder("Progresso de Continentes:\n");
                foreach (var reqContinent in continentObjective.RequiredContinents)
                {
                    var controlled = gameState.IsContinentControlledBy(reqContinent, player.Id);
                    lines.AppendLine($"• {reqContinent}: {(controlled ? "✓ DOMINADO" : "✗ Pendente")}");
                }

                if (continentObjective.AdditionalAnyContinentCount > 0)
                {
                    var all = Continent.GetAll().Select(c => c.Type);
                    var otherCount = all
                        .Where(c => !continentObjective.RequiredContinents.Contains(c))
                        .Count(c => gameState.IsContinentControlledBy(c, player.Id));

                    lines.AppendLine($"• Mais {continentObjective.AdditionalAnyContinentCount} continente(s) à sua escolha: {otherCount} dominado(s)");
                }

                return lines.ToString().TrimEnd();
            }

            if (objective is EliminateColorObjective eliminateObjective)
            {
                var target = gameState.GetPlayerByColor(eliminateObjective.TargetColor);
                if (target == null || target.Id == player.Id)
                {
                    var count = gameState.GetTerritoryCount(player.Id);
                    return $"Meta Alternativa (Alvo não ativo):\n• Conquistar 24 territórios: {count} / 24";
                }

                if (target.IsEliminated)
                {
                    if (string.Equals(target.EliminatedByPlayerId, player.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Alvo Eliminado por você!\n• Exército {eliminateObjective.TargetColor} destruído!";
                    }
                    var count = gameState.GetTerritoryCount(player.Id);
                    return $"Alvo eliminado por outro jogador (Meta alternativa ativa):\n• Conquistar 24 territórios: {count} / 24";
                }

                var targetTerritoryCount = gameState.GetTerritoryCount(target.Id);
                return $"Alvo Primário: Exército {eliminateObjective.TargetColor}\n• Restam {targetTerritoryCount} territórios do inimigo no mapa.";
            }

            return "Status: Em andamento militar.";
        }

        private static void SetLabel(TMP_Text? tmp, Text? legacy, string value)
        {
            if (tmp != null) tmp.text = value;
            if (legacy != null) legacy.text = value;
        }
    }
}
