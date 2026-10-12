#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Models;

namespace War.Core.Data
{
    /// <summary>
    /// Constantes com os IDs padronizados de todos os 40 territórios do WAR.
    /// Atualizado para divisão geopolítica por países soberanos e regiões.
    /// </summary>
    public static class TerritoryIds
    {
        // América do Norte (8)
        public const string Alasca = "alasca";
        public const string Mackenzie = "mackenzie";
        public const string Groenlandia = "groenlandia";
        public const string Ottawa = "ottawa";
        public const string Labrador = "labrador";
        public const string California = "california";
        public const string NovaYork = "nova_york";
        public const string Mexico = "mexico";

        // América do Sul (5)
        public const string Venezuela = "venezuela";
        public const string Peru = "peru";
        public const string Brasil = "brasil";
        public const string Argentina = "argentina";
        public const string Uruguai = "uruguai";

        // Europa (7)
        public const string Islandia = "islandia";
        public const string Inglaterra = "inglaterra";
        public const string Suecia = "suecia";
        public const string Alemanha = "alemanha";
        public const string Franca = "franca";
        public const string Polonia = "polonia";
        public const string RussiaEuropeia = "russia_europeia";

        // África (6)
        public const string Argelia = "argelia";
        public const string Egito = "egito";
        public const string Sudao = "sudao";
        public const string Congo = "congo";
        public const string AfricaDoSul = "africa_do_sul";
        public const string Madagascar = "madagascar";

        // Ásia (9)
        public const string RussiaAsiatica = "russia_asiatica";
        public const string Cazaquistao = "cazaquistao";
        public const string ArabiaSaudita = "arabia_saudita";
        public const string Ira = "ira";
        public const string China = "china";
        public const string India = "india";
        public const string Mongolia = "mongolia";
        public const string Japao = "japao";
        public const string Vietna = "vietna";

        // Oceania (5)
        public const string Australia = "australia";
        public const string NovaGuine = "nova_guine";
        public const string NovaZelandia = "nova_zelandia";
        public const string IlhasSalomao = "ilhas_salomao";
        public const string Fiji = "fiji";
    }

    /// <summary>
    /// Definição estática imutável de cada território do mapa.
    /// </summary>
    public sealed class TerritoryDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string GeopoliticalName { get; }
        public ContinentType Continent { get; }
        public IReadOnlyCollection<string> Neighbors { get; }

        public TerritoryDefinition(string id, string name, ContinentType continent, IEnumerable<string> neighbors, string? geopoliticalName = null)
        {
            Id = id;
            Name = name;
            GeopoliticalName = !string.IsNullOrWhiteSpace(geopoliticalName) ? geopoliticalName! : name;
            Continent = continent;
            Neighbors = new ReadOnlyCollection<string>(neighbors.ToList());
        }
    }

    /// <summary>
    /// Tabela de dados de territórios do WAR (40 territórios, 6 continentes e grafo completo de adjacências).
    /// </summary>
    public static class ClassicMapData
    {
        public const int TotalTerritoriesCount = 40;

        private static readonly Dictionary<string, TerritoryDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase);

        static ClassicMapData()
        {
            // América do Norte (8)
            RegisterWithGeo(TerritoryIds.Alasca, "Alasca", "Alasca", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.RussiaAsiatica);

            RegisterWithGeo(TerritoryIds.Mackenzie, "Mackenzie", "Mackenzie", ContinentType.NorthAmerica,
                TerritoryIds.Alasca, TerritoryIds.Groenlandia, TerritoryIds.Ottawa, TerritoryIds.California);

            RegisterWithGeo(TerritoryIds.Groenlandia, "Groenlândia", "Groenlândia", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.Labrador, TerritoryIds.Islandia);

            RegisterWithGeo(TerritoryIds.Ottawa, "Ottawa", "Ottawa", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.Labrador, TerritoryIds.California, TerritoryIds.NovaYork);

            RegisterWithGeo(TerritoryIds.Labrador, "Labrador", "Labrador", ContinentType.NorthAmerica,
                TerritoryIds.Groenlandia, TerritoryIds.Ottawa, TerritoryIds.NovaYork);

            RegisterWithGeo(TerritoryIds.California, "Califórnia", "Califórnia", ContinentType.NorthAmerica,
                TerritoryIds.Mackenzie, TerritoryIds.Ottawa, TerritoryIds.NovaYork, TerritoryIds.Mexico);

            RegisterWithGeo(TerritoryIds.NovaYork, "Nova York", "Nova York", ContinentType.NorthAmerica,
                TerritoryIds.Ottawa, TerritoryIds.Labrador, TerritoryIds.California, TerritoryIds.Mexico);

            RegisterWithGeo(TerritoryIds.Mexico, "México", "México", ContinentType.NorthAmerica,
                TerritoryIds.California, TerritoryIds.NovaYork, TerritoryIds.Venezuela);

            // América do Sul (5)
            RegisterWithGeo(TerritoryIds.Venezuela, "Venezuela", "Venezuela", ContinentType.SouthAmerica,
                TerritoryIds.Mexico, TerritoryIds.Peru, TerritoryIds.Brasil);

            RegisterWithGeo(TerritoryIds.Peru, "Peru", "Peru", ContinentType.SouthAmerica,
                TerritoryIds.Venezuela, TerritoryIds.Brasil, TerritoryIds.Argentina);

            RegisterWithGeo(TerritoryIds.Brasil, "Brasil", "Brasil", ContinentType.SouthAmerica,
                TerritoryIds.Venezuela, TerritoryIds.Peru, TerritoryIds.Argentina, TerritoryIds.Uruguai, TerritoryIds.Argelia);

            RegisterWithGeo(TerritoryIds.Argentina, "Argentina", "Argentina", ContinentType.SouthAmerica,
                TerritoryIds.Peru, TerritoryIds.Brasil, TerritoryIds.Uruguai);

            RegisterWithGeo(TerritoryIds.Uruguai, "Uruguai", "Uruguai", ContinentType.SouthAmerica,
                TerritoryIds.Brasil, TerritoryIds.Argentina);

            // Europa (7)
            RegisterWithGeo(TerritoryIds.Islandia, "Islândia", "Islândia", ContinentType.Europe,
                TerritoryIds.Groenlandia, TerritoryIds.Inglaterra);

            RegisterWithGeo(TerritoryIds.Inglaterra, "Inglaterra", "Inglaterra", ContinentType.Europe,
                TerritoryIds.Islandia, TerritoryIds.Suecia, TerritoryIds.Franca, TerritoryIds.Alemanha);

            RegisterWithGeo(TerritoryIds.Suecia, "Suécia", "Suécia", ContinentType.Europe,
                TerritoryIds.Inglaterra, TerritoryIds.RussiaEuropeia);

            RegisterWithGeo(TerritoryIds.Alemanha, "Alemanha", "Alemanha", ContinentType.Europe,
                TerritoryIds.Inglaterra, TerritoryIds.Franca, TerritoryIds.Polonia);

            RegisterWithGeo(TerritoryIds.Franca, "França", "França", ContinentType.Europe,
                TerritoryIds.Inglaterra, TerritoryIds.Alemanha, TerritoryIds.Polonia, TerritoryIds.Argelia);

            RegisterWithGeo(TerritoryIds.Polonia, "Polônia", "Polônia", ContinentType.Europe,
                TerritoryIds.Alemanha, TerritoryIds.Franca, TerritoryIds.RussiaEuropeia, TerritoryIds.Egito, TerritoryIds.Ira);

            RegisterWithGeo(TerritoryIds.RussiaEuropeia, "Rússia Europeia", "Rússia Europeia", ContinentType.Europe,
                TerritoryIds.Suecia, TerritoryIds.Polonia, TerritoryIds.Cazaquistao, TerritoryIds.Ira, TerritoryIds.RussiaAsiatica);

            // África (6)
            RegisterWithGeo(TerritoryIds.Argelia, "Argélia", "Argélia", ContinentType.Africa,
                TerritoryIds.Brasil, TerritoryIds.Franca, TerritoryIds.Egito, TerritoryIds.Sudao, TerritoryIds.Congo);

            RegisterWithGeo(TerritoryIds.Egito, "Egito", "Egito", ContinentType.Africa,
                TerritoryIds.Argelia, TerritoryIds.Polonia, TerritoryIds.ArabiaSaudita, TerritoryIds.Sudao);

            RegisterWithGeo(TerritoryIds.Sudao, "Sudão", "Sudão", ContinentType.Africa,
                TerritoryIds.Argelia, TerritoryIds.Egito, TerritoryIds.Congo, TerritoryIds.AfricaDoSul, TerritoryIds.Madagascar);

            RegisterWithGeo(TerritoryIds.Congo, "Congo", "Congo", ContinentType.Africa,
                TerritoryIds.Argelia, TerritoryIds.Sudao, TerritoryIds.AfricaDoSul);

            RegisterWithGeo(TerritoryIds.AfricaDoSul, "África do Sul", "África do Sul", ContinentType.Africa,
                TerritoryIds.Congo, TerritoryIds.Sudao, TerritoryIds.Madagascar);

            RegisterWithGeo(TerritoryIds.Madagascar, "Madagascar", "Madagascar", ContinentType.Africa,
                TerritoryIds.Sudao, TerritoryIds.AfricaDoSul);

            // Ásia (9)
            RegisterWithGeo(TerritoryIds.RussiaAsiatica, "Rússia Asiática", "Rússia Asiática", ContinentType.Asia,
                TerritoryIds.RussiaEuropeia, TerritoryIds.Cazaquistao, TerritoryIds.Mongolia, TerritoryIds.China, TerritoryIds.Alasca, TerritoryIds.Japao);

            RegisterWithGeo(TerritoryIds.Cazaquistao, "Cazaquistão", "Cazaquistão", ContinentType.Asia,
                TerritoryIds.RussiaEuropeia, TerritoryIds.RussiaAsiatica, TerritoryIds.Ira, TerritoryIds.China, TerritoryIds.India);

            RegisterWithGeo(TerritoryIds.ArabiaSaudita, "Arábia Saudita", "Arábia Saudita", ContinentType.Asia,
                TerritoryIds.Egito, TerritoryIds.Ira);

            RegisterWithGeo(TerritoryIds.Ira, "Irã", "Irã", ContinentType.Asia,
                TerritoryIds.ArabiaSaudita, TerritoryIds.Polonia, TerritoryIds.RussiaEuropeia, TerritoryIds.Cazaquistao, TerritoryIds.India);

            RegisterWithGeo(TerritoryIds.China, "China", "China", ContinentType.Asia,
                TerritoryIds.Cazaquistao, TerritoryIds.RussiaAsiatica, TerritoryIds.Mongolia, TerritoryIds.India, TerritoryIds.Vietna, TerritoryIds.Japao);

            RegisterWithGeo(TerritoryIds.India, "Índia", "Índia", ContinentType.Asia,
                TerritoryIds.Ira, TerritoryIds.Cazaquistao, TerritoryIds.China, TerritoryIds.Vietna);

            RegisterWithGeo(TerritoryIds.Mongolia, "Mongólia", "Mongólia", ContinentType.Asia,
                TerritoryIds.RussiaAsiatica, TerritoryIds.China);

            RegisterWithGeo(TerritoryIds.Japao, "Japão", "Japão", ContinentType.Asia,
                TerritoryIds.RussiaAsiatica, TerritoryIds.China);

            RegisterWithGeo(TerritoryIds.Vietna, "Vietnã", "Vietnã", ContinentType.Asia,
                TerritoryIds.China, TerritoryIds.India, TerritoryIds.Australia, TerritoryIds.NovaGuine);

            // Oceania (5)
            RegisterWithGeo(TerritoryIds.Australia, "Austrália", "Austrália", ContinentType.Oceania,
                TerritoryIds.Vietna, TerritoryIds.NovaGuine, TerritoryIds.NovaZelandia);

            RegisterWithGeo(TerritoryIds.NovaGuine, "Nova Guiné", "Nova Guiné", ContinentType.Oceania,
                TerritoryIds.Vietna, TerritoryIds.Australia, TerritoryIds.IlhasSalomao);

            RegisterWithGeo(TerritoryIds.IlhasSalomao, "Ilhas Salomão", "Ilhas Salomão", ContinentType.Oceania,
                TerritoryIds.NovaGuine, TerritoryIds.Fiji);

            RegisterWithGeo(TerritoryIds.Fiji, "Fiji", "Fiji", ContinentType.Oceania,
                TerritoryIds.IlhasSalomao, TerritoryIds.NovaZelandia);

            RegisterWithGeo(TerritoryIds.NovaZelandia, "Nova Zelândia", "Nova Zelândia", ContinentType.Oceania,
                TerritoryIds.Australia, TerritoryIds.Fiji);

            // Validação de simetria do grafo e integridade na carga estática
            ValidateGraphIntegrity();
        }

        private static void Register(string id, string name, ContinentType continent, params string[] neighbors)
        {
            RegisterWithGeo(id, name, name, continent, neighbors);
        }

        private static void RegisterWithGeo(string id, string name, string geopoliticalName, ContinentType continent, params string[] neighbors)
        {
            Definitions[id] = new TerritoryDefinition(id, name, continent, neighbors, geopoliticalName);
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
        /// Cria uma nova instância de mapa com todos os 40 territórios prontos para uso em jogo.
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
                    armies: 1,
                    geopoliticalName: def.GeopoliticalName);
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
