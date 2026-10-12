#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace War.Core.Models
{
    /// <summary>
    /// Identificador dos 6 continentes clássicos do WAR.
    /// </summary>
    public enum ContinentType
    {
        NorthAmerica,
        SouthAmerica,
        Europe,
        Africa,
        Asia,
        Oceania
    }

    /// <summary>
    /// Modelo POCO que representa um continente e suas regras clássicas de bonificação de tropas.
    /// </summary>
    public sealed class Continent : IEquatable<Continent>
    {
        public ContinentType Type { get; }
        public string Name { get; }
        public int TroopBonus { get; }

        public Continent(ContinentType type, string name, int troopBonus)
        {
            Type = type;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TroopBonus = troopBonus;
        }

        public static Continent NorthAmerica => new(ContinentType.NorthAmerica, "América do Norte", 5);
        public static Continent SouthAmerica => new(ContinentType.SouthAmerica, "América do Sul", 2);
        public static Continent Europe => new(ContinentType.Europe, "Europa", 5);
        public static Continent Africa => new(ContinentType.Africa, "África", 3);
        public static Continent Asia => new(ContinentType.Asia, "Ásia", 7);
        public static Continent Oceania => new(ContinentType.Oceania, "Oceania", 2);

        private static readonly IReadOnlyDictionary<ContinentType, Continent> AllContinents =
            new ReadOnlyDictionary<ContinentType, Continent>(new Dictionary<ContinentType, Continent>
            {
                [ContinentType.NorthAmerica] = NorthAmerica,
                [ContinentType.SouthAmerica] = SouthAmerica,
                [ContinentType.Europe] = Europe,
                [ContinentType.Africa] = Africa,
                [ContinentType.Asia] = Asia,
                [ContinentType.Oceania] = Oceania
            });

        /// <summary>
        /// Retorna todos os 6 continentes clássicos.
        /// </summary>
        public static IReadOnlyCollection<Continent> GetAll() => new ReadOnlyCollection<Continent>(new List<Continent>(AllContinents.Values));

        /// <summary>
        /// Obtém o continente correspondente ao tipo.
        /// </summary>
        public static Continent Get(ContinentType type)
        {
            if (AllContinents.TryGetValue(type, out var continent))
            {
                return continent;
            }
            throw new ArgumentOutOfRangeException(nameof(type), $"Continente não reconhecido: {type}");
        }

        /// <summary>
        /// Obtém a quantidade de tropas extras por rodada concedida ao jogador que domina o continente.
        /// </summary>
        public static int GetBonus(ContinentType type) => Get(type).TroopBonus;

        public bool Equals(Continent? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type;
        }

        public override bool Equals(object? obj) => Equals(obj as Continent);

        public override int GetHashCode() => (int)Type;

        public override string ToString() => $"{Name} (+{TroopBonus} tropas)";
    }
}
