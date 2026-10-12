#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Game;
using War.Core.Models;

namespace War.Core.Objectives
{
    /// <summary>
    /// Repositório e fábrica de objetivos clássicos do WAR brasileiro.
    /// Contém os 14 objetivos tradicionais e métodos utilitários para distribuição aos jogadores.
    /// </summary>
    public static class ClassicObjectives
    {
        public static IReadOnlyList<Objective> CreateAll()
        {
            return new List<Objective>
            {
                // 1. Europa + Oceania + 1 à escolha
                new ContinentConquestObjective(
                    id: 1,
                    title: "Europa, Oceania e +1",
                    description: "Conquistar na totalidade a EUROPA, a OCEANIA e mais um terceiro continente à sua escolha.",
                    requiredContinents: new[] { ContinentType.Europe, ContinentType.Oceania },
                    additionalAnyContinentCount: 1),

                // 2. Ásia + América do Sul
                new ContinentConquestObjective(
                    id: 2,
                    title: "Ásia e América do Sul",
                    description: "Conquistar na totalidade a ÁSIA e a AMÉRICA DO SUL.",
                    requiredContinents: new[] { ContinentType.Asia, ContinentType.SouthAmerica }),

                // 3. Ásia + África
                new ContinentConquestObjective(
                    id: 3,
                    title: "Ásia e África",
                    description: "Conquistar na totalidade a ÁSIA e a ÁFRICA.",
                    requiredContinents: new[] { ContinentType.Asia, ContinentType.Africa }),

                // 4. América do Norte + América do Sul
                new ContinentConquestObjective(
                    id: 4,
                    title: "América do Norte e América do Sul",
                    description: "Conquistar na totalidade a AMÉRICA DO NORTE e a AMÉRICA DO SUL.",
                    requiredContinents: new[] { ContinentType.NorthAmerica, ContinentType.SouthAmerica }),

                // 5. América do Norte + Oceania
                new ContinentConquestObjective(
                    id: 5,
                    title: "América do Norte e Oceania",
                    description: "Conquistar na totalidade a AMÉRICA DO NORTE e a OCEANIA.",
                    requiredContinents: new[] { ContinentType.NorthAmerica, ContinentType.Oceania }),

                // 6. América do Norte + África
                new ContinentConquestObjective(
                    id: 6,
                    title: "América do Norte e África",
                    description: "Conquistar na totalidade a AMÉRICA DO NORTE e a ÁFRICA.",
                    requiredContinents: new[] { ContinentType.NorthAmerica, ContinentType.Africa }),

                // 7. 24 Territórios
                new TerritoryCountObjective(
                    id: 7,
                    title: "24 Territórios",
                    description: "Conquistar 24 territórios à sua escolha.",
                    requiredTerritories: 24),

                // 8. 18 Territórios com min 2 tropas
                new TerritoriesWithArmiesObjective(
                    id: 8,
                    title: "18 Territórios Fortificados",
                    description: "Conquistar 18 territórios com pelo menos 2 exércitos em cada um.",
                    requiredTerritories: 18,
                    minimumArmiesPerTerritory: 2),

                // 9-14. Destruição de cores clássicas
                new EliminateColorObjective(
                    id: 9,
                    title: "Destruir os Amarelos",
                    description: "Destruir totalmente os exércitos AMARELOS. Se você for o amarelo ou ele não estiver no jogo, conquiste 24 territórios.",
                    targetColor: PlayerColor.Yellow),

                new EliminateColorObjective(
                    id: 10,
                    title: "Destruir os Azuis",
                    description: "Destruir totalmente os exércitos AZUIS. Se você for o azul ou ele não estiver no jogo, conquiste 24 territórios.",
                    targetColor: PlayerColor.Blue),

                new EliminateColorObjective(
                    id: 11,
                    title: "Destruir os Brancos",
                    description: "Destruir totalmente os exércitos BRANCOS. Se você for o branco ou ele não estiver no jogo, conquiste 24 territórios.",
                    targetColor: PlayerColor.White),

                new EliminateColorObjective(
                    id: 12,
                    title: "Destruir os Pretos",
                    description: "Destruir totalmente os exércitos PRETOS. Se você for o preto ou ele não estiver no jogo, conquiste 24 territórios.",
                    targetColor: PlayerColor.Black),

                new EliminateColorObjective(
                    id: 13,
                    title: "Destruir os Vermelhos",
                    description: "Destruir totalmente os exércitos VERMELHOS. Se você for o vermelho ou ele não estiver no jogo, conquiste 24 territórios.",
                    targetColor: PlayerColor.Red),

                new EliminateColorObjective(
                    id: 14,
                    title: "Destruir os Verdes",
                    description: "Destruir totalmente os exércitos VERDES. Se você for o verde ou ele não estiver no jogo, conquiste 24 territórios.",
                    targetColor: PlayerColor.Green),
            };
        }

        /// <summary>
        /// Embaralha e distribui um objetivo secreto único para cada jogador participante.
        /// </summary>
        public static void DistributeObjectives(IEnumerable<Player> players, Random? random = null)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            var playerList = players.ToList();
            var objectives = CreateAll().ToList();

            var rng = random ?? new Random();
            var n = objectives.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                var value = objectives[k];
                objectives[k] = objectives[n];
                objectives[n] = value;
            }

            for (var i = 0; i < playerList.Count; i++)
            {
                if (i >= objectives.Count)
                {
                    throw new InvalidOperationException("Número de jogadores excede os objetivos disponíveis.");
                }
                playerList[i].SecretObjective = objectives[i];
            }
        }
    }
}
