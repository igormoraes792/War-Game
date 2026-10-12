#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using War.Core.Data;
using War.Core.Game;
using War.Core.Models;

namespace War2D.Presentation.Board
{
    /// <summary>
    /// Gerenciador central do tabuleiro 2D de WAR.
    /// Indexa os 42 territórios, sincroniza estado com o GameState do Core,
    /// e orquestra eventos de clique, seleção e iluminação de continentes e conexões.
    /// </summary>
    [DisallowMultipleComponent]
    public class Board2DView : MonoBehaviour
    {
        [Header("Conexões Táticas")]
        [SerializeField] private ConnectionLines2DRenderer? connectionLinesRenderer;

        private readonly Dictionary<string, Territory2DView> _viewsById = new(StringComparer.OrdinalIgnoreCase);
        private string? _selectedTerritoryId;

        public event Action<string>? OnTerritoryClicked;

        private void Awake()
        {
            if (connectionLinesRenderer == null)
            {
                connectionLinesRenderer = GetComponentInChildren<ConnectionLines2DRenderer>();
            }
            ScanAndIndexTerritories();
        }

        private void OnDestroy()
        {
            UnbindViewEvents();
        }

        public void ScanAndIndexTerritories()
        {
            UnbindViewEvents();
            _viewsById.Clear();

            var views = GetComponentsInChildren<Territory2DView>(true);
            foreach (var tv in views)
            {
                var id = tv.TerritoryId;
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!_viewsById.ContainsKey(id))
                {
                    _viewsById.Add(id, tv);
                    tv.OnClicked += HandleTerritoryClicked;
                }
            }
        }

        private void UnbindViewEvents()
        {
            foreach (var view in _viewsById.Values)
            {
                if (view != null) view.OnClicked -= HandleTerritoryClicked;
            }
        }

        private void HandleTerritoryClicked(Territory2DView view)
        {
            OnTerritoryClicked?.Invoke(view.TerritoryId);
        }

        public Territory2DView? GetTerritoryView(string territoryId)
        {
            if (string.IsNullOrWhiteSpace(territoryId)) return null;
            _viewsById.TryGetValue(territoryId, out var view);
            return view;
        }

        public bool TryGetTerritory(string territoryId, out Territory2DView view)
        {
            return _viewsById.TryGetValue(territoryId, out view!);
        }

        public void BindGameState(GameState state, Func<PlayerColor, Color> colorResolver)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (colorResolver == null) throw new ArgumentNullException(nameof(colorResolver));

            foreach (var (territoryId, territoryModel) in state.Territories)
            {
                if (!_viewsById.TryGetValue(territoryId, out var view)) continue;

                view.SetArmyCount(territoryModel.Armies);

                if (!string.IsNullOrEmpty(territoryModel.OwnerId))
                {
                    var owner = state.GetPlayerById(territoryModel.OwnerId);
                    if (owner != null)
                    {
                        view.SetOwnerColor(colorResolver(owner.Color));
                    }
                }
            }
        }

        public void UpdateTerritory(string territoryId, Color ownerColor, int armyCount)
        {
            if (_viewsById.TryGetValue(territoryId, out var view))
            {
                view.SetOwnerColor(ownerColor);
                view.SetArmyCount(armyCount);
            }
        }

        public void SelectTerritory(string? territoryId)
        {
            _selectedTerritoryId = territoryId;
            bool hasSelection = !string.IsNullOrEmpty(_selectedTerritoryId);

            foreach (var kvp in _viewsById)
            {
                var view = kvp.Value;
                if (view == null) continue;

                bool isSelected = hasSelection && string.Equals(kvp.Key, _selectedTerritoryId, StringComparison.OrdinalIgnoreCase);
                view.SetSelected(isSelected);
                view.SetDimmed(hasSelection && !isSelected);
            }
        }

        public void ClearSelection()
        {
            SelectTerritory(null);
            ClearConnectionHighlights();
        }

        public void HighlightConnections(string? territoryId, Func<string, bool>? isEnemyPredicate = null)
        {
            if (string.IsNullOrEmpty(territoryId) || connectionLinesRenderer == null) return;
            connectionLinesRenderer.HighlightConnections(territoryId, targetId => !(isEnemyPredicate?.Invoke(targetId) ?? false));
        }

        public void ClearConnectionHighlights()
        {
            connectionLinesRenderer?.ResetAllHighlights();
        }

        public void HighlightTerritories(IEnumerable<string> territoryIds, bool highlight)
        {
            if (territoryIds == null) return;
            foreach (var id in territoryIds)
            {
                if (_viewsById.TryGetValue(id, out var view))
                {
                    view.SetHighlighted(highlight);
                    if (highlight) view.SetDimmed(false);
                }
            }
        }

        public void ClearAllHighlights()
        {
            foreach (var view in _viewsById.Values)
            {
                view.SetHighlighted(false);
            }
        }

        public void HighlightContinent(ContinentType continent, bool highlight)
        {
            var defs = ClassicMapData.GetTerritoriesByContinent(continent);
            foreach (var def in defs)
            {
                if (_viewsById.TryGetValue(def.Id, out var view))
                {
                    view.SetHighlighted(highlight);
                }
            }
        }

        public static Color GetPlayerColor(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Red => new Color(0.92f, 0.22f, 0.22f, 1f),
                PlayerColor.Blue => new Color(0.22f, 0.52f, 0.95f, 1f),
                PlayerColor.Yellow => new Color(0.96f, 0.82f, 0.18f, 1f),
                PlayerColor.Green => new Color(0.24f, 0.78f, 0.32f, 1f),
                PlayerColor.Black => new Color(0.22f, 0.24f, 0.28f, 1f),
                PlayerColor.White => new Color(0.90f, 0.90f, 0.94f, 1f),
                _ => new Color(0.68f, 0.70f, 0.75f, 1f)
            };
        }
    }
}
