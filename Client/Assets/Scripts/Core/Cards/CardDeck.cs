#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Data;
using War.Core.Models;

namespace War.Core.Cards
{
    /// <summary>
    /// Resultado consolidado de uma troca de cartas de território por exércitos.
    /// </summary>
    public sealed class CardTradeResult
    {
        /// <summary>
        /// Quantidade de tropas ganhas pela tabela de trocas da partida.
        /// </summary>
        public int BaseArmies { get; }

        /// <summary>
        /// Territórios do jogador presentes nas cartas trocadas que concedem +2 tropas extras cada.
        /// </summary>
        public IReadOnlyList<string> OwnedTerritoriesAwardedBonus { get; }

        /// <summary>
        /// Quantidade total de tropas adicionais oriundas do bônus de território (+2 por território possuído).
        /// </summary>
        public int TerritoryBonusArmies => OwnedTerritoriesAwardedBonus.Count * 2;

        /// <summary>
        /// Total geral de exércitos recebidos nesta troca.
        /// </summary>
        public int TotalArmies => BaseArmies + TerritoryBonusArmies;

        public CardTradeResult(int baseArmies, IEnumerable<string> ownedTerritoriesAwardedBonus)
        {
            BaseArmies = baseArmies;
            OwnedTerritoriesAwardedBonus = new ReadOnlyCollection<string>(ownedTerritoriesAwardedBonus.ToList());
        }

        public override string ToString() =>
            $"Troca de cartas: Base={BaseArmies}, Bônus de Território={TerritoryBonusArmies} (Territórios: {string.Join(", ", OwnedTerritoriesAwardedBonus)}), Total={TotalArmies}";
    }

    /// <summary>
    /// Baralho de cartas do WAR clássico, contendo 42 cartas de território e 2 coringas (44 cartas no total).
    /// Gerencia compras, descartes, validação e resolução de trocas de cartas por tropas.
    /// </summary>
    public sealed class CardDeck
    {
        public const int WildcardsCount = 2;
        public const int RequiredCardsForTrade = 3;

        private readonly List<Card> _drawPile = new();
        private readonly List<Card> _discardPile = new();
        private int _tradeCount;

        /// <summary>
        /// Número de trocas de cartas já realizadas até o momento.
        /// </summary>
        public int TradeCount => _tradeCount;

        /// <summary>
        /// Quantidade de cartas disponíveis para compra no monte principal.
        /// </summary>
        public int RemainingCardsCount => _drawPile.Count;

        /// <summary>
        /// Quantidade de cartas na pilha de descarte.
        /// </summary>
        public int DiscardPileCount => _discardPile.Count;

        /// <summary>
        /// Cria e inicializa um novo baralho com as 44 cartas clássicas.
        /// </summary>
        public CardDeck(bool shuffle = true, Random? random = null)
        {
            InitializeDeck();
            if (shuffle)
            {
                Shuffle(random);
            }
        }

        /// <summary>
        /// Inicializa o baralho padrão de 44 cartas (14 Círculo, 14 Quadrado, 14 Triângulo, 2 Coringas).
        /// </summary>
        private void InitializeDeck()
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _tradeCount = 0;

            // Ordenamos as definições para garantir distribuição determinística consistente
            var definitions = ClassicMapData.GetAllDefinitions()
                .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var shapes = new[] { CardShape.Circle, CardShape.Square, CardShape.Triangle };
            for (var i = 0; i < definitions.Count; i++)
            {
                var shape = shapes[i % shapes.Length];
                _drawPile.Add(new Card(definitions[i].Id, shape));
            }

            // Adiciona os 2 coringas
            for (var i = 0; i < WildcardsCount; i++)
            {
                _drawPile.Add(Card.CreateWildcard());
            }
        }

        /// <summary>
        /// Embaralha as cartas do monte de compra usando o algoritmo Fisher-Yates.
        /// </summary>
        public void Shuffle(Random? random = null)
        {
            var rng = random ?? new Random();
            var n = _drawPile.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                var value = _drawPile[k];
                _drawPile[k] = _drawPile[n];
                _drawPile[n] = value;
            }
        }

        /// <summary>
        /// Retira uma carta do topo do baralho. Se o monte de compra estiver vazio,
        /// reembaralha automaticamente a pilha de descarte.
        /// </summary>
        public Card? DrawCard(Random? random = null)
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0)
                {
                    return null; // Não há mais cartas no jogo
                }

                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(random);
            }

            var card = _drawPile[0];
            _drawPile.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Devolve uma carta para a pilha de descarte.
        /// </summary>
        public void Discard(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            _discardPile.Add(card);
        }

        /// <summary>
        /// Devolve uma lista de cartas para a pilha de descarte.
        /// </summary>
        public void DiscardRange(IEnumerable<Card> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            _discardPile.AddRange(cards);
        }

        /// <summary>
        /// Calcula a quantidade de exércitos fornecidos com base na escala clássica progressiva de trocas.
        /// Escala clássica do WAR brasileiro:
        /// 1ª troca: 4
        /// 2ª troca: 6
        /// 3ª troca: 8
        /// 4ª troca: 10
        /// 5ª troca: 12
        /// 6ª troca: 15
        /// 7ª em diante: +5 tropas por troca (20, 25, 30, 35...).
        /// </summary>
        /// <param name="tradeNumber">Número ordinal da troca (1-indexado).</param>
        public static int GetArmiesForTrade(int tradeNumber)
        {
            if (tradeNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(tradeNumber), "O número da troca deve ser >= 1.");

            return tradeNumber switch
            {
                1 => 4,
                2 => 6,
                3 => 8,
                4 => 10,
                5 => 12,
                6 => 15,
                _ => 15 + ((tradeNumber - 6) * 5)
            };
        }

        /// <summary>
        /// Retorna a quantidade de tropas que a PRÓXIMA troca deste baralho concederá.
        /// </summary>
        public int GetNextTradeArmies() => GetArmiesForTrade(_tradeCount + 1);

        /// <summary>
        /// Valida se um conjunto de cartas é válido para troca segundo as regras clássicas:
        /// - Deve ter exatamente 3 cartas;
        /// - 3 formatos iguais; OU
        /// - 3 formatos diferentes (Círculo, Quadrado, Triângulo); OU
        /// - Qualquer combinação usando 1 ou mais Coringas.
        /// </summary>
        public static bool IsValidTradeSet(IReadOnlyList<Card> cards, out string? failureReason)
        {
            failureReason = null;
            if (cards == null || cards.Count != RequiredCardsForTrade)
            {
                failureReason = $"Uma troca de cartas requer exatamente {RequiredCardsForTrade} cartas.";
                return false;
            }

            var wildcardsCount = cards.Count(c => c.IsWildcard);
            if (wildcardsCount >= 1)
            {
                // No WAR, um ou mais coringas em uma trinca de 3 cartas sempre formam uma combinação válida.
                return true;
            }

            var shapes = cards.Select(c => c.Shape).ToList();
            var distinctShapesCount = shapes.Distinct().Count();

            // 3 iguais (distinct = 1) ou 3 todos diferentes (distinct = 3)
            if (distinctShapesCount == 1 || distinctShapesCount == 3)
            {
                return true;
            }

            failureReason = "As 3 cartas devem ter todas a mesma figura, ou uma de cada figura, ou conter ao menos um coringa.";
            return false;
        }

        /// <summary>
        /// Executa a troca de 3 cartas, incrementando o contador global de trocas e descartando as cartas.
        /// Verifica quais territórios das cartas pertencem ao jogador para conceder o bônus de +2 tropas por território.
        /// </summary>
        public CardTradeResult TradeCards(
            IReadOnlyList<Card> cardsToTrade,
            IReadOnlyCollection<string> playerOwnedTerritoryIds)
        {
            if (!IsValidTradeSet(cardsToTrade, out var failureReason))
            {
                throw new InvalidOperationException($"Troca de cartas inválida: {failureReason}");
            }

            _tradeCount++;
            var baseArmies = GetArmiesForTrade(_tradeCount);

            var ownedBonusTerritories = new List<string>();
            var ownedSet = new HashSet<string>(playerOwnedTerritoryIds, StringComparer.OrdinalIgnoreCase);

            foreach (var card in cardsToTrade)
            {
                if (!card.IsWildcard && card.TerritoryId != null && ownedSet.Contains(card.TerritoryId))
                {
                    ownedBonusTerritories.Add(card.TerritoryId);
                }
            }

            // Descarta as cartas trocadas
            DiscardRange(cardsToTrade);

            return new CardTradeResult(baseArmies, ownedBonusTerritories);
        }
    }
}
