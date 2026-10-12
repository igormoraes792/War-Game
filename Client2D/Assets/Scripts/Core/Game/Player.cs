#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using War.Core.Cards;
using War.Core.Objectives;

namespace War.Core.Game
{
    /// <summary>
    /// Representa um jogador na partida de WAR (POCO puro sem dependência de engine).
    /// </summary>
    public sealed class Player
    {
        private readonly List<Card> _cards = new();

        /// <summary>
        /// Identificador único do jogador (GUID ou string arbitrária).
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Nome de exibição do jogador.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Cor dos exércitos do jogador na partida.
        /// </summary>
        public PlayerColor Color { get; }

        /// <summary>
        /// Quantidade de tropas/reforços acumuladas a serem distribuídas na fase de reforço.
        /// </summary>
        public int ArmiesToDeploy { get; private set; }

        /// <summary>
        /// Objetivo secreto sorteado para o jogador.
        /// </summary>
        public Objective? SecretObjective { get; set; }

        /// <summary>
        /// Indica se o jogador foi completamente eliminado da partida.
        /// </summary>
        public bool IsEliminated { get; private set; }

        /// <summary>
        /// Identificador do jogador que eliminou este jogador, se aplicável.
        /// </summary>
        public string? EliminatedByPlayerId { get; private set; }

        /// <summary>
        /// Flag que indica se o jogador conquistou pelo menos 1 território durante o turno atual
        /// (condição necessária para ter direito a comprar uma carta ao fim do turno).
        /// </summary>
        public bool ConqueredTerritoryThisTurn { get; set; }

        /// <summary>
        /// Mão de cartas de território atualmente na posse do jogador.
        /// </summary>
        public IReadOnlyList<Card> Cards => _cards;

        public Player(string id, string name, PlayerColor color)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id do jogador não pode ser vazio.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nome do jogador não pode ser vazio.", nameof(name));

            Id = id.Trim();
            Name = name.Trim();
            Color = color;
            ArmiesToDeploy = 0;
            IsEliminated = false;
        }

        /// <summary>
        /// Adiciona tropas ao saldo a receber/alocar do jogador.
        /// </summary>
        public void AddArmiesToDeploy(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Quantidade de tropas a adicionar deve ser >= 0.");

            ArmiesToDeploy += count;
        }

        /// <summary>
        /// Consome tropas do saldo de reforço ao alocar em um território.
        /// </summary>
        public void ConsumeArmiesToDeploy(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Quantidade de tropas a consumir deve ser >= 0.");
            if (count > ArmiesToDeploy)
                throw new InvalidOperationException($"Não é possível alocar {count} tropas. Saldo disponível: {ArmiesToDeploy}.");

            ArmiesToDeploy -= count;
        }

        /// <summary>
        /// Adiciona uma carta comprada à mão do jogador.
        /// </summary>
        public void AddCard(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            _cards.Add(card);
        }

        /// <summary>
        /// Remove cartas trocadas ou transferidas da mão do jogador.
        /// </summary>
        public void RemoveCards(IEnumerable<Card> cardsToRemove)
        {
            if (cardsToRemove == null) throw new ArgumentNullException(nameof(cardsToRemove));

            foreach (var card in cardsToRemove)
            {
                if (!_cards.Remove(card))
                {
                    throw new InvalidOperationException($"O jogador não possui a carta {card} para remover.");
                }
            }
        }

        /// <summary>
        /// Transfere todas as cartas restantes deste jogador para outro jogador (ao ser eliminado).
        /// </summary>
        public List<Card> SurrenderAllCards()
        {
            var cardsCopy = new List<Card>(_cards);
            _cards.Clear();
            return cardsCopy;
        }

        /// <summary>
        /// Marca o jogador como eliminado da partida.
        /// </summary>
        public void MarkEliminated(string? eliminatedByPlayerId)
        {
            IsEliminated = true;
            EliminatedByPlayerId = eliminatedByPlayerId;
            ArmiesToDeploy = 0;
        }

        /// <summary>
        /// Reinicia as flags de estado transitórias no início do turno do jogador.
        /// </summary>
        public void ResetTurnFlags()
        {
            ConqueredTerritoryThisTurn = false;
        }

        public override string ToString() =>
            $"Jogador {Name} ({Color}) [Id: {Id}] - Tropas a alocar: {ArmiesToDeploy}, Cartas: {_cards.Count}, Eliminado: {IsEliminated}";
    }
}
