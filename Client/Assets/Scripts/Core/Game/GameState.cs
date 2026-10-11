#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Cards;
using War.Core.Data;
using War.Core.Models;
using War.Core.Objectives;

namespace War.Core.Game
{
    /// <summary>
    /// Estado global imutável externamente de uma partida em andamento de WAR.
    /// Implementa <see cref="IGameContext"/> para verificação de condições de vitória.
    /// </summary>
    public sealed class GameState : IGameContext
    {
        private readonly Dictionary<string, Territory> _territories;
        private readonly List<Player> _players;

        public IReadOnlyDictionary<string, Territory> Territories => _territories;
        public IReadOnlyList<Player> Players => _players;
        public CardDeck Deck { get; }

        public int CurrentPlayerIndex { get; internal set; }
        public int TurnNumber { get; internal set; }
        public TurnPhase CurrentPhase { get; internal set; }
        public Player? Winner { get; internal set; }

        public Player CurrentPlayer => _players[CurrentPlayerIndex];
        public bool IsGameOver => CurrentPhase == TurnPhase.GameOver && Winner != null;

        public GameState(
            IEnumerable<Player> players,
            IDictionary<string, Territory>? territories = null,
            CardDeck? deck = null)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            _players = players.ToList();
            if (_players.Count < 2)
            {
                throw new ArgumentException("Uma partida de WAR necessita de pelo menos 2 jogadores.", nameof(players));
            }

            _territories = territories != null
                ? new Dictionary<string, Territory>(territories, StringComparer.OrdinalIgnoreCase)
                : ClassicMapData.CreateClassicMap();

            Deck = deck ?? new CardDeck();
            CurrentPlayerIndex = 0;
            TurnNumber = 1;
            CurrentPhase = TurnPhase.NotStarted;
            Winner = null;
        }

        /// <summary>
        /// Retorna a quantidade de territórios sob o controle do jogador.
        /// </summary>
        public int GetTerritoryCount(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return 0;

            return _territories.Values.Count(t =>
                string.Equals(t.OwnerId, playerId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifica se um determinado jogador domina a totalidade dos territórios de um continente.
        /// </summary>
        public bool IsContinentControlledBy(ContinentType continent, string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return false;

            var continentTerritories = _territories.Values
                .Where(t => t.Continent == continent)
                .ToList();

            if (continentTerritories.Count == 0) return false;

            return continentTerritories.All(t =>
                string.Equals(t.OwnerId, playerId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Busca jogador participante por sua cor.
        /// </summary>
        public Player? GetPlayerByColor(PlayerColor color) =>
            _players.FirstOrDefault(p => p.Color == color);

        /// <summary>
        /// Busca jogador participante por seu ID único.
        /// </summary>
        public Player? GetPlayerById(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return null;
            return _players.FirstOrDefault(p => string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retorna todos os territórios pertencentes a um determinado jogador.
        /// </summary>
        public IEnumerable<Territory> GetTerritoriesByPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return Enumerable.Empty<Territory>();

            return _territories.Values.Where(t =>
                string.Equals(t.OwnerId, playerId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifica através de busca em largura (BFS) se dois territórios estão conectados por um caminho
        /// contínuo de territórios dominados pelo mesmo jogador.
        /// </summary>
        public bool AreTerritoriesConnected(string fromTerritoryId, string toTerritoryId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(fromTerritoryId) ||
                string.IsNullOrWhiteSpace(toTerritoryId) ||
                string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            if (!_territories.TryGetValue(fromTerritoryId, out var start) ||
                !_territories.TryGetValue(toTerritoryId, out var target))
            {
                return false;
            }

            if (!string.Equals(start.OwnerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(target.OwnerId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(start.Id, target.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start.Id };
            var queue = new Queue<Territory>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighborId in current.AdjacentTerritoryIds)
                {
                    if (string.Equals(neighborId, target.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (visited.Add(neighborId) && _territories.TryGetValue(neighborId, out var neighbor))
                    {
                        if (string.Equals(neighbor.OwnerId, playerId, StringComparison.OrdinalIgnoreCase))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Avalia se o jogador indicado cumpriu seu objetivo secreto.
        /// </summary>
        public bool CheckPlayerVictory(Player player)
        {
            if (player == null || player.IsEliminated || player.SecretObjective == null)
            {
                return false;
            }

            return player.SecretObjective.CheckVictoryCondition(this, player);
        }
    }
}
