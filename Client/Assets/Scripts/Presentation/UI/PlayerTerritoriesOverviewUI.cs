#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using War.Core.Data;
using War.Core.Game;
using War.Core.Models;
using War.Presentation.Game;

namespace War.Presentation.UI
{
    /// <summary>
    /// Modal HUD que exibe a relação completa de países e territórios por jogador.
    /// Exibe os jogadores ordenados por quantidade de territórios dominados (do maior para o menor).
    /// Permite consulta tática a qualquer momento da partida com o clique do botão na barra superior.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerTerritoriesOverviewUI : MonoBehaviour
    {
        [Header("Contêiner Principal")]
        [Tooltip("Objeto raiz do modal ativado/desativado.")]
        [SerializeField] private GameObject? modalRoot;

        [Header("Botão Fechar")]
        [SerializeField] private Button? closeButton;

        [Header("Elementos de Texto")]
        [SerializeField] private TMP_Text? titleText;
        [SerializeField] private TMP_Text? subtitleText;
        [SerializeField] private TMP_Text? contentText;
        [SerializeField] private ScrollRect? scrollRect;

        [Header("Fallback Text (UnityEngine.UI)")]
        [SerializeField] private Text? legacyContentText;

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
        }

        private void Start()
        {
            // Oculto por padrão no início da partida conforme especificado
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        /// <summary>
        /// Fecha e oculta o painel.
        /// </summary>
        public void Close()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Alterna a visibilidade do painel (Abre se fechado, Fecha se aberto).
        /// </summary>
        public void Toggle(GameState state, Func<PlayerColor, Color> colorResolver, Func<Player, int>? reinforcementsResolver = null)
        {
            if (modalRoot != null && modalRoot.activeSelf)
            {
                Close();
            }
            else
            {
                Show(state, colorResolver, reinforcementsResolver);
            }
        }

        /// <summary>
        /// Exibe o painel populado com os dados mais recentes do jogo.
        /// </summary>
        public void Show(GameState state, Func<PlayerColor, Color> colorResolver, Func<Player, int>? reinforcementsResolver = null)
        {
            if (state == null) return;

            PopulateOverview(state, colorResolver, reinforcementsResolver);

            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }

            if (scrollRect != null)
            {
                scrollRect.normalizedPosition = new Vector2(0f, 1f); // Retorna ao topo
            }
        }

        /// <summary>
        /// Atualiza o conteúdo se o painel estiver aberto na tela.
        /// </summary>
        public void Refresh(GameState state, Func<PlayerColor, Color> colorResolver, Func<Player, int>? reinforcementsResolver = null)
        {
            if (modalRoot != null && modalRoot.activeSelf)
            {
                PopulateOverview(state, colorResolver, reinforcementsResolver);
            }
        }

        private void PopulateOverview(GameState state, Func<PlayerColor, Color> colorResolver, Func<Player, int>? reinforcementsResolver)
        {
            var sb = new StringBuilder();
            var totalTerritories = state.Territories.Count;

            // Ordena os jogadores por quantidade de territórios dominados (ordem decrescente)
            var sortedPlayers = state.Players
                .OrderByDescending(p => state.Territories.Values.Count(t => string.Equals(t.OwnerId, p.Id, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(p => p.Name)
                .ToList();

            var rank = 1;
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                var playerColor = colorResolver(player.Color);
                var hexColor = ColorUtility.ToHtmlStringRGB(playerColor);

                var owned = state.Territories.Values
                    .Where(t => string.Equals(t.OwnerId, player.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.Continent)
                    .ThenBy(t => t.Name)
                    .ToList();

                var count = owned.Count;
                var totalArmies = owned.Sum(t => t.Armies);
                var pct = totalTerritories > 0 ? (count * 100f / totalTerritories) : 0f;
                var isCurrentTurn = string.Equals(state.CurrentPlayer?.Id, player.Id, StringComparison.OrdinalIgnoreCase);

                var medal = rank switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"#{rank}"
                };

                var turnBadge = isCurrentTurn ? " <color=#ECC94B><b>[TURNO ATIVO]</b></color>" : "";
                var eliminatedBadge = player.IsEliminated || count == 0 ? " <color=#E53E3E><b>[ELIMINADO]</b></color>" : "";

                // Cabeçalho do Jogador
                sb.AppendLine($"<size=18>{medal} <color=#{hexColor}>■ <b>{player.Name}</b></color>{turnBadge}{eliminatedBadge}</size>");

                if (count == 0)
                {
                    sb.AppendLine("   <color=#718096><i>Sem territórios dominados no momento.</i></color>");
                }
                else
                {
                    var troopsBonus = reinforcementsResolver != null 
                        ? reinforcementsResolver(player) 
                        : Math.Max(3, count / 2);

                    sb.AppendLine($"   <color=#A0AEC0><b>{count} Territórios</b> ({pct:F1}%)  •  Exército Total: <b>{totalArmies} tropas</b>  •  Reforço: <b>+{troopsBonus}/rodada</b></color>");

                    // Territórios agrupados por continente
                    var byContinent = owned.GroupBy(t => t.Continent).OrderBy(g => g.Key);
                    foreach (var group in byContinent)
                    {
                        var continent = Continent.Get(group.Key);
                        var totalInContinent = ClassicMapData.GetTerritoriesByContinent(group.Key).Count();
                        var dominatesAll = group.Count() >= totalInContinent && totalInContinent > 0;
                        var bonusTag = dominatesAll ? $" <color=#48BB78><b>(+{continent.TroopBonus} Bônus Ativo!)</b></color>" : "";

                        var terrNames = string.Join(", ", group.OrderBy(t => t.Name).Select(t => $"<b>{t.Name}</b> <color=#718096>({t.Armies})</color>"));
                        sb.AppendLine($"   <color=#CBD5E0>• <b>{continent.Name}</b> ({group.Count()}/{totalInContinent}){bonusTag}:</color> {terrNames}");
                    }
                }

                sb.AppendLine("<color=#2D3748>──────────────────────────────────────────────────────────</color>");
                rank++;
            }

            var textResult = sb.ToString();
            if (contentText != null)
            {
                contentText.text = textResult;
            }
            if (legacyContentText != null)
            {
                legacyContentText.text = textResult;
            }
        }
    }
}

