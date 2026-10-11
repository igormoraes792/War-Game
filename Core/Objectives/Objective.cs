#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Game;
using War.Core.Models;

namespace War.Core.Objectives
{
    /// <summary>
    /// Contexto de leitura do estado do jogo fornecido aos objetivos para avaliação de vitória.
    /// </summary>
    public interface IGameContext
    {
        /// <summary>
        /// Todos os territórios do mapa indexados por ID.
        /// </summary>
        IReadOnlyDictionary<string, Territory> Territories { get; }

        /// <summary>
        /// Lista de todos os jogadores na partida.
        /// </summary>
        IReadOnlyList<Player> Players { get; }

        /// <summary>
        /// Verifica se um continente inteiro é dominado por um determinado jogador.
        /// </summary>
        bool IsContinentControlledBy(ContinentType continent, string playerId);

        /// <summary>
        /// Quantidade de territórios dominados por um determinado jogador.
        /// </summary>
        int GetTerritoryCount(string playerId);

        /// <summary>
        /// Busca jogador por cor.
        /// </summary>
        Player? GetPlayerByColor(PlayerColor color);

        /// <summary>
        /// Busca jogador por ID.
        /// </summary>
        Player? GetPlayerById(string playerId);
    }

    /// <summary>
    /// Classe base para objetivos secretos do WAR.
    /// </summary>
    public abstract class Objective
    {
        public int Id { get; }
        public string Title { get; }
        public string Description { get; }

        protected Objective(int id, string title, string description)
        {
            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// Avalia se a condição de vitória foi atingida para o jogador especificado no estado atual.
        /// </summary>
        public abstract bool CheckVictoryCondition(IGameContext context, Player player);

        public override string ToString() => $"[Objetivo #{Id}] {Title}: {Description}";
    }

    /// <summary>
    /// Objetivo clássico: Conquistar X territórios à sua escolha (padrão 24).
    /// </summary>
    public sealed class TerritoryCountObjective : Objective
    {
        public int RequiredTerritories { get; }

        public TerritoryCountObjective(int id, string title, string description, int requiredTerritories = 24)
            : base(id, title, description)
        {
            RequiredTerritories = requiredTerritories;
        }

        public override bool CheckVictoryCondition(IGameContext context, Player player)
        {
            if (context == null || player == null || player.IsEliminated) return false;
            return context.GetTerritoryCount(player.Id) >= RequiredTerritories;
        }
    }

    /// <summary>
    /// Objetivo clássico: Conquistar X territórios com pelo menos Y tropas em cada um (padrão 18 com 2 tropas).
    /// </summary>
    public sealed class TerritoriesWithArmiesObjective : Objective
    {
        public int RequiredTerritories { get; }
        public int MinimumArmiesPerTerritory { get; }

        public TerritoriesWithArmiesObjective(
            int id,
            string title,
            string description,
            int requiredTerritories = 18,
            int minimumArmiesPerTerritory = 2)
            : base(id, title, description)
        {
            RequiredTerritories = requiredTerritories;
            MinimumArmiesPerTerritory = minimumArmiesPerTerritory;
        }

        public override bool CheckVictoryCondition(IGameContext context, Player player)
        {
            if (context == null || player == null || player.IsEliminated) return false;

            var matchingCount = context.Territories.Values.Count(t =>
                string.Equals(t.OwnerId, player.Id, StringComparison.OrdinalIgnoreCase) &&
                t.Armies >= MinimumArmiesPerTerritory);

            return matchingCount >= RequiredTerritories;
        }
    }

    /// <summary>
    /// Objetivo clássico: Conquistar continentes específicos (ex: América do Norte e África)
    /// ou continentes específicos + qualquer outro continente adicional à escolha.
    /// </summary>
    public sealed class ContinentConquestObjective : Objective
    {
        public IReadOnlyList<ContinentType> RequiredContinents { get; }
        public int AdditionalAnyContinentCount { get; }

        public ContinentConquestObjective(
            int id,
            string title,
            string description,
            IEnumerable<ContinentType> requiredContinents,
            int additionalAnyContinentCount = 0)
            : base(id, title, description)
        {
            RequiredContinents = new ReadOnlyCollection<ContinentType>(requiredContinents.ToList());
            AdditionalAnyContinentCount = additionalAnyContinentCount;
        }

        public override bool CheckVictoryCondition(IGameContext context, Player player)
        {
            if (context == null || player == null || player.IsEliminated) return false;

            // Verifica os continentes obrigatórios
            foreach (var continent in RequiredContinents)
            {
                if (!context.IsContinentControlledBy(continent, player.Id))
                {
                    return false;
                }
            }

            if (AdditionalAnyContinentCount <= 0)
            {
                return true;
            }

            // Conta continentes extras dominados além dos obrigatórios
            var allContinents = Continent.GetAll().Select(c => c.Type);
            var otherContinentsControlled = allContinents
                .Where(c => !RequiredContinents.Contains(c))
                .Count(c => context.IsContinentControlledBy(c, player.Id));

            return otherContinentsControlled >= AdditionalAnyContinentCount;
        }
    }

    /// <summary>
    /// Objetivo clássico: Destruir totalmente o exército de uma cor específica.
    /// Se a cor sorteada for a do próprio jogador, se a cor não estiver na partida,
    /// ou se outro jogador eliminar o alvo antes, o objetivo converte automaticamente para 24 territórios.
    /// </summary>
    public sealed class EliminateColorObjective : Objective
    {
        public const int FallbackRequiredTerritories = 24;

        public PlayerColor TargetColor { get; }

        public EliminateColorObjective(int id, string title, string description, PlayerColor targetColor)
            : base(id, title, description)
        {
            TargetColor = targetColor;
        }

        public override bool CheckVictoryCondition(IGameContext context, Player player)
        {
            if (context == null || player == null || player.IsEliminated) return false;

            var targetPlayer = context.GetPlayerByColor(TargetColor);

            // Regra oficial do WAR:
            // Se o alvo não existe na partida, ou é o próprio jogador: conquista 24 territórios
            if (targetPlayer == null || targetPlayer.Id == player.Id)
            {
                return context.GetTerritoryCount(player.Id) >= FallbackRequiredTerritories;
            }

            // Se o alvo foi eliminado:
            if (targetPlayer.IsEliminated)
            {
                // Se foi este jogador que eliminou o alvo, ele vence!
                if (string.Equals(targetPlayer.EliminatedByPlayerId, player.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Se outro jogador eliminou o alvo, cai no fallback clássico dos 24 territórios
                return context.GetTerritoryCount(player.Id) >= FallbackRequiredTerritories;
            }

            // Alvo ainda vivo: ainda não completou
            return false;
        }
    }
}
