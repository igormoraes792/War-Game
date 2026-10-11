#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Models;

namespace War.Core.Data
{
    /// <summary>
    /// Constantes com os IDs padronizados de todos os 42 territórios clássicos do WAR brasileiro.
    /// </summary>
    public static class TerritoryIds
    {
        // América do Norte (9)
        public const string Alasca = "alasca";
        public const string Mackenzie = "mackenzie";
        public const string Groenlandia = "groenlandia";
        public const string Vancouver = "vancouver";
        public const string Ottawa = "ottawa";
        public const string Labrador = "labrador";
        public const string California = "california";
        public const string NovaYork = "nova_york";
        public const string Mexico = "mexico";

        // América do Sul (4)
        public const string Venezuela = "venezuela";
        public const string Peru = "peru";
        public const string Brasil = "brasil";
        public const string Argentina = "argentina";

        // Europa (7)
        public const string Islandia = "islandia";
        public const string Inglaterra = "inglaterra";
        public const string Suecia = "suecia";
        public const string Alemanha = "alemanha";
        public const string Franca = "franca";
        public const string Polonia = "polonia";
        public const string Moscou = "moscou";

        // África (6)
        public const string Nigeria = "nigeria"; // Representa Argélia/Nigéria no mapa clássico
        public const string Egito = "egito";
        public const string Sudao = "sudao";
        public const string Congo = "congo";
        public const string AfricaDoSul = "africa_do_sul";
        public const string Madagascar = "madagascar";

        // Ásia (12)
        public const string OrienteMedio = "oriente_medio";
        public const string Aral = "aral";
        public const string Omsk = "omsk";
        public const string Dudinka = "dudinka";
        public const string Siberia = "siberia";
        public const string Tchita = "tchita";
        public const string Mongolia = "mongolia";
        public const string Vladivostok = "vladivostok";
        public const string China = "china";
        public const string Japao = "japao";
        public const string India = "india";
        public const string Vietna = "vietna";

        // Oceania (4)
        public const string Sumatra = "sumatra";
        public const string Borneo = "borneo";
        public const string NovaGuine = "nova_guine";
        public const string Australia = "australia";
    }

    /// <summary>
    /// Definição estática imutável de cada território clássico.
    /// </summary>
    public sealed class TerritoryDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ContinentType Continent { get; }
        public IReadOnlyCollection<string> Neighbors { get; }

        public TerritoryDefinition(string id, string name, ContinentType continent, IEnumerable<string> neighbors)
        {
            Id = id;
            Name = name;
            Continent = continent;
            Neighbors = new ReadOnlyCollection<string>(neighbors.ToList());
        }
    }

    /// <summary>
    /// Tabela de dados clássicos do WAR brasileiro (42 territórios, 6 continentes e grafo completo de adjacências).
    /// </summary>
    public static class ClassicMapData
    {
        public const int TotalTerritoriesCount = 42;

        private static readonly Dictionary<string, TerritoryDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase);

        static ClassicMapData()
        {
            Register(TerritoryIds.Alasca, "Alasca", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.Vancouver, TerritoryIds.Vladivostok);

            Register(TerritoryIds.Mackenzie, "Mackenzie", ContinentType.NorthAmerica,
                TerritoryIds.Alasca, TerritoryIds.Vancouver, TerritoryIds.Ottawa, TerritoryIds.Groenlandia);

            Register(TerritoryIds.Groenlandia, "Groenlândia", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.Labrador, TerritoryIds.Islandia);

            Register(TerritoryIds.Vancouver, "Vancouver", ContinentType.NorthAmerica,
                TerritoryIds.Alasca, TerritoryIds.Mackenzie, TerritoryIds.Ottawa, TerritoryIds.California);

            Register(TerritoryIds.Ottawa, "Ottawa", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.Vancouver, TerritoryIds.Labrador, TerritoryIds.California, TerritoryIds.NovaYork);

            Register(TerritoryIds.Labrador, "Labrador", ContinentType.NorthAmerica,
                TerritoryIds.Groenlandia, TerritoryIds.Ottawa, TerritoryIds.NovaYork);

            Register(TerritoryIds.California, "Califórnia", ContinentType.NorthAmerica,
                TerritoryIds.Vancouver, TerritoryIds.Ottawa, TerritoryIds.NovaYork, TerritoryIds.Mexico);

            Register(TerritoryIds.NovaYork, "Nova York", ContinentType.NorthAmerica,
                TerritoryIds.Ottawa, TerritoryIds.Labrador, TerritoryIds.California, TerritoryIds.Mexico);

            Register(TerritoryIds.Mexico, "México", ContinentType.NorthAmerica,
                TerritoryIds.California, TerritoryIds.NovaYork, TerritoryIds.Venezuela);

            // América do Sul (4)
            Register(TerritoryIds.Venezuela, "Venezuela", ContinentType.SouthAmerica,
                TerritoryIds.Mexico, TerritoryIds.Peru, TerritoryIds.Brasil);

            Register(TerritoryIds.Peru, "Peru", ContinentType.SouthAmerica,
                TerritoryIds.Venezuela, TerritoryIds.Brasil, TerritoryIds.Argentina);

            Register(TerritoryIds.Brasil, "Brasil", ContinentType.SouthAmerica,
                TerritoryIds.Venezuela, TerritoryIds.Peru, TerritoryIds.Argentina, TerritoryIds.Nigeria);

            Register(TerritoryIds.Argentina, "Argentina", ContinentType.SouthAmerica,
                TerritoryIds.Peru, TerritoryIds.Brasil);

            // Europa (7)
            Register(TerritoryIds.Islandia, "Islândia", ContinentType.Europe,
                TerritoryIds.Groenlandia, TerritoryIds.Inglaterra);

            Register(TerritoryIds.Inglaterra, "Inglaterra", ContinentType.Europe,
                TerritoryIds.Islandia, TerritoryIds.Suecia, TerritoryIds.Franca, TerritoryIds.Alemanha);

            Register(TerritoryIds.Suecia, "Suécia", ContinentType.Europe,
                TerritoryIds.Inglaterra, TerritoryIds.Moscou);

            Register(TerritoryIds.Alemanha, "Alemanha", ContinentType.Europe,
                TerritoryIds.Inglaterra, TerritoryIds.Franca, TerritoryIds.Polonia);

            Register(TerritoryIds.Franca, "França", ContinentType.Europe,
                TerritoryIds.Inglaterra, TerritoryIds.Alemanha, TerritoryIds.Polonia, TerritoryIds.Nigeria);

            Register(TerritoryIds.Polonia, "Polônia", ContinentType.Europe,
                TerritoryIds.Alemanha, TerritoryIds.Franca, TerritoryIds.Moscou, TerritoryIds.Egito, TerritoryIds.OrienteMedio);

            Register(TerritoryIds.Moscou, "Moscou", ContinentType.Europe,
                TerritoryIds.Suecia, TerritoryIds.Polonia, TerritoryIds.OrienteMedio, TerritoryIds.Aral, TerritoryIds.Omsk);

            // África (6)
            Register(TerritoryIds.Nigeria, "Nigéria", ContinentType.Africa,
                TerritoryIds.Brasil, TerritoryIds.Franca, TerritoryIds.Egito, TerritoryIds.Sudao, TerritoryIds.Congo);

            Register(TerritoryIds.Egito, "Egito", ContinentType.Africa,
                TerritoryIds.Nigeria, TerritoryIds.Polonia, TerritoryIds.OrienteMedio, TerritoryIds.Sudao);

            Register(TerritoryIds.Sudao, "Sudão", ContinentType.Africa,
                TerritoryIds.Nigeria, TerritoryIds.Egito, TerritoryIds.Congo, TerritoryIds.AfricaDoSul, TerritoryIds.Madagascar);

            Register(TerritoryIds.Congo, "Congo", ContinentType.Africa,
                TerritoryIds.Nigeria, TerritoryIds.Sudao, TerritoryIds.AfricaDoSul);

            Register(TerritoryIds.AfricaDoSul, "África do Sul", ContinentType.Africa,
                TerritoryIds.Congo, TerritoryIds.Sudao, TerritoryIds.Madagascar);

            Register(TerritoryIds.Madagascar, "Madagascar", ContinentType.Africa,
                TerritoryIds.Sudao, TerritoryIds.AfricaDoSul);

            // Ásia (12)
            Register(TerritoryIds.OrienteMedio, "Oriente Médio", ContinentType.Asia,
                TerritoryIds.Polonia, TerritoryIds.Moscou, TerritoryIds.Egito, TerritoryIds.Aral, TerritoryIds.India);

            Register(TerritoryIds.Aral, "Aral", ContinentType.Asia,
                TerritoryIds.Moscou, TerritoryIds.OrienteMedio, TerritoryIds.Omsk, TerritoryIds.India, TerritoryIds.China);

            Register(TerritoryIds.Omsk, "Omsk", ContinentType.Asia,
                TerritoryIds.Moscou, TerritoryIds.Aral, TerritoryIds.Dudinka, TerritoryIds.Mongolia, TerritoryIds.China);

            Register(TerritoryIds.Dudinka, "Dudinka", ContinentType.Asia,
                TerritoryIds.Omsk, TerritoryIds.Siberia, TerritoryIds.Tchita, TerritoryIds.Mongolia);

            Register(TerritoryIds.Siberia, "Sibéria", ContinentType.Asia,
                TerritoryIds.Dudinka, TerritoryIds.Tchita, TerritoryIds.Vladivostok);

            Register(TerritoryIds.Tchita, "Tchita", ContinentType.Asia,
                TerritoryIds.Dudinka, TerritoryIds.Siberia, TerritoryIds.Mongolia, TerritoryIds.China, TerritoryIds.Vladivostok);

            Register(TerritoryIds.Mongolia, "Mongólia", ContinentType.Asia,
                TerritoryIds.Omsk, TerritoryIds.Dudinka, TerritoryIds.Tchita, TerritoryIds.China, TerritoryIds.Vladivostok);

            Register(TerritoryIds.Vladivostok, "Vladivostok", ContinentType.Asia,
                TerritoryIds.Alasca, TerritoryIds.Siberia, TerritoryIds.Tchita, TerritoryIds.Mongolia, TerritoryIds.China, TerritoryIds.Japao);

            Register(TerritoryIds.China, "China", ContinentType.Asia,
                TerritoryIds.Aral, TerritoryIds.Omsk, TerritoryIds.Mongolia, TerritoryIds.Tchita, TerritoryIds.Vladivostok, TerritoryIds.Japao, TerritoryIds.India, TerritoryIds.Vietna);

            Register(TerritoryIds.Japao, "Japão", ContinentType.Asia,
                TerritoryIds.Vladivostok, TerritoryIds.China);

            Register(TerritoryIds.India, "Índia", ContinentType.Asia,
                TerritoryIds.OrienteMedio, TerritoryIds.Aral, TerritoryIds.China, TerritoryIds.Vietna, TerritoryIds.Sumatra);

            Register(TerritoryIds.Vietna, "Vietnã", ContinentType.Asia,
                TerritoryIds.China, TerritoryIds.India, TerritoryIds.Borneo);

            // Oceania (4)
            Register(TerritoryIds.Sumatra, "Sumatra", ContinentType.Oceania,
                TerritoryIds.India, TerritoryIds.Australia);

            Register(TerritoryIds.Borneo, "Bornéu", ContinentType.Oceania,
                TerritoryIds.Vietna, TerritoryIds.NovaGuine, TerritoryIds.Australia);

            Register(TerritoryIds.NovaGuine, "Nova Guiné", ContinentType.Oceania,
                TerritoryIds.Borneo, TerritoryIds.Australia);

            Register(TerritoryIds.Australia, "Austrália", ContinentType.Oceania,
                TerritoryIds.Sumatra, TerritoryIds.Borneo, TerritoryIds.NovaGuine);

            // Validação de simetria do grafo e integridade na carga estática
            ValidateGraphIntegrity();
        }

        private static void Register(string id, string name, ContinentType continent, params string[] neighbors)
        {
            Definitions[id] = new TerritoryDefinition(id, name, continent, neighbors);
        }

        /// <summary>
        /// Valida se todas as conexões são simétricas (se A é vizinho de B, B deve ser vizinho de A) e existem.
        /// </summary>
        private static void ValidateGraphIntegrity()
        {
            if (Definitions.Count != TotalTerritoriesCount)
            {
                throw new InvalidOperationException($"O mapa deve conter exatamente {TotalTerritoriesCount} territórios, mas contém {Definitions.Count}.");
            }

            foreach (var kvp in Definitions)
            {
                var sourceId = kvp.Key;
                var def = kvp.Value;

                foreach (var neighborId in def.Neighbors)
                {
                    if (!Definitions.TryGetValue(neighborId, out var neighborDef))
                    {
                        throw new InvalidOperationException($"Território '{sourceId}' referencia vizinho inexistente '{neighborId}'.");
                    }

                    if (!neighborDef.Neighbors.Contains(sourceId, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Fronteira assimétrica detectada entre '{sourceId}' e '{neighborId}'.");
                    }
                }
            }
        }

        /// <summary>
        /// Obtém a definição estática de um território.
        /// </summary>
        public static TerritoryDefinition GetDefinition(string territoryId)
        {
            if (Definitions.TryGetValue(territoryId, out var def))
            {
                return def;
            }
            throw new KeyNotFoundException($"Território '{territoryId}' não encontrado no mapa clássico.");
        }

        /// <summary>
        /// Retorna todas as definições de territórios do mapa.
        /// </summary>
        public static IReadOnlyCollection<TerritoryDefinition> GetAllDefinitions() => Definitions.Values;

        /// <summary>
        /// Cria uma nova instância de mapa com todos os 42 territórios prontos para uso em jogo.
        /// </summary>
        public static Dictionary<string, Territory> CreateClassicMap()
        {
            var map = new Dictionary<string, Territory>(StringComparer.OrdinalIgnoreCase);

            // Primeiro cria as instâncias
            foreach (var def in Definitions.Values)
            {
                map[def.Id] = new Territory(
                    id: def.Id,
                    name: def.Name,
                    continent: def.Continent,
                    ownerId: null,
                    armies: 1);
            }

            // Depois conecta todas as adjacências
            foreach (var def in Definitions.Values)
            {
                var territory = map[def.Id];
                foreach (var neighborId in def.Neighbors)
                {
                    territory.AddNeighbor(neighborId);
                }
            }

            return map;
        }

        /// <summary>
        /// Retorna os territórios que compõem um determinado continente.
        /// </summary>
        public static IEnumerable<TerritoryDefinition> GetTerritoriesByContinent(ContinentType continent) =>
            Definitions.Values.Where(t => t.Continent == continent);
    }
}
