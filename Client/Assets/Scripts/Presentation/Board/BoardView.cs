#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Game;
using War.Core.Models;

namespace War.Presentation.Board
{
    /// <summary>
    /// Gerenciador visual central do tabuleiro 3D.
    /// Indexa todas as instâncias de <see cref="TerritoryView"/> pelo seu TerritoryId e synchroniza com o <see cref="GameState"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardView : MonoBehaviour
    {
        [Header("Lista de Territórios")]
        [Tooltip("Lista manual ou populada automaticamente de todos os TerritoryViews do tabuleiro.")]
        [SerializeField] private List<TerritoryView> registeredTerritories = new();

        [Header("Linhas Táticas de Conexão")]
        [Tooltip("Gerenciador visual das conexões e rotas entre territórios.")]
        [SerializeField] private ConnectionLinesRenderer? connectionLinesRenderer;

        private readonly Dictionary<string, TerritoryView> _viewsById = new(StringComparer.OrdinalIgnoreCase);
        private string? _selectedTerritoryId;

        #region Eventos Públicos
        /// <summary>
        /// Disparado quando qualquer território do tabuleiro é clicado pelo jogador.
        /// </summary>
        public event Action<string>? OnTerritoryClicked;

        /// <summary>
        /// Disparado quando o mouse entra na área de um território.
        /// </summary>
        public event Action<string>? OnTerritoryHoverEnter;

        /// <summary>
        /// Disparado quando o mouse sai da área de um território.
        /// </summary>
        public event Action<string>? OnTerritoryHoverExit;
        #endregion

        #region Propriedades Públicas
        public IReadOnlyDictionary<string, TerritoryView> TerritoriesById => _viewsById;
        public string? SelectedTerritoryId => _selectedTerritoryId;
        public ConnectionLinesRenderer? ConnectionLines => connectionLinesRenderer;
        #endregion

        private void Awake()
        {
            if (connectionLinesRenderer == null)
            {
                connectionLinesRenderer = GetComponentInChildren<ConnectionLinesRenderer>();
            }
            ScanAndIndexTerritories();
        }

        private void OnDestroy()
        {
            UnbindViewEvents();
        }

        /// <summary>
        /// Localiza recursivamente todos os TerritoryViews na hierarquia de filhos e indexa pelo Id.
        /// </summary>
        public void ScanAndIndexTerritories()
        {
            UnbindViewEvents();
            _viewsById.Clear();

            // Se não foi configurado manualmente no Inspector, procura nos filhos
            if (registeredTerritories == null || registeredTerritories.Count == 0)
            {
                registeredTerritories = new List<TerritoryView>(GetComponentsInChildren<TerritoryView>(true));
            }

            foreach (var view in registeredTerritories)
            {
                if (view == null) continue;

                var id = view.TerritoryId;
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!_viewsById.ContainsKey(id))
                {
                    _viewsById.Add(id, view);
                    view.OnClicked += HandleTerritoryClicked;
                    view.OnHoverEnter += HandleTerritoryHoverEnter;
                    view.OnHoverExit += HandleTerritoryHoverExit;
                }
                else
                {
                    Debug.LogWarning($"[BoardView] Território duplicado detectado com ID '{id}' no GameObject '{view.gameObject.name}'.", view);
                }
            }
        }

        private void UnbindViewEvents()
        {
            foreach (var view in _viewsById.Values)
            {
                if (view == null) continue;
                view.OnClicked -= HandleTerritoryClicked;
                view.OnHoverEnter -= HandleTerritoryHoverEnter;
                view.OnHoverExit -= HandleTerritoryHoverExit;
            }
        }

        /// <summary>
        /// Conecta-se ao estado do Core Engine e atualiza todos os territórios de acordo com as tropas e cores dos donos.
        /// </summary>
        public void BindGameState(GameState state, Func<PlayerColor, Color> colorResolver)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (colorResolver == null) throw new ArgumentNullException(nameof(colorResolver));

            foreach (var (territoryId, territoryModel) in state.Territories)
            {
                if (!_viewsById.TryGetValue(territoryId, out var view))
                {
                    continue;
                }

                view.SetArmyCount(territoryModel.Armies);

                if (!string.IsNullOrEmpty(territoryModel.OwnerId))
                {
                    var owner = state.GetPlayerById(territoryModel.OwnerId);
                    if (owner != null)
                    {
                        var playerColor = colorResolver(owner.Color);
                        view.SetOwnerColor(playerColor);
                    }
                }
            }
        }

        /// <summary>
        /// Atualiza visualmente um único território.
        /// </summary>
        public void UpdateTerritory(string territoryId, Color ownerColor, int armyCount)
        {
            if (_viewsById.TryGetValue(territoryId, out var view))
            {
                view.SetOwnerColor(ownerColor);
                view.SetArmyCount(armyCount);
            }
        }

        /// <summary>
        /// Retorna a visualização de um território específico pelo seu ID.
        /// </summary>
        public TerritoryView? GetTerritoryView(string territoryId)
        {
            if (string.IsNullOrWhiteSpace(territoryId)) return null;
            _viewsById.TryGetValue(territoryId, out var view);
            return view;
        }

        /// <summary>
        /// Define qual território está selecionado (ou desmarca a seleção se nulo).
        /// </summary>
        public void SelectTerritory(string? territoryId)
        {
            if (!string.IsNullOrEmpty(_selectedTerritoryId) &&
                _viewsById.TryGetValue(_selectedTerritoryId, out var prevView))
            {
                prevView.SetSelected(false);
            }

            _selectedTerritoryId = territoryId;

            if (!string.IsNullOrEmpty(_selectedTerritoryId) &&
                _viewsById.TryGetValue(_selectedTerritoryId, out var nextView))
            {
                nextView.SetSelected(true);
            }
        }

        /// <summary>
        /// Desmarca qualquer território selecionado e desativa os destaques das linhas.
        /// </summary>
        public void ClearSelection()
        {
            SelectTerritory(null);
            ClearConnectionHighlights();
        }

        /// <summary>
        /// Destaca visualmente as linhas de conexão conectadas a um território selecionado.
        /// </summary>
        public void HighlightConnections(string? territoryId, Func<string, bool>? isEnemyPredicate = null)
        {
            connectionLinesRenderer?.HighlightConnectionsFor(territoryId, isEnemyPredicate);
        }

        /// <summary>
        /// Restaura o estilo neutro de todas as linhas de conexão.
        /// </summary>
        public void ClearConnectionHighlights()
        {
            connectionLinesRenderer?.ClearHighlights();
        }

        /// <summary>
        /// Destaca visualmente um conjunto de territórios (ex: vizinhos válidos para ataque).
        /// </summary>
        public void HighlightTerritories(IEnumerable<string> territoryIds, bool highlight)
        {
            if (territoryIds == null) return;

            foreach (var id in territoryIds)
            {
                if (_viewsById.TryGetValue(id, out var view))
                {
                    view.SetHighlighted(highlight);
                }
            }
        }

        /// <summary>
        /// Remove o destaque visual de todos os territórios do tabuleiro e das conexões.
        /// </summary>
        public void ClearAllHighlights()
        {
            foreach (var view in _viewsById.Values)
            {
                view.SetHighlighted(false);
            }
            ClearConnectionHighlights();
        }

        #region Manipuladores de Eventos
        private void HandleTerritoryClicked(TerritoryView view)
        {
            OnTerritoryClicked?.Invoke(view.TerritoryId);
        }

        private void HandleTerritoryHoverEnter(TerritoryView view)
        {
            OnTerritoryHoverEnter?.Invoke(view.TerritoryId);
        }

        private void HandleTerritoryHoverExit(TerritoryView view)
        {
            OnTerritoryHoverExit?.Invoke(view.TerritoryId);
        }
        #endregion
    }
}

