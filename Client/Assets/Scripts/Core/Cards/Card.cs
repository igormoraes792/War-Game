#nullable enable
using System;

namespace War.Core.Cards
{
    /// <summary>
    /// Representa uma carta de território ou coringa no baralho do WAR.
    /// </summary>
    public sealed class Card : IEquatable<Card>
    {
        /// <summary>
        /// Identificador do território associado à carta (ou nulo no caso de Coringa).
        /// </summary>
        public string? TerritoryId { get; }

        /// <summary>
        /// Forma geométrica da carta (Círculo, Quadrado, Triângulo ou Coringa).
        /// </summary>
        public CardShape Shape { get; }

        /// <summary>
        /// Indica se a carta é um Coringa.
        /// </summary>
        public bool IsWildcard => Shape == CardShape.Wildcard;

        public Card(string? territoryId, CardShape shape)
        {
            if (shape == CardShape.Wildcard)
            {
                TerritoryId = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(territoryId))
                {
                    throw new ArgumentException("Cartas normais devem possuir um TerritoryId válido.", nameof(territoryId));
                }
                TerritoryId = territoryId.Trim().ToLowerInvariant();
            }

            Shape = shape;
        }

        public static Card CreateWildcard() => new(null, CardShape.Wildcard);

        public bool Equals(Card? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(TerritoryId, other.TerritoryId, StringComparison.OrdinalIgnoreCase) && Shape == other.Shape;
        }

        public override bool Equals(object? obj) => Equals(obj as Card);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (TerritoryId != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(TerritoryId) : 0);
                return (hash * 397) ^ (int)Shape;
            }
        }

        public override string ToString() =>
            IsWildcard ? "[Carta Coringa]" : $"[Carta {Shape}: {TerritoryId}]";
    }
}
