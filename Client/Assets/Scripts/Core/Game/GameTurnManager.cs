#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using War.Core.Cards;
using War.Core.Combat;
using War.Core.Models;
using War.Core.Objectives;

namespace War.Core.Game
{
    /// <summary>
    /// Gerenciador de Máquina de Estados Finitos (FSM) do turno e árbitro das regras de negócio do WAR.
    /// Funciona de forma 100% determinística, desacoplada de engines gráficas, pronta para Unity ou backend de servidor.
    /// </summary>
    public sealed class GameTurnManager
    {
        public GameState State { get; }

        public GameTurnManager(GameState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public GameTurnManager(IEnumerable<Player> players, IDictionary<string, Territory>? territories = null, CardDeck? deck = null)
            : this(new GameState(players, territories, deck))
        {
        }

        /// <summary>
        /// Inicializa a partida: distribui objetivos secretos caso ainda não tenham sido atribuídos,
        /// calcula os reforços iniciais do primeiro jogador e coloca a FSM na fase de Reforço.
        /// </summary>
        public void StartGame(Random? rng = null)
        {
            if (State.CurrentPhase != TurnPhase.NotStarted)
            {
                throw new InvalidOperationException($"Partida já foi iniciada. Fase atual: {State.CurrentPhase}");
            }

            // Garante que todos os jogadores tenham objetivos secretos
            var needObjectives = State.Players.Any(p => p.SecretObjective == null);
            if (needObjectives)
            {
                ClassicObjectives.DistributeObjectives(State.Players, rng);
            }

            State.CurrentPlayerIndex = 0;
            State.TurnNumber = 1;
            State.Winner = null;

            BeginPlayerTurn(State.CurrentPlayer);
        }

        /// <summary>
        /// Inicia o turno de um jogador, calculando seus reforços e mudando a fase para Reinforcement.
        /// </summary>
        private void BeginPlayerTurn(Player player)
        {
            player.ResetTurnFlags();
            State.CurrentPhase = TurnPhase.Reinforcement;

            var reinforcements = CalculateTurnReinforcements(player);
            player.AddArmiesToDeploy(reinforcements);
        }

        /// <summary>
        /// Calcula a quantidade de tropas de reforço devidas ao jogador no início do seu turno:
        /// 1) Tropas territoriais: Total de territórios / 2 (mínimo clássico de 3 tropas).
        /// 2) Bônus por continente: Tropas extras para cada continente totalmente dominado.
        /// </summary>
        public int CalculateTurnReinforcements(Player player)
        {
            if (player == null || player.IsEliminated) return 0;

            var territoryCount = State.GetTerritoryCount(player.Id);
            var baseFromTerritories = Math.Max(3, territoryCount / 2);

            var continentBonus = 0;
            foreach (var continent in Continent.GetAll())
            {
                if (State.IsContinentControlledBy(continent.Type, player.Id))
                {
                    continentBonus += continent.TroopBonus;
                }
            }

            return baseFromTerritories + continentBonus;
        }

        /// <summary>
        /// Executa a troca de 3 cartas da mão do jogador ativo por exércitos de reforço.
        /// Permite bônus de posse de território (+2 tropas por território possuído correspondente à carta).
        /// </summary>
        public CardTradeResult TradeCards(IReadOnlyList<Card> cardsToTrade)
        {
            if (State.CurrentPhase != TurnPhase.Reinforcement)
            {
                throw new InvalidOperationException("Trocas de cartas só podem ser realizadas durante a fase de Reforço.");
            }

            if (cardsToTrade == null || cardsToTrade.Count != CardDeck.RequiredCardsForTrade)
            {
                throw new ArgumentException($"A troca exige exatamente {CardDeck.RequiredCardsForTrade} cartas.", nameof(cardsToTrade));
            }

            var activePlayer = State.CurrentPlayer;

            // Valida se todas as cartas estão na mão do jogador
            foreach (var card in cardsToTrade)
            {
                if (!activePlayer.Cards.Contains(card))
                {
                    throw new InvalidOperationException($"O jogador {activePlayer.Name} não possui a carta {card} em sua mão.");
                }
            }

            var ownedTerritoryIds = State.GetTerritoriesByPlayer(activePlayer.Id)
                .Select(t => t.Id)
                .ToList();

            var tradeResult = State.Deck.TradeCards(cardsToTrade, ownedTerritoryIds);

            // Remove cartas da mão do jogador
            activePlayer.RemoveCards(cardsToTrade);

            // Adiciona as tropas base ao saldo a alocar
            activePlayer.AddArmiesToDeploy(tradeResult.BaseArmies);

            // Aplica os bônus de +2 tropas diretamente nos territórios dominados pelo jogador
            foreach (var bonusTerritoryId in tradeResult.OwnedTerritoriesAwardedBonus)
            {
                if (State.Territories.TryGetValue(bonusTerritoryId, out var territory) &&
                    string.Equals(territory.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase))
                {
                    territory.AddArmies(2);
                }
            }

            return tradeResult;
        }

        /// <summary>
        /// Aloca tropas do saldo de reforço do jogador ativo em um de seus territórios.
        /// </summary>
        public void DeployArmies(string territoryId, int armiesCount)
        {
            if (State.CurrentPhase != TurnPhase.Reinforcement)
            {
                throw new InvalidOperationException("Tropas só podem ser alocadas durante a fase de Reforço.");
            }

            if (armiesCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(armiesCount), "A quantidade de tropas a alocar deve ser > 0.");
            }

            var activePlayer = State.CurrentPlayer;
            if (!State.Territories.TryGetValue(territoryId, out var territory))
            {
                throw new KeyNotFoundException($"Território '{territoryId}' não encontrado no mapa.");
            }

            if (!string.Equals(territory.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"O território '{territory.Name}' não pertence ao jogador {activePlayer.Name}.");
            }

            activePlayer.ConsumeArmiesToDeploy(armiesCount);
            territory.AddArmies(armiesCount);
        }

        /// <summary>
        /// Avança da fase de Reforço para a fase de Ataque.
        /// Valida se todas as tropas foram alocadas e se o jogador não viola o limite de 5 cartas sem trocar.
        /// </summary>
        public void AdvanceToAttackPhase()
        {
            if (State.CurrentPhase != TurnPhase.Reinforcement)
            {
                throw new InvalidOperationException($"Transição inválida para Ataque a partir da fase {State.CurrentPhase}.");
            }

            var activePlayer = State.CurrentPlayer;
            if (activePlayer.ArmiesToDeploy > 0)
            {
                throw new InvalidOperationException($"O jogador ainda possui {activePlayer.ArmiesToDeploy} tropas a serem alocadas antes de atacar.");
            }

            // Regra clássica: com 5 cartas na mão, a troca de cartas na fase de reforço é obrigatória
            if (activePlayer.Cards.Count >= 5)
            {
                throw new InvalidOperationException($"O jogador possui {activePlayer.Cards.Count} cartas na mão. A troca é obrigatória antes de passar para a fase de ataque.");
            }

            State.CurrentPhase = TurnPhase.Attack;
        }

        /// <summary>
        /// Realiza um combate ofensivo do território atacante contra o defensor.
        /// Gerencia baixas, conquista de territórios, eliminação de adversários, espólio de cartas e vitória.
        /// </summary>
        public CombatResult Attack(
            string attackerTerritoryId,
            string defenderTerritoryId,
            int? attackerDiceCount = null,
            int? defenderDiceCount = null,
            IDiceRoller? diceRoller = null)
        {
            if (State.CurrentPhase != TurnPhase.Attack)
            {
                throw new InvalidOperationException("Ataques só podem ser realizados durante a fase de Ataque.");
            }

            if (State.IsGameOver)
            {
                throw new InvalidOperationException("A partida já está encerrada.");
            }

            var activePlayer = State.CurrentPlayer;

            if (!State.Territories.TryGetValue(attackerTerritoryId, out var attacker))
            {
                throw new KeyNotFoundException($"Território atacante '{attackerTerritoryId}' não encontrado.");
            }

            if (!State.Territories.TryGetValue(defenderTerritoryId, out var defender))
            {
                throw new KeyNotFoundException($"Território defensor '{defenderTerritoryId}' não encontrado.");
            }

            if (!string.Equals(attacker.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"O território atacante '{attacker.Name}' não pertence ao jogador ativo.");
            }

            if (string.Equals(defender.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Não é permitido atacar um território que já pertence ao próprio jogador.");
            }

            var defenderOwnerId = defender.OwnerId;

            // Executa o combate
            var combatResult = CombatResolver.Attack(
                attacker,
                defender,
                attackerDiceCount,
                defenderDiceCount,
                diceRoller);

            // Se o território foi conquistado nesta rodada
            if (combatResult.TerritoryConquered)
            {
                activePlayer.ConqueredTerritoryThisTurn = true;

                // Move para o novo território a quantidade de tropas correspondente aos dados atacantes rolados
                var diceRolledCount = combatResult.SortedAttackerDice.Count;
                var armiesToMove = Math.Min(diceRolledCount, attacker.Armies - 1);
                if (armiesToMove < 1) armiesToMove = 1;

                attacker.RemoveArmies(armiesToMove);
                defender.SetOwner(activePlayer.Id, armiesToMove);

                // Verifica se o antigo proprietário foi eliminado
                if (defenderOwnerId != null)
                {
                    var remainingTerritories = State.GetTerritoryCount(defenderOwnerId);
                    if (remainingTerritories == 0)
                    {
                        var eliminatedPlayer = State.GetPlayerById(defenderOwnerId);
                        if (eliminatedPlayer != null && !eliminatedPlayer.IsEliminated)
                        {
                            eliminatedPlayer.MarkEliminated(activePlayer.Id);

                            // Espólio: o jogador que eliminou recebe todas as cartas do jogador derrotado
                            var surrenderedCards = eliminatedPlayer.SurrenderAllCards();
                            foreach (var card in surrenderedCards)
                            {
                                activePlayer.AddCard(card);
                            }
                        }
                    }
                }

                // Verifica se o atacante alcançou seu objetivo de vitória
                if (CheckAndSetVictory(activePlayer))
                {
                    return combatResult;
                }
            }

            return combatResult;
        }

        /// <summary>
        /// Move tropas adicionais do território que acabou de invadir para o território conquistado,
        /// preservando o mínimo obrigatório de 1 tropa no território de origem.
        /// </summary>
        public void MoveAdditionalArmiesAfterConquest(string sourceTerritoryId, string targetTerritoryId, int count)
        {
            if (State.CurrentPhase != TurnPhase.Attack)
            {
                throw new InvalidOperationException("Movimentação pós-conquista só é permitida durante a fase de Ataque.");
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Quantidade de tropas a mover deve ser > 0.");
            }

            var activePlayer = State.CurrentPlayer;
            if (!State.Territories.TryGetValue(sourceTerritoryId, out var source) ||
                !State.Territories.TryGetValue(targetTerritoryId, out var target))
            {
                throw new KeyNotFoundException("Território de origem ou destino não encontrado.");
            }

            if (!string.Equals(source.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(target.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Ambos os territórios devem pertencer ao jogador ativo.");
            }

            if (!source.IsAdjacentTo(target.Id))
            {
                throw new InvalidOperationException("Os territórios devem fazer fronteira direta.");
            }

            if (source.Armies - count < 1)
            {
                throw new InvalidOperationException($"Não é possível mover {count} tropas. Devem permanecer ao menos 1 tropa em {source.Name}.");
            }

            source.RemoveArmies(count);
            target.AddArmies(count);
        }

        /// <summary>
        /// Avança da fase de Ataque para a fase de Remanejamento (Fortificação).
        /// </summary>
        public void AdvanceToFortificationPhase()
        {
            if (State.CurrentPhase != TurnPhase.Attack)
            {
                throw new InvalidOperationException($"Transição inválida para Fortificação a partir de {State.CurrentPhase}.");
            }

            if (State.IsGameOver) return;

            State.CurrentPhase = TurnPhase.Fortification;
        }

        /// <summary>
        /// Executa o remanejamento/fortificação de tropas entre dois territórios do jogador ativo conectados por um caminho contíguo.
        /// </summary>
        public void Fortify(string sourceTerritoryId, string destinationTerritoryId, int armiesToMove)
        {
            if (State.CurrentPhase != TurnPhase.Fortification)
            {
                throw new InvalidOperationException("Remanejamentos só podem ser realizados durante a fase de Fortificação.");
            }

            if (armiesToMove <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(armiesToMove), "Quantidade de tropas a remanejar deve ser > 0.");
            }

            var activePlayer = State.CurrentPlayer;
            if (!State.Territories.TryGetValue(sourceTerritoryId, out var source) ||
                !State.Territories.TryGetValue(destinationTerritoryId, out var destination))
            {
                throw new KeyNotFoundException("Território de origem ou destino não encontrado.");
            }

            if (!string.Equals(source.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(destination.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Ambos os territórios devem pertencer ao jogador ativo.");
            }

            if (source.Armies - armiesToMove < 1)
            {
                throw new InvalidOperationException($"Não é possível mover {armiesToMove} tropas de {source.Name}. Deve permanecer ao menos 1 tropa.");
            }

            if (!State.AreTerritoriesConnected(sourceTerritoryId, destinationTerritoryId, activePlayer.Id))
            {
                throw new InvalidOperationException($"Não há conexão territorial contínua entre '{source.Name}' e '{destination.Name}' dominada pelo jogador {activePlayer.Name}.");
            }

            source.RemoveArmies(armiesToMove);
            destination.AddArmies(armiesToMove);
        }

        /// <summary>
        /// Encerra o turno do jogador ativo:
        /// 1) Se conquistou território na rodada, compra 1 carta de território (DrawCardPhase).
        /// 2) Verifica se o jogador venceu por seu objetivo secreto.
        /// 3) Passa a vez para o próximo jogador ativo (vivo) e inicia sua fase de reforço.
        /// </summary>
        public void EndTurn(Random? drawRng = null)
        {
            if (State.CurrentPhase != TurnPhase.Fortification && State.CurrentPhase != TurnPhase.Attack)
            {
                throw new InvalidOperationException($"Não é possível encerrar o turno a partir da fase {State.CurrentPhase}.");
            }

            if (State.IsGameOver) return;

            var activePlayer = State.CurrentPlayer;

            // Fase de Compra de Carta
            State.CurrentPhase = TurnPhase.DrawCard;
            if (activePlayer.ConqueredTerritoryThisTurn)
            {
                var drawnCard = State.Deck.DrawCard(drawRng);
                if (drawnCard != null)
                {
                    activePlayer.AddCard(drawnCard);
                }
            }

            // Verifica vitória ao término do turno
            if (CheckAndSetVictory(activePlayer))
            {
                return;
            }

            // Transiciona para o fim do turno e próximo jogador
            State.CurrentPhase = TurnPhase.TurnEnded;

            // Localiza o próximo jogador não eliminado
            var totalPlayers = State.Players.Count;
            var nextIndex = State.CurrentPlayerIndex;
            var found = false;

            for (var i = 1; i <= totalPlayers; i++)
            {
                var candidateIndex = (State.CurrentPlayerIndex + i) % totalPlayers;
                if (!State.Players[candidateIndex].IsEliminated)
                {
                    nextIndex = candidateIndex;
                    found = true;
                    // Se o próximo índice for menor ou igual ao atual, uma volta completa de rodada ocorreu
                    if (candidateIndex <= State.CurrentPlayerIndex)
                    {
                        State.TurnNumber++;
                    }
                    break;
                }
            }

            if (!found)
            {
                // Todos os outros jogadores foram eliminados! Vitória do jogador ativo
                State.CurrentPhase = TurnPhase.GameOver;
                State.Winner = activePlayer;
                return;
            }

            // Verifica se restou apenas 1 jogador ativo no jogo todo
            var activePlayers = State.Players.Where(p => !p.IsEliminated).ToList();
            if (activePlayers.Count == 1)
            {
                State.CurrentPhase = TurnPhase.GameOver;
                State.Winner = activePlayers[0];
                return;
            }

            State.CurrentPlayerIndex = nextIndex;
            BeginPlayerTurn(State.CurrentPlayer);
        }

        /// <summary>
        /// Avalia e registra se o jogador indicado completou seu objetivo secreto.
        /// </summary>
        private bool CheckAndSetVictory(Player player)
        {
            if (State.CheckPlayerVictory(player))
            {
                State.CurrentPhase = TurnPhase.GameOver;
                State.Winner = player;
                return true;
            }
            return false;
        }
    }
}
