#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using War.Core.Combat;
using War.Core.Data;
using War.Core.Game;
using War.Core.Models;
using War.Presentation.Board;
using War.Presentation.UI;

namespace War.Presentation.Game
{
    /// <summary>
    /// Presenter principal da arquitetura MVP (Model-View-Presenter).
    /// Coordena o fluxo entre a máquina de estados do Core Engine (<see cref="GameTurnManager"/>) e a visualização (<see cref="BoardView"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class GamePresenter : MonoBehaviour
    {
        [Header("Referências Visuais")]
        [Tooltip("Referência ao gerenciador visual do tabuleiro.")]
        [SerializeField] private BoardView? boardView;

        [Header("Interface e Modais")]
        [Tooltip("Diálogo modal para escolha da quantidade de tropas a posicionar.")]
        [SerializeField] private DeployArmiesDialogUI? deployDialog;

        [Header("Configuração da Partida Local")]
        [SerializeField] private bool autoStartDemoMatch = true;
        [SerializeField] private int numberOfDemoPlayers = 4;

        // Instância central do Core Engine
        private GameTurnManager? _turnManager;

        // Contexto de seleção de território para ações multifásicas
        private string? _selectedSourceTerritoryId;

        #region Eventos para UI / HUD
        public event Action<TurnPhase, Player>? OnPhaseChanged;
        public event Action<string>? OnGameStatusMessage;
        public event Action<CombatResult>? OnCombatResolved;
        public event Action<CombatResult, Territory, Territory>? OnCombatDetailsResolved;
        public event Action<Player>? OnPlayerArmiesChanged;
        public event Action<string, string>? OnTerritoryConquered;
        public event Action<Player>? OnGameOver;
        #endregion

        #region Propriedades Públicas
        public GameTurnManager? TurnManager => _turnManager;
        public GameState? GameState => _turnManager?.State;
        public Player? ActivePlayer => _turnManager?.State.CurrentPlayer;
        public TurnPhase CurrentPhase => _turnManager?.State.CurrentPhase ?? TurnPhase.NotStarted;
        public BoardView? Board => boardView;
        public DeployArmiesDialogUI? DeployDialog
        {
            get => deployDialog;
            set => deployDialog = value;
        }
        #endregion

        private void Awake()
        {
            if (boardView == null)
            {
                boardView = FindFirstObjectByType<BoardView>();
            }

            if (deployDialog == null)
            {
                deployDialog = FindFirstObjectByType<DeployArmiesDialogUI>();
            }
        }

        private void Start()
        {
            if (boardView != null)
            {
                boardView.OnTerritoryClicked += HandleTerritoryClicked;
            }

            if (autoStartDemoMatch)
            {
                StartNewMatch(numberOfDemoPlayers);
            }
        }

        private void OnDestroy()
        {
            if (boardView != null)
            {
                boardView.OnTerritoryClicked -= HandleTerritoryClicked;
            }
        }

        /// <summary>
        /// Inicializa uma partida com jogadores clássicos distribuídos.
        /// </summary>
        public void StartNewMatch(int playerCount = 4)
        {
            playerCount = Mathf.Clamp(playerCount, 2, 6);

            var classicColors = new[]
            {
                PlayerColor.Red,
                PlayerColor.Blue,
                PlayerColor.Green,
                PlayerColor.Yellow,
                PlayerColor.Black,
                PlayerColor.White
            };

            var players = new List<Player>();
            for (var i = 0; i < playerCount; i++)
            {
                var color = classicColors[i];
                var playerName = GetPlayerDefaultName(color);
                players.Add(new Player($"player_{color.ToString().ToLowerInvariant()}", playerName, color));
            }

            // Cria o mapa clássico com os 42 territórios e grafo de adjacências
            var map = ClassicMapData.CreateClassicMap();

            // Distribuição balanceada inicial dos 42 territórios entre os jogadores
            var rng = new System.Random();
            var shuffledTerritories = map.Values.OrderBy(_ => rng.Next()).ToList();
            for (var i = 0; i < shuffledTerritories.Count; i++)
            {
                var assignedPlayer = players[i % players.Count];
                shuffledTerritories[i].SetOwner(assignedPlayer.Id, 1);
            }

            _turnManager = new GameTurnManager(new GameState(players, map));
            _turnManager.StartGame(rng);

            _selectedSourceTerritoryId = null;

            RefreshVisuals();
            NotifyPhaseChanged();
            NotifyStatusMessage($"Partida iniciada! Vez de {ActivePlayer?.Name} - Fase de Reforço ({ActivePlayer?.ArmiesToDeploy} tropas para posicionar).");
        }

        /// <summary>
        /// Atualiza todo o tabuleiro visual com os dados atuais do GameState.
        /// </summary>
        public void RefreshVisuals()
        {
            if (_turnManager == null || boardView == null) return;
            boardView.BindGameState(_turnManager.State, ResolvePlayerColor);
        }

        /// <summary>
        /// Converte enum de cores de jogadores para cores RGB para a renderização na cena.
        /// </summary>
        public static Color ResolvePlayerColor(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Red => new Color(0.88f, 0.22f, 0.22f, 1f),
                PlayerColor.Blue => new Color(0.22f, 0.48f, 0.92f, 1f),
                PlayerColor.Green => new Color(0.18f, 0.78f, 0.28f, 1f),
                PlayerColor.Yellow => new Color(0.96f, 0.85f, 0.15f, 1f),
                PlayerColor.Black => new Color(0.22f, 0.22f, 0.26f, 1f),
                PlayerColor.White => new Color(0.94f, 0.94f, 0.96f, 1f),
                _ => new Color(0.6f, 0.6f, 0.6f, 1f)
            };
        }

        private static string GetPlayerDefaultName(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Red => "Exército Vermelho",
                PlayerColor.Blue => "Exército Azul",
                PlayerColor.Green => "Exército Verde",
                PlayerColor.Yellow => "Exército Amarelo",
                PlayerColor.Black => "Exército Preto",
                PlayerColor.White => "Exército Branco",
                _ => "Jogador"
            };
        }

        #region Máquina de Estados e Interação
        private void HandleTerritoryClicked(string territoryId)
        {
            if (_turnManager == null || _turnManager.State.IsGameOver) return;

            var activePlayer = ActivePlayer;
            if (activePlayer == null) return;

            var state = _turnManager.State;
            if (!state.Territories.TryGetValue(territoryId, out var clickedTerritory)) return;

            var isOwnedByActivePlayer = string.Equals(clickedTerritory.OwnerId, activePlayer.Id, StringComparison.OrdinalIgnoreCase);

            switch (state.CurrentPhase)
            {
                case TurnPhase.Reinforcement:
                    HandleReinforcementClick(clickedTerritory, isOwnedByActivePlayer);
                    break;

                case TurnPhase.Attack:
                    HandleAttackClick(clickedTerritory, isOwnedByActivePlayer);
                    break;

                case TurnPhase.Fortification:
                    HandleFortificationClick(clickedTerritory, isOwnedByActivePlayer);
                    break;
            }
        }

        private void HandleReinforcementClick(Territory clickedTerritory, bool isOwned)
        {
            var activePlayer = ActivePlayer!;

            if (!isOwned)
            {
                NotifyStatusMessage($"Você só pode posicionar tropas em seus próprios territórios! {clickedTerritory.Name} pertence a outro jogador.");
                return;
            }

            if (activePlayer.ArmiesToDeploy <= 0)
            {
                NotifyStatusMessage("Você não possui mais tropas de reforço para posicionar. Avance para a fase de Ataque!");
                return;
            }

            // Seleciona visualmente o território no tabuleiro
            boardView?.SelectTerritory(clickedTerritory.Id);

            // Abre o diálogo modal de alocação de tropas
            if (deployDialog != null)
            {
                deployDialog.Open(
                    clickedTerritory,
                    activePlayer.ArmiesToDeploy,
                    onConfirm: count =>
                    {
                        DeployArmiesToTerritory(clickedTerritory.Id, count);
                    },
                    onCancel: () =>
                    {
                        boardView?.ClearSelection();
                        NotifyStatusMessage("Alocação cancelada.");
                    });
            }
            else
            {
                // Fallback caso o diálogo não esteja instanciado: posiciona 1 tropa diretamente
                DeployArmiesToTerritory(clickedTerritory.Id, 1);
            }
        }

        /// <summary>
        /// Posiciona uma quantidade específica de tropas de reforço no território indicado.
        /// </summary>
        /// <param name="territoryId">ID do território alvo.</param>
        /// <param name="count">Quantidade de tropas a posicionar.</param>
        /// <returns>Verdadeiro se a alocação foi bem-sucedida.</returns>
        public bool DeployArmiesToTerritory(string territoryId, int count)
        {
            if (_turnManager == null || _turnManager.State.IsGameOver) return false;

            var activePlayer = ActivePlayer;
            if (activePlayer == null || count <= 0) return false;

            try
            {
                _turnManager.DeployArmies(territoryId, count);
                boardView?.SelectTerritory(territoryId);
                RefreshVisuals();
                OnPlayerArmiesChanged?.Invoke(activePlayer);

                var terrName = _turnManager.State.Territories.TryGetValue(territoryId, out var t) ? t.Name : territoryId;
                NotifyStatusMessage($"+{count} tropa(s) posicionada(s) em {terrName}. Restam {activePlayer.ArmiesToDeploy} tropas.");

                if (activePlayer.ArmiesToDeploy == 0)
                {
                    NotifyStatusMessage("Todas as tropas foram posicionadas! Você já pode avançar para a fase de Ataque.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GamePresenter] Erro ao alocar tropas: {ex.Message}");
                NotifyStatusMessage(ex.Message);
                return false;
            }
        }

        private void HandleAttackClick(Territory clickedTerritory, bool isOwned)
        {
            var state = _turnManager!.State;

            // Se nenhum atacante está selecionado ainda
            if (string.IsNullOrEmpty(_selectedSourceTerritoryId))
            {
                if (!isOwned)
                {
                    NotifyStatusMessage("Selecione um dos SEUS territórios com pelo menos 2 tropas para iniciar um ataque.");
                    return;
                }

                if (clickedTerritory.Armies < 2)
                {
                    NotifyStatusMessage($"{clickedTerritory.Name} tem apenas 1 tropa e não pode atacar (deve restar ao menos 1 guardando o território).");
                    return;
                }

                _selectedSourceTerritoryId = clickedTerritory.Id;
                boardView?.SelectTerritory(clickedTerritory.Id);

                // Destaca fronteiras inimigas válidas
                var enemyNeighbors = clickedTerritory.AdjacentTerritoryIds
                    .Where(id => state.Territories.TryGetValue(id, out var t) &&
                                 !string.Equals(t.OwnerId, ActivePlayer?.Id, StringComparison.OrdinalIgnoreCase));
                boardView?.ClearAllHighlights();
                boardView?.HighlightTerritories(enemyNeighbors, true);

                NotifyStatusMessage($"Atacante selecionado: {clickedTerritory.Name} ({clickedTerritory.Armies} tropas). Agora clique em um território inimigo vizinho para atacar!");
            }
            else
            {
                // Já temos um território de origem selecionado
                if (string.Equals(_selectedSourceTerritoryId, clickedTerritory.Id, StringComparison.OrdinalIgnoreCase))
                {
                    // Clicou no mesmo: cancela seleção
                    _selectedSourceTerritoryId = null;
                    boardView?.ClearSelection();
                    boardView?.ClearAllHighlights();
                    NotifyStatusMessage("Seleção de ataque cancelada.");
                    return;
                }

                if (isOwned)
                {
                    // Clicou em outro território próprio: troca de atacante
                    if (clickedTerritory.Armies >= 2)
                    {
                        _selectedSourceTerritoryId = clickedTerritory.Id;
                        boardView?.SelectTerritory(clickedTerritory.Id);

                        var enemyNeighbors = clickedTerritory.AdjacentTerritoryIds
                            .Where(id => state.Territories.TryGetValue(id, out var t) &&
                                         !string.Equals(t.OwnerId, ActivePlayer?.Id, StringComparison.OrdinalIgnoreCase));
                        boardView?.ClearAllHighlights();
                        boardView?.HighlightTerritories(enemyNeighbors, true);

                        NotifyStatusMessage($"Atacante alterado para: {clickedTerritory.Name} ({clickedTerritory.Armies} tropas).");
                    }
                    else
                    {
                        NotifyStatusMessage($"{clickedTerritory.Name} tem apenas 1 tropa e não pode atacar.");
                    }
                    return;
                }

                // Clicou em território adversário: executa o combate
                ExecuteAttack(_selectedSourceTerritoryId, clickedTerritory.Id);
            }
        }

        private void ExecuteAttack(string attackerId, string defenderId)
        {
            var state = _turnManager!.State;
            var attacker = state.Territories[attackerId];
            var defender = state.Territories[defenderId];

            if (!attacker.IsAdjacentTo(defender.Id))
            {
                NotifyStatusMessage($"Ataque inválido: {attacker.Name} não faz fronteira com {defender.Name}.");
                return;
            }

            try
            {
                var result = _turnManager.Attack(attackerId, defenderId);
                OnCombatResolved?.Invoke(result);
                OnCombatDetailsResolved?.Invoke(result, attacker, defender);

                var attackerDiceStr = string.Join(", ", result.SortedAttackerDice);
                var defenderDiceStr = string.Join(", ", result.SortedDefenderDice);

                if (result.TerritoryConquered)
                {
                    NotifyStatusMessage($"Vitória! {defender.Name} foi CONQUISTADO! Dados: [{attackerDiceStr}] vs [{defenderDiceStr}]. Baixas: Atacante -{result.AttackerArmiesLost}, Defensor -{result.DefenderArmiesLost}.");
                    OnTerritoryConquered?.Invoke(attackerId, defenderId);

                    // Limpa seleção
                    _selectedSourceTerritoryId = null;
                    boardView?.ClearSelection();
                    boardView?.ClearAllHighlights();
                }
                else
                {
                    NotifyStatusMessage($"Combate em {defender.Name}: Dados [{attackerDiceStr}] vs [{defenderDiceStr}]. Baixas: Atacante -{result.AttackerArmiesLost}, Defensor -{result.DefenderArmiesLost}.");

                    // Se o atacante ficou com apenas 1 tropa, não pode mais atacar
                    if (attacker.Armies <= 1)
                    {
                        _selectedSourceTerritoryId = null;
                        boardView?.ClearSelection();
                        boardView?.ClearAllHighlights();
                        NotifyStatusMessage($"{attacker.Name} ficou com apenas 1 tropa restante. Selecione outro território para atacar.");
                    }
                }

                RefreshVisuals();

                // Verifica se a partida terminou com a vitória do atacante
                if (state.IsGameOver)
                {
                    HandleGameOver(state.Winner);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GamePresenter] Erro no combate: {ex.Message}");
                NotifyStatusMessage(ex.Message);
            }
        }

        private void HandleFortificationClick(Territory clickedTerritory, bool isOwned)
        {
            if (!isOwned)
            {
                NotifyStatusMessage("Na fase de Fortificação, você só pode mover tropas entre seus próprios territórios conectados!");
                return;
            }

            if (string.IsNullOrEmpty(_selectedSourceTerritoryId))
            {
                if (clickedTerritory.Armies < 2)
                {
                    NotifyStatusMessage($"{clickedTerritory.Name} tem apenas 1 tropa e não pode remanejar para outro local.");
                    return;
                }

                _selectedSourceTerritoryId = clickedTerritory.Id;
                boardView?.SelectTerritory(clickedTerritory.Id);
                NotifyStatusMessage($"Origem do remanejamento selecionada: {clickedTerritory.Name}. Agora clique no território destino conectado.");
            }
            else
            {
                if (string.Equals(_selectedSourceTerritoryId, clickedTerritory.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedSourceTerritoryId = null;
                    boardView?.ClearSelection();
                    NotifyStatusMessage("Remanejamento cancelado.");
                    return;
                }

                try
                {
                    _turnManager!.Fortify(_selectedSourceTerritoryId, clickedTerritory.Id, 1);
                    RefreshVisuals();
                    NotifyStatusMessage($"1 tropa remanejada para {clickedTerritory.Name}.");

                    _selectedSourceTerritoryId = null;
                    boardView?.ClearSelection();
                }
                catch (Exception ex)
                {
                    NotifyStatusMessage(ex.Message);
                }
            }
        }
        #endregion

        #region Controle de Fases e Botões de Ação
        /// <summary>
        /// Avança para a próxima fase do turno ativo.
        /// </summary>
        public void AdvancePhase()
        {
            if (_turnManager == null || _turnManager.State.IsGameOver) return;

            var phase = _turnManager.State.CurrentPhase;
            _selectedSourceTerritoryId = null;
            deployDialog?.Close();
            boardView?.ClearSelection();
            boardView?.ClearAllHighlights();

            try
            {
                switch (phase)
                {
                    case TurnPhase.Reinforcement:
                        _turnManager.AdvanceToAttackPhase();
                        NotifyPhaseChanged();
                        NotifyStatusMessage($"Fase de ATAQUE iniciada para {ActivePlayer?.Name}. Selecione seus territórios para atacar.");
                        break;

                    case TurnPhase.Attack:
                        _turnManager.AdvanceToFortificationPhase();
                        NotifyPhaseChanged();
                        NotifyStatusMessage($"Fase de FORTIFICAÇÃO iniciada para {ActivePlayer?.Name}. Remaneje tropas entre territórios conectados.");
                        break;

                    case TurnPhase.Fortification:
                        EndTurn();
                        break;
                }
            }
            catch (Exception ex)
            {
                NotifyStatusMessage(ex.Message);
            }
        }

        /// <summary>
        /// Encerra o turno do jogador atual, compra carta se aplicável e passa a vez para o próximo jogador.
        /// </summary>
        public void EndTurn()
        {
            if (_turnManager == null || _turnManager.State.IsGameOver) return;

            _selectedSourceTerritoryId = null;
            deployDialog?.Close();
            boardView?.ClearSelection();
            boardView?.ClearAllHighlights();

            try
            {
                var prevPlayer = ActivePlayer?.Name;
                _turnManager.EndTurn();

                RefreshVisuals();

                if (_turnManager.State.IsGameOver)
                {
                    HandleGameOver(_turnManager.State.Winner);
                    return;
                }

                NotifyPhaseChanged();
                NotifyStatusMessage($"Turno de {prevPlayer} encerrado. Vez de {ActivePlayer?.Name} - Fase de Reforço ({ActivePlayer?.ArmiesToDeploy} tropas para posicionar).");
            }
            catch (Exception ex)
            {
                NotifyStatusMessage(ex.Message);
            }
        }

        private void HandleGameOver(Player? winner)
        {
            deployDialog?.Close();
            RefreshVisuals();
            OnGameOver?.Invoke(winner!);
            NotifyStatusMessage($"FIM DE JOGO! O vencedor é {winner?.Name} ({winner?.Color})! Parabéns!");
        }

        private void NotifyPhaseChanged()
        {
            if (_turnManager != null && ActivePlayer != null)
            {
                OnPhaseChanged?.Invoke(_turnManager.State.CurrentPhase, ActivePlayer);
            }
        }

        private void NotifyStatusMessage(string message)
        {
            Debug.Log($"[WAR GAME] {message}");
            OnGameStatusMessage?.Invoke(message);
        }
        #endregion
    }
}

