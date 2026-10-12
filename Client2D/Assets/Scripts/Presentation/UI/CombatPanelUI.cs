#nullable enable
using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using War.Core.Combat;
using War.Core.Models;
using War.Presentation.Game;

namespace War.Presentation.UI
{
    /// <summary>
    /// Modal de resolução de combate com visualização detalhada de dados de ataque (vermelho)
    /// e defesa (amarelo), comparações par a par, baixas militares e alerta de conquista.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatPanelUI : MonoBehaviour
    {
        [Header("Contêiner Principal")]
        [Tooltip("Objeto raiz do painel que é ligado/desligado.")]
        [SerializeField] private GameObject? modalRoot;

        [Header("Territórios e Cores")]
        [SerializeField] private TMP_Text? attackerTerritoryText;
        [SerializeField] private Image? attackerColorBadge;
        [SerializeField] private TMP_Text? defenderTerritoryText;
        [SerializeField] private Image? defenderColorBadge;

        [Header("Dados do Atacante (Vermelhos)")]
        [Tooltip("Até 3 caixas de dados vermelhas.")]
        [SerializeField] private GameObject[] attackerDiceBoxes = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text[] attackerDiceTexts = Array.Empty<TMP_Text>();

        [Header("Dados do Defensor (Amarelos)")]
        [Tooltip("Até 3 caixas de dados amarelas.")]
        [SerializeField] private GameObject[] defenderDiceBoxes = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text[] defenderDiceTexts = Array.Empty<TMP_Text>();

        [Header("Comparações e Resumo")]
        [SerializeField] private TMP_Text? comparisonsText;
        [SerializeField] private TMP_Text? lossesSummaryText;

        [Header("Banner de Conquista")]
        [SerializeField] private GameObject? conquestBanner;
        [SerializeField] private TMP_Text? conquestBannerText;

        [Header("Botões de Ação")]
        [SerializeField] private Button? closeButton;

        [Header("Conexão com Presenter")]
        [SerializeField] private GamePresenter? presenter;

        [Header("Fallback Text (UnityEngine.UI)")]
        [SerializeField] private Text? legacyAttackerTerritoryText;
        [SerializeField] private Text? legacyDefenderTerritoryText;
        [SerializeField] private Text? legacyComparisonsText;
        [SerializeField] private Text? legacyLossesSummaryText;
        [SerializeField] private Text? legacyConquestBannerText;
        [SerializeField] private Text[] legacyAttackerDiceTexts = Array.Empty<Text>();
        [SerializeField] private Text[] legacyDefenderDiceTexts = Array.Empty<Text>();

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

            if (presenter == null)
            {
                presenter = FindAnyObjectByType<GamePresenter>();
            }
        }

        private void Start()
        {
            if (presenter != null)
            {
                presenter.OnCombatDetailsResolved += HandleCombatDetailsResolved;
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (presenter != null)
            {
                presenter.OnCombatDetailsResolved -= HandleCombatDetailsResolved;
            }
        }

        public void BindPresenter(GamePresenter newPresenter)
        {
            if (presenter != null)
            {
                presenter.OnCombatDetailsResolved -= HandleCombatDetailsResolved;
            }

            presenter = newPresenter;

            if (presenter != null)
            {
                presenter.OnCombatDetailsResolved += HandleCombatDetailsResolved;
            }
        }

        private void HandleCombatDetailsResolved(CombatResult result, Territory attacker, Territory defender)
        {
            var attackerColor = Color.red;
            var defenderColor = Color.yellow;

            if (presenter?.GameState != null)
            {
                if (presenter.GameState.Players.FirstOrDefault(p => string.Equals(p.Id, attacker.OwnerId, StringComparison.OrdinalIgnoreCase)) is { } pAttacker)
                {
                    attackerColor = GamePresenter.ResolvePlayerColor(pAttacker.Color);
                }

                if (presenter.GameState.Players.FirstOrDefault(p => string.Equals(p.Id, defender.OwnerId, StringComparison.OrdinalIgnoreCase)) is { } pDefender)
                {
                    defenderColor = GamePresenter.ResolvePlayerColor(pDefender.Color);
                }
            }

            ShowCombat(result, attacker, defender, attackerColor, defenderColor);
        }


        /// <summary>
        /// Apresenta o resultado do combate com todos os detalhes visuais.
        /// </summary>
        public void ShowCombat(
            CombatResult result,
            Territory? attackerTerritory = null,
            Territory? defenderTerritory = null,
            Color? attackerColor = null,
            Color? defenderColor = null)
        {
            if (result == null) return;

            // 1. Territórios
            var atkName = attackerTerritory != null ? attackerTerritory.Name : "Atacante";
            var defName = defenderTerritory != null ? defenderTerritory.Name : "Defensor";

            SetLabel(attackerTerritoryText, legacyAttackerTerritoryText, $"⚔ {atkName.ToUpperInvariant()}");
            SetLabel(defenderTerritoryText, legacyDefenderTerritoryText, $"🛡 {defName.ToUpperInvariant()}");

            if (attackerColorBadge != null && attackerColor.HasValue)
            {
                attackerColorBadge.color = attackerColor.Value;
            }

            if (defenderColorBadge != null && defenderColor.HasValue)
            {
                defenderColorBadge.color = defenderColor.Value;
            }

            // 2. Dados do Atacante (Vermelhos, até 3)
            var sortedAtk = result.SortedAttackerDice;
            for (var i = 0; i < attackerDiceBoxes.Length; i++)
            {
                var box = attackerDiceBoxes[i];
                if (box == null) continue;

                if (i < sortedAtk.Count)
                {
                    box.SetActive(true);
                    var valStr = sortedAtk[i].ToString();
                    if (i < attackerDiceTexts.Length && attackerDiceTexts[i] != null)
                    {
                        attackerDiceTexts[i].text = valStr;
                    }
                    if (i < legacyAttackerDiceTexts.Length && legacyAttackerDiceTexts[i] != null)
                    {
                        legacyAttackerDiceTexts[i].text = valStr;
                    }
                }
                else
                {
                    box.SetActive(false);
                }
            }

            // 3. Dados do Defensor (Amarelos, até 3)
            var sortedDef = result.SortedDefenderDice;
            for (var i = 0; i < defenderDiceBoxes.Length; i++)
            {
                var box = defenderDiceBoxes[i];
                if (box == null) continue;

                if (i < sortedDef.Count)
                {
                    box.SetActive(true);
                    var valStr = sortedDef[i].ToString();
                    if (i < defenderDiceTexts.Length && defenderDiceTexts[i] != null)
                    {
                        defenderDiceTexts[i].text = valStr;
                    }
                    if (i < legacyDefenderDiceTexts.Length && legacyDefenderDiceTexts[i] != null)
                    {
                        legacyDefenderDiceTexts[i].text = valStr;
                    }
                }
                else
                {
                    box.SetActive(false);
                }
            }

            // 4. Comparações Par a Par
            var sbComp = new StringBuilder();
            for (var i = 0; i < result.Comparisons.Count; i++)
            {
                var comp = result.Comparisons[i];
                var pairNum = i + 1;
                if (comp.AttackerWon)
                {
                    sbComp.AppendLine($"Dado {pairNum}: [ {comp.AttackerDie} ] vs [ {comp.DefenderDie} ] ➜ Vitória do Atacante (-1 tropa defensora)");
                }
                else
                {
                    var isTie = comp.AttackerDie == comp.DefenderDie;
                    var tieNote = isTie ? " (Empate favorece a defesa)" : "";
                    sbComp.AppendLine($"Dado {pairNum}: [ {comp.AttackerDie} ] vs [ {comp.DefenderDie} ] ➜ Vitória da Defesa{tieNote} (-1 tropa atacante)");
                }
            }
            SetLabel(comparisonsText, legacyComparisonsText, sbComp.ToString().TrimEnd());

            // 5. Resumo de Baixas
            var lossesStr = $"Baixas Militares: Atacante perdeu {result.AttackerArmiesLost} tropa(s) | Defensor perdeu {result.DefenderArmiesLost} tropa(s)";
            SetLabel(lossesSummaryText, legacyLossesSummaryText, lossesStr);

            // 6. Conquista de Território
            if (conquestBanner != null)
            {
                conquestBanner.SetActive(result.TerritoryConquered);
            }

            if (result.TerritoryConquered)
            {
                SetLabel(conquestBannerText, legacyConquestBannerText, $"★ TERRITÓRIO {defName.ToUpperInvariant()} CONQUISTADO! ★");
            }

            // Abre o modal
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
        }

        public void Close()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private static void SetLabel(TMP_Text? tmp, Text? legacy, string value)
        {
            if (tmp != null) tmp.text = value;
            if (legacy != null) legacy.text = value;
        }
    }
}
