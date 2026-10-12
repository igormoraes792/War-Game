#nullable enable
using System;
using System.Collections.Generic;

namespace War.Core.Models
{
    /// <summary>
    /// Representa um território do mapa no jogo WAR (POCO sem dependência de engine gráfica).
    /// </summary>
    public sealed class Territory
    {
        private readonly HashSet<string> _adjacentTerritoryIds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Identificador único em formato string invariante (ex: "brasil", "alasca").
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Nome amigável do território (ex: "Brasil", "Alasca").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Nome geopolítico/país soberano correspondente (ex: "EUA Ocidental", "Brasil", "Reino Unido").
        /// </summary>
        public string GeopoliticalName { get; }

        /// <summary>
        /// Continente ao qual o território pertence.
        /// </summary>
        public ContinentType Continent { get; }

        /// <summary>
        /// Identificador do jogador proprietário do território (ou nulo se desocupado).
        /// </summary>
        public string? OwnerId { get; private set; }

        /// <summary>
        /// Quantidade de tropas/exércitos estacionados no território.
        /// No tabuleiro tradicional, sempre deve haver ao menos 1 exército em território ocupado.
        /// </summary>
        public int Armies { get; private set; }

        /// <summary>
        /// Coleção somente leitura dos identificadores dos territórios adjacentes (fronteiras).
        /// </summary>
        public IReadOnlyCollection<string> AdjacentTerritoryIds => _adjacentTerritoryIds;

        public Territory(
            string id,
            string name,
            ContinentType continent,
            string? ownerId = null,
            int armies = 1,
            IEnumerable<string>? adjacentTerritoryIds = null,
            string? geopoliticalName = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id do território não pode ser vazio.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nome do território não pode ser vazio.", nameof(name));
            if (armies < 0)
                throw new ArgumentOutOfRangeException(nameof(armies), "Quantidade de tropas não pode ser negativa.");

            Id = id.Trim().ToLowerInvariant();
            Name = name.Trim();
            GeopoliticalName = !string.IsNullOrWhiteSpace(geopoliticalName) ? geopoliticalName.Trim() : Name;
            Continent = continent;
            OwnerId = ownerId;
            Armies = armies;

            if (adjacentTerritoryIds != null)
            {
                foreach (var neighborId in adjacentTerritoryIds)
                {
                    AddNeighbor(neighborId);
                }
            }
        }

        /// <summary>
        /// Define o novo proprietário do território e ajusta tropas caso necessário.
        /// </summary>
        public void SetOwner(string? newOwnerId, int armies = 1)
        {
            if (armies < 0)
                throw new ArgumentOutOfRangeException(nameof(armies), "Quantidade de tropas não pode ser negativa.");

            OwnerId = newOwnerId;
            Armies = armies;
        }

        /// <summary>
        /// Adiciona tropas ao território.
        /// </summary>
        public void AddArmies(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Quantidade de tropas a adicionar deve ser >= 0.");

            Armies += count;
        }

        /// <summary>
        /// Remove tropas do território.
        /// </summary>
        public void RemoveArmies(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Quantidade de tropas a remover deve ser >= 0.");
            if (count > Armies)
                throw new InvalidOperationException($"Não é possível remover {count} tropas de {Name}, pois há apenas {Armies}.");

            Armies -= count;
        }

        /// <summary>
        /// Adiciona um território vizinho como adjacente.
        /// </summary>
        public bool AddNeighbor(string neighborId)
        {
            if (string.IsNullOrWhiteSpace(neighborId))
                throw new ArgumentException("Id do vizinho não pode ser vazio.", nameof(neighborId));

            var normalized = neighborId.Trim().ToLowerInvariant();
            if (normalized == Id)
                throw new InvalidOperationException("Um território não pode fazer fronteira consigo mesmo.");

            return _adjacentTerritoryIds.Add(normalized);
        }

        /// <summary>
        /// Verifica se este território faz fronteira com o território especificado.
        /// </summary>
        public bool IsAdjacentTo(string neighborId)
        {
            if (string.IsNullOrWhiteSpace(neighborId)) return false;
            return _adjacentTerritoryIds.Contains(neighborId.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Verifica se este território faz fronteira com outro objeto Territory.
        /// </summary>
        public bool IsAdjacentTo(Territory neighbor)
        {
            if (neighbor == null) return false;
            return IsAdjacentTo(neighbor.Id);
        }

        public override string ToString() =>
            $"{Name} [{Continent}] - Dono: {OwnerId ?? "Nenhum"} ({Armies} tropas) - Fronteiras: {_adjacentTerritoryIds.Count}";
    }
}
