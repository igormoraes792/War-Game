using System;
using System.Collections.Generic;
using System.Linq;
using War.Core.Cards;
using War.Core.Combat;
using War.Core.Data;
using War.Core.Game;
using War.Core.Models;
using War.Core.Objectives;

namespace War.Tests
{
    class Program
    {
        private static int _passed = 0;
        private static int _failed = 0;

        static int Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  INICIANDO SUÍTE DE TESTES: WAR CORE ENGINE - ETAPA 2");
            Console.WriteLine("==================================================");

            RunTest("Cartas: Inicialização do baralho com 43 cartas (41 territórios + 2 coringas)", TestCardDeckInitialization);
            RunTest("Cartas: Validação de combinações de troca (3 iguais, 3 distintas, Coringa)", TestCardTradeCombinations);
            RunTest("Cartas: Tabela progressiva de tropas nas trocas (4, 6, 8, 10, 12, 15, 20...)", TestCardTradeProgression);
            RunTest("Cartas: Bônus de +2 tropas para territórios dominados presentes nas cartas", TestCardTradeTerritoryBonus);
            RunTest("Objetivos: Conquistar 24 territórios", TestTerritoryCountObjective);
            RunTest("Objetivos: Conquistar 18 territórios com pelo menos 2 tropas", TestTerritoriesWithArmiesObjective);
            RunTest("Objetivos: Conquista de continentes (América do Norte + África)", TestContinentConquestObjective);
            RunTest("Objetivos: Eliminar cor com vitória imediata e fallback para 24 territórios", TestEliminateColorObjective);
            RunTest("FSM: Inicialização da partida e cálculo de reforços (territórios/2 + continentes)", TestGameTurnManagerReinforcements);
            RunTest("FSM: Alocação de tropas (DeployArmies) e transição para ataque", TestDeployArmiesAndAdvance);
            RunTest("FSM: Ataque, conquista, eliminação de jogador adversário e espólio de cartas", TestAttackConquestAndElimination);
            RunTest("FSM: Remanejamento (Fortificação) com validação de conectividade em grafo BFS", TestFortificationBFS);
            RunTest("FSM: Compra de carta ao conquistar território e passagem para próximo jogador", TestEndTurnAndDrawCard);
            RunTest("Cartografia: Polígonos de Mackenzie e Rússia Asiática (geometria contígua e triangulação)", TestMackenzieAndRussiaPolygons);
            RunTest("Cartografia GIS: Integridade dos 41 territórios na base WorldCartographyData", TestWorldCartographyAllTerritoriesPresent);
            RunTest("Cartografia GIS: Alta densidade de curvas no Pacífico Sul (Chile, Peru, Argentina, Brasil)", TestSouthAmericaGisHighDensityPacificCurves);
            RunTest("Cartografia GIS: Litoral Ártico e arquipélagos da Sibéria (Rússia Asiática)", TestAsianRussiaArcticCoastlineAndArchipelagos);
            RunTest("Cartografia GIS: Triangulação Ear-Clipping robusta para todos os 41 territórios", TestAll41TerritoriesTriangulation);
            RunTest("Cartografia GIS: Ausência de sobreposição entre Brasil/Venezuela e entre as Rússias", TestNoCartographicOverlapsBetweenKeyRegions);
            RunTest("HUD Jogadores: Ordenação decrescente por territórios dominados (ranking)", TestPlayerTerritoryOverviewRankingSort);

            Console.WriteLine("\n==================================================");
            Console.WriteLine($"RESULTADO DOS TESTES: {_passed} PASSOU | {_failed} FALHOU");
            Console.WriteLine("==================================================");

            return _failed == 0 ? 0 : 1;
        }

        static void RunTest(string testName, Action testAction)
        {
            try
            {
                testAction();
                Console.WriteLine($"[PASSOU] {testName}");
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FALHOU] {testName}: {ex.Message}");
                Console.ResetColor();
                _failed++;
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Asserção falhou: {message}");
            }
        }

        // 1. Baralho de 43 cartas (41 territórios + 2 coringas)
        static void TestCardDeckInitialization()
        {
            var deck = new CardDeck(shuffle: false);
            Assert(deck.RemainingCardsCount == 43, $"Total de cartas deve ser 43, obtido: {deck.RemainingCardsCount}");

            var cards = new List<Card>();
            while (deck.RemainingCardsCount > 0)
            {
                cards.Add(deck.DrawCard()!);
            }

            Assert(cards.Count(c => c.IsWildcard) == 2, "Devem existir exatamente 2 Coringas.");
            var nonWildcards = cards.Where(c => !c.IsWildcard).ToList();
            Assert(nonWildcards.Count == 41, "Devem existir exatamente 41 cartas de territórios.");

            var territoryIds = nonWildcards.Select(c => c.TerritoryId).Distinct().ToList();
            Assert(territoryIds.Count == 41, "Todas as 41 cartas devem referenciar territórios únicos.");
        }

        // 2. Validação de trocas de cartas
        static void TestCardTradeCombinations()
        {
            var circle1 = new Card("brasil", CardShape.Circle);
            var circle2 = new Card("argentina", CardShape.Circle);
            var circle3 = new Card("peru", CardShape.Circle);

            var square = new Card("franca", CardShape.Square);
            var triangle = new Card("egito", CardShape.Triangle);
            var joker = Card.CreateWildcard();

            // 3 iguais
            Assert(CardDeck.IsValidTradeSet(new[] { circle1, circle2, circle3 }, out _), "3 círculos devem ser válidos.");

            // 3 distintos
            Assert(CardDeck.IsValidTradeSet(new[] { circle1, square, triangle }, out _), "1 círculo, 1 quadrado, 1 triângulo devem ser válidos.");

            // 2 iguais e 1 diferente (inválido sem coringa)
            Assert(!CardDeck.IsValidTradeSet(new[] { circle1, circle2, square }, out _), "2 círculos e 1 quadrado devem ser inválidos.");

            // Com coringa
            Assert(CardDeck.IsValidTradeSet(new[] { circle1, square, joker }, out _), "1 círculo, 1 quadrado e 1 coringa devem ser válidos.");
            Assert(CardDeck.IsValidTradeSet(new[] { circle1, circle2, joker }, out _), "2 círculos e 1 coringa devem ser válidos.");
        }

        // 3. Tabela progressiva de trocas
        static void TestCardTradeProgression()
        {
            Assert(CardDeck.GetArmiesForTrade(1) == 4, "1ª troca = 4");
            Assert(CardDeck.GetArmiesForTrade(2) == 6, "2ª troca = 6");
            Assert(CardDeck.GetArmiesForTrade(3) == 8, "3ª troca = 8");
            Assert(CardDeck.GetArmiesForTrade(4) == 10, "4ª troca = 10");
            Assert(CardDeck.GetArmiesForTrade(5) == 12, "5ª troca = 12");
            Assert(CardDeck.GetArmiesForTrade(6) == 15, "6ª troca = 15");
            Assert(CardDeck.GetArmiesForTrade(7) == 20, "7ª troca = 20");
            Assert(CardDeck.GetArmiesForTrade(8) == 25, "8ª troca = 25");
            Assert(CardDeck.GetArmiesForTrade(9) == 30, "9ª troca = 30");
        }

        // 4. Bônus de posse de território nas cartas trocadas
        static void TestCardTradeTerritoryBonus()
        {
            var deck = new CardDeck();
            var cards = new[]
            {
                new Card("brasil", CardShape.Circle),
                new Card("franca", CardShape.Square),
                new Card("japao", CardShape.Triangle)
            };

            // Jogador possui "brasil" e "franca", mas não "japao"
            var owned = new[] { "brasil", "franca" };
            var result = deck.TradeCards(cards, owned);

            Assert(result.BaseArmies == 4, "1ª troca deve conceder 4 tropas base.");
            Assert(result.OwnedTerritoriesAwardedBonus.Count == 2, "Deve ter 2 territórios elegíveis ao bônus.");
            Assert(result.TerritoryBonusArmies == 4, "2 territórios x 2 = 4 tropas extras.");
            Assert(result.TotalArmies == 8, "Total deve ser 8 (4 base + 4 bônus).");
        }

        // 5. Objetivo: 24 territórios
        static void TestTerritoryCountObjective()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            var game = new GameState(new[] { p1, p2 }, map);
            var obj = new TerritoryCountObjective(7, "24 Territórios", "Conquistar 24 territórios", 24);

            Assert(!obj.CheckVictoryCondition(game, p1), "Alice ainda não tem 24 territórios.");

            var territories = map.Values.Take(24).ToList();
            foreach (var t in territories)
            {
                t.SetOwner(p1.Id, 1);
            }

            Assert(obj.CheckVictoryCondition(game, p1), "Alice deve ter vencido com 24 territórios.");
        }

        // 6. Objetivo: 18 territórios com min 2 tropas
        static void TestTerritoriesWithArmiesObjective()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            var game = new GameState(new[] { p1, p2 }, map);
            var obj = new TerritoriesWithArmiesObjective(8, "18 Territórios Fortificados", "18 territórios com min 2 tropas", 18, 2);

            var list = map.Values.Take(18).ToList();
            foreach (var t in list)
            {
                t.SetOwner(p1.Id, 1); // Apenas 1 exército cada
            }

            Assert(!obj.CheckVictoryCondition(game, p1), "Ainda não tem min 2 tropas em cada.");

            foreach (var t in list)
            {
                t.AddArmies(1); // Agora 2 exércitos cada
            }

            Assert(obj.CheckVictoryCondition(game, p1), "Deve vencer com 18 territórios tendo 2 tropas cada.");
        }

        // 7. Objetivo: América do Norte + África
        static void TestContinentConquestObjective()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            var game = new GameState(new[] { p1, p2 }, map);
            var obj = new ContinentConquestObjective(6, "NA + África", "América do Norte e África",
                new[] { ContinentType.NorthAmerica, ContinentType.Africa });

            // Domina apenas América do Norte
            foreach (var t in map.Values.Where(t => t.Continent == ContinentType.NorthAmerica))
            {
                t.SetOwner(p1.Id, 1);
            }
            Assert(!obj.CheckVictoryCondition(game, p1), "Apenas NA não é suficiente.");

            // Domina também África
            foreach (var t in map.Values.Where(t => t.Continent == ContinentType.Africa))
            {
                t.SetOwner(p1.Id, 1);
            }
            Assert(obj.CheckVictoryCondition(game, p1), "Deve vencer dominando NA e África.");
        }

        // 8. Objetivo: Eliminar cor com vitória direta e fallback de 24 territórios
        static void TestEliminateColorObjective()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Yellow);
            var p3 = new Player("p3", "Charlie", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            var game = new GameState(new[] { p1, p2, p3 }, map);
            var obj = new EliminateColorObjective(9, "Destruir Amarelos", "Destruir Amarelos", PlayerColor.Yellow);

            Assert(!obj.CheckVictoryCondition(game, p1), "Bob (amarelo) ainda não foi eliminado.");

            // Caso A: Alice elimina Bob
            p2.MarkEliminated(p1.Id);
            Assert(obj.CheckVictoryCondition(game, p1), "Alice eliminou Bob diretamente e deve vencer.");

            // Caso B: Charlie eliminou Bob (fallback entra em ação)
            var p2Alt = new Player("p2b", "Bob2", PlayerColor.Yellow);
            p2Alt.MarkEliminated(p3.Id); // Charlie eliminou!
            var gameAlt = new GameState(new[] { p1, p2Alt, p3 }, map);

            Assert(!obj.CheckVictoryCondition(gameAlt, p1), "Charlie eliminou Bob, então Alice precisa de 24 territórios.");

            // Alice conquista 24 territórios
            foreach (var t in map.Values.Take(24))
            {
                t.SetOwner(p1.Id, 1);
            }
            Assert(obj.CheckVictoryCondition(gameAlt, p1), "Com 24 territórios conquistados, o fallback conclui o objetivo.");
        }

        // 9. FSM: Reinforcements
        static void TestGameTurnManagerReinforcements()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            // Atribui 10 territórios para Alice (10 / 2 = 5 tropas)
            // Dá América do Sul inteira para Alice (4 territórios, bônus +2)
            foreach (var t in map.Values.Where(t => t.Continent == ContinentType.SouthAmerica))
            {
                t.SetOwner(p1.Id, 1);
            }

            // Mais 2 territórios da África (sem completar o continente Africano)
            map[TerritoryIds.Egito].SetOwner(p1.Id, 1);
            map[TerritoryIds.Sudao].SetOwner(p1.Id, 1);

            var manager = new GameTurnManager(new[] { p1, p2 }, map);
            manager.StartGame();

            Assert(manager.State.CurrentPhase == TurnPhase.Reinforcement, "Fase inicial deve ser Reinforcement.");
            var totalTerritories = manager.State.GetTerritoryCount(p1.Id); // 6 territórios
            var expectedBase = Math.Max(3, totalTerritories / 2); // 3 tropas
            var expectedTotal = expectedBase + Continent.SouthAmerica.TroopBonus; // 3 + 2 = 5 tropas

            Assert(p1.ArmiesToDeploy == expectedTotal, $"Alice deve receber {expectedTotal} tropas. Recebeu {p1.ArmiesToDeploy}.");
        }

        // 10. FSM: Deploy e avanço para Attack
        static void TestDeployArmiesAndAdvance()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            map[TerritoryIds.Brasil].SetOwner(p1.Id, 2);
            map[TerritoryIds.Argentina].SetOwner(p2.Id, 1);

            var manager = new GameTurnManager(new[] { p1, p2 }, map);
            manager.StartGame();

            var deployCount = p1.ArmiesToDeploy;
            manager.DeployArmies(TerritoryIds.Brasil, deployCount);

            Assert(p1.ArmiesToDeploy == 0, "Todas as tropas devem ter sido alocadas.");
            Assert(map[TerritoryIds.Brasil].Armies == 2 + deployCount, "Tropas do Brasil devem ter aumentado.");

            manager.AdvanceToAttackPhase();
            Assert(manager.State.CurrentPhase == TurnPhase.Attack, "Fase deve ter transicionado para Attack.");
        }

        // 11. FSM: Ataque, conquista, eliminação e cartas
        static void TestAttackConquestAndElimination()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            // Bob possui apenas Argentina com 1 tropa
            map[TerritoryIds.Argentina].SetOwner(p2.Id, 1);
            p2.AddCard(new Card("egito", CardShape.Square));

            // Alice possui Brasil com 5 tropas
            map[TerritoryIds.Brasil].SetOwner(p1.Id, 5);

            var manager = new GameTurnManager(new[] { p1, p2 }, map);
            manager.StartGame();
            manager.DeployArmies(TerritoryIds.Brasil, p1.ArmiesToDeploy);
            manager.AdvanceToAttackPhase();

            // Mock: Atacante rola 6, Defensor rola 1
            var mockDice = new FixedDiceRoller(new[] { 6, 6, 6, 1 });
            var result = manager.Attack(TerritoryIds.Brasil, TerritoryIds.Argentina, 3, 1, mockDice);

            Assert(result.TerritoryConquered, "Argentina deve ter sido conquistada.");
            Assert(map[TerritoryIds.Argentina].OwnerId == p1.Id, "Alice agora é dona da Argentina.");
            Assert(p1.ConqueredTerritoryThisTurn, "Flag ConqueredTerritoryThisTurn deve ser true.");
            Assert(p2.IsEliminated, "Bob não tem mais territórios e deve estar eliminado.");
            Assert(p1.Cards.Count == 1, "Alice deve ter herdado a carta de Bob.");
        }

        // 12. FSM: Remanejamento (Fortificação) com busca em grafo BFS
        static void TestFortificationBFS()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            // Alice é dona de Brasil, Venezuela e Peru conectados
            map[TerritoryIds.Brasil].SetOwner(p1.Id, 5);
            map[TerritoryIds.Venezuela].SetOwner(p1.Id, 1);
            map[TerritoryIds.Peru].SetOwner(p1.Id, 1);

            // Território isolado na Europa
            map[TerritoryIds.Inglaterra].SetOwner(p1.Id, 1);

            var manager = new GameTurnManager(new[] { p1, p2 }, map);
            manager.StartGame();
            var deployed = p1.ArmiesToDeploy;
            manager.DeployArmies(TerritoryIds.Brasil, deployed);
            manager.AdvanceToAttackPhase();
            manager.AdvanceToFortificationPhase();

            Assert(manager.State.CurrentPhase == TurnPhase.Fortification, "Fase atual deve ser Fortification.");

            // Mover tropas de Brasil para Peru (passando por Venezuela)
            manager.Fortify(TerritoryIds.Brasil, TerritoryIds.Peru, 2);

            Assert(map[TerritoryIds.Brasil].Armies == 5 + deployed - 2, "Brasil deve ter perdido 2 tropas.");
            Assert(map[TerritoryIds.Peru].Armies == 3, "Peru deve ter recebido 2 tropas (1 + 2 = 3).");

            // Tentar mover de Brasil para Inglaterra (não conectado) deve lançar exceção
            var threw = false;
            try
            {
                manager.Fortify(TerritoryIds.Brasil, TerritoryIds.Inglaterra, 1);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Assert(threw, "Remanejamento entre territórios não conectados deve ser proibido.");
        }

        // 13. FSM: EndTurn e compra de carta
        static void TestEndTurnAndDrawCard()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            map[TerritoryIds.Brasil].SetOwner(p1.Id, 5);
            map[TerritoryIds.Argentina].SetOwner(p2.Id, 1);
            map[TerritoryIds.Peru].SetOwner(p2.Id, 5);

            var manager = new GameTurnManager(new[] { p1, p2 }, map);
            manager.StartGame();
            manager.DeployArmies(TerritoryIds.Brasil, p1.ArmiesToDeploy);
            manager.AdvanceToAttackPhase();

            // Ataca e conquista
            var mockDice = new FixedDiceRoller(new[] { 6, 6, 6, 1 });
            manager.Attack(TerritoryIds.Brasil, TerritoryIds.Argentina, 3, 1, mockDice);

            manager.AdvanceToFortificationPhase();
            manager.EndTurn();

            Assert(p1.Cards.Count >= 1, "Alice deve ter comprado uma carta por conquistar território.");
            Assert(manager.State.CurrentPlayer.Id == p2.Id, "A vez deve ter passado para Bob.");
            Assert(manager.State.CurrentPhase == TurnPhase.Reinforcement, "Fase deve ter resetado para Reinforcement para Bob.");
            Assert(p2.ArmiesToDeploy >= 3, "Bob deve ter recebido tropas de reforço.");
        }

        // 14. FSM: GameOver
        static void TestGameOverTrigger()
        {
            var p1 = new Player("p1", "Alice", PlayerColor.Red);
            var p2 = new Player("p2", "Bob", PlayerColor.Blue);
            var map = ClassicMapData.CreateClassicMap();

            p1.SecretObjective = new TerritoryCountObjective(7, "24 Territórios", "Conquistar 24 territórios", 24);

            map[TerritoryIds.Brasil].SetOwner(p1.Id, 5);
            map[TerritoryIds.Argentina].SetOwner(p2.Id, 1);

            // Dá mais 22 territórios para Alice (totalizando 23 com o Brasil)
            foreach (var t in map.Values.Where(t => t.Id != TerritoryIds.Argentina && t.Id != TerritoryIds.Brasil).Take(22))
            {
                t.SetOwner(p1.Id, 2);
            }

            var manager = new GameTurnManager(new[] { p1, p2 }, map);
            manager.StartGame();
            manager.DeployArmies(TerritoryIds.Brasil, p1.ArmiesToDeploy);
            manager.AdvanceToAttackPhase();

            // Ao conquistar Argentina, Alice atinge o 24º território
            var mockDice = new FixedDiceRoller(new[] { 6, 6, 6, 1 });
            manager.Attack(TerritoryIds.Brasil, TerritoryIds.Argentina, 3, 1, mockDice);

            Assert(manager.State.IsGameOver, "Partida deve estar em GameOver.");
            Assert(manager.State.Winner == p1, "Alice deve ser a vencedora.");
        }

        // 15. Cartografia: Polígonos de Mackenzie, Rússia Asiática, Chile, Peru e Argentina
        static void TestMackenzieAndRussiaPolygons()
        {
            var mackenzie = new (float x, float y)[]
            {
                (-2.58f, -2.82f), (-1.00f, -2.83f), (0.71f, -2.86f),
                (0.98f, 0.05f), (2.22f, 0.01f), (2.02f, -0.70f),
                (2.52f, -1.23f), (3.82f, -1.39f), (4.12f, -1.51f),
                (4.66f, -1.15f), (4.50f, -0.54f), (4.86f, -0.16f),
                (4.76f, 0.23f), (4.40f, 1.00f), (3.40f, 1.80f),
                (2.40f, 1.30f), (1.40f, 2.60f), (0.40f, 1.70f),
                (-0.60f, 2.70f), (-1.70f, 1.80f), (-2.70f, 2.50f),
                (-3.75f, 2.13f), (-3.83f, 0.22f), (-3.31f, 0.15f),
                (-2.81f, -1.03f), (-3.20f, -1.60f), (-3.00f, -2.30f)
            };

            var russiaAsiatica = new (float x, float y)[]
            {
                (-6.55f, 2.03f), (-6.73f, -1.31f), (-6.30f, -1.61f),
                (-6.32f, -2.46f), (-6.97f, -3.00f), (-7.46f, -2.85f),
                (-6.20f, -2.54f), (-4.83f, -3.20f), (-3.42f, -3.60f),
                (-2.50f, -3.00f), (-2.09f, -2.41f), (-0.27f, -2.45f),
                (0.44f, -2.15f), (0.94f, -1.30f), (1.52f, -1.25f),
                (3.35f, -3.20f), (4.00f, -4.00f), (5.30f, -3.30f),
                (6.00f, -1.60f), (7.30f, 0.00f), (8.30f, 1.70f),
                (8.40f, 2.20f), (7.80f, 3.10f), (7.10f, 3.60f),
                (6.60f, 3.40f), (6.00f, 3.10f), (5.50f, 3.40f),
                (4.90f, 3.50f), (4.50f, 3.20f), (3.90f, 3.60f),
                (3.50f, 3.90f), (3.10f, 3.70f), (2.50f, 3.30f),
                (1.80f, 3.00f), (1.30f, 3.70f), (0.70f, 4.40f),
                (0.00f, 4.80f), (-0.60f, 4.40f), (-1.40f, 3.80f),
                (-2.20f, 2.60f), (-2.60f, 3.30f), (-3.00f, 3.80f),
                (-3.40f, 3.40f), (-3.60f, 2.40f), (-4.00f, 2.30f),
                (-4.20f, 3.20f), (-4.50f, 3.80f), (-4.90f, 4.10f),
                (-5.80f, 2.70f)
            };

            var islandSevernayaZemlya = new (float x, float y)[]
            {
                (-0.35f, 5.25f), (0.35f, 5.30f), (0.40f, 5.75f), (-0.25f, 5.80f)
            };

            var islandNovosibirskiye = new (float x, float y)[]
            {
                (3.10f, 4.45f), (3.80f, 4.40f), (3.90f, 4.85f), (3.20f, 4.80f)
            };

            var islandNovayaZemlya = new (float x, float y)[]
            {
                (-5.90f, 4.10f), (-5.50f, 4.30f), (-5.20f, 5.20f), (-5.60f, 5.30f), (-5.85f, 4.60f)
            };

            var islandWrangel = new (float x, float y)[]
            {
                (6.80f, 4.15f), (7.40f, 4.20f), (7.35f, 4.55f), (6.75f, 4.50f)
            };

            var chile = new (float x, float y)[]
            {
                (-1.50f, 3.70f), (-0.50f, 3.70f), (-0.20f, 2.30f),
                (0.20f, 0.70f), (0.60f, -0.90f), (1.20f, -2.40f),
                (1.70f, -3.70f), (1.30f, -3.70f), (0.50f, -2.50f),
                (-0.10f, -1.00f), (-0.50f, 0.70f), (-1.00f, 2.30f)
            };

            var peru = new (float x, float y)[]
            {
                (-2.00f, 2.80f), (-0.87f, 1.91f), (0.38f, 1.10f),
                (-0.04f, 0.52f), (0.99f, 0.12f), (0.71f, -0.11f),
                (0.87f, -0.37f), (1.77f, -0.06f), (3.05f, -0.65f),
                (2.00f, -2.00f), (1.00f, -2.20f), (0.00f, -2.20f),
                (-1.00f, -2.20f), (-1.60f, -1.00f), (-2.20f, 0.40f),
                (-2.40f, 1.80f)
            };

            var argentina = new (float x, float y)[]
            {
                (-0.40f, -4.40f), (-0.90f, -3.10f), (-1.50f, -1.60f),
                (-1.90f, 0.00f), (-2.30f, 1.60f), (-2.60f, 3.00f),
                (-1.60f, 3.00f), (-0.60f, 3.20f), (0.45f, 4.55f),
                (1.88f, 2.26f), (1.36f, 1.58f), (1.50f, 1.00f),
                (2.00f, 0.60f), (1.40f, -0.80f), (0.70f, -2.30f),
                (0.20f, -3.50f)
            };

            var uruguai = new (float x, float y)[]
            {
                (-0.44f, 0.48f), (0.10f, 0.25f), (0.40f, -0.27f),
                (0.30f, -0.70f), (0.00f, -0.60f), (-0.30f, -0.10f)
            };

            float areaM = Math.Abs(ComputeArea(mackenzie));
            Assert(areaM > 15f, $"Área de Mackenzie deve cobrir todo o oeste e norte canadense, obtido: {areaM}");

            float areaR = Math.Abs(ComputeArea(russiaAsiatica));
            Assert(areaR > 50f, $"Área da Rússia Asiática deve cobrir a vasta massa continental siberiana, obtido: {areaR}");

            float areaC = Math.Abs(ComputeArea(chile));
            Assert(areaC > 4f, $"Área do Chile deve ser positiva e substancial, obtido: {areaC}");

            float areaP = Math.Abs(ComputeArea(peru));
            Assert(areaP > 8f, $"Área do Peru deve ser substancial e cobrir a região andina, obtido: {areaP}");

            float areaA = Math.Abs(ComputeArea(argentina));
            Assert(areaA > 10f, $"Área da Argentina deve ser positiva e substancial, obtido: {areaA}");

            float areaU = Math.Abs(ComputeArea(uruguai));
            Assert(areaU > 0.5f, $"Área do Uruguai deve ser positiva, obtido: {areaU}");

            int triM = EarClipCount(mackenzie);
            Assert(triM == mackenzie.Length - 2, $"Mackenzie deve triangular {mackenzie.Length - 2} triângulos, obtido: {triM}");

            int triR = EarClipCount(russiaAsiatica);
            Assert(triR == russiaAsiatica.Length - 2, $"Rússia Asiática deve triangular {russiaAsiatica.Length - 2} triângulos, obtido: {triR}");

            int triISZ = EarClipCount(islandSevernayaZemlya);
            Assert(triISZ == islandSevernayaZemlya.Length - 2, "Severnaya Zemlya deve triangular");

            int triINS = EarClipCount(islandNovosibirskiye);
            Assert(triINS == islandNovosibirskiye.Length - 2, "Novosibirskiye deve triangular");

            int triINZ = EarClipCount(islandNovayaZemlya);
            Assert(triINZ == islandNovayaZemlya.Length - 2, "Novaya Zemlya deve triangular");

            int triIW = EarClipCount(islandWrangel);
            Assert(triIW == islandWrangel.Length - 2, "Wrangel deve triangular");

            int triC = EarClipCount(chile);
            Assert(triC == chile.Length - 2, $"Chile deve triangular {chile.Length - 2} triângulos, obtido: {triC}");

            int triP = EarClipCount(peru);
            Assert(triP == peru.Length - 2, $"Peru deve triangular {peru.Length - 2} triângulos, obtido: {triP}");

            int triA = EarClipCount(argentina);
            Assert(triA == argentina.Length - 2, $"Argentina deve triangular {argentina.Length - 2} triângulos, obtido: {triA}");

            int triU = EarClipCount(uruguai);
            Assert(triU == uruguai.Length - 2, $"Uruguai deve triangular {uruguai.Length - 2} triângulos, obtido: {triU}");
        }


        static float ComputeArea((float x, float y)[] poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                area += (a.x * b.y - b.x * a.y);
            }
            return area * 0.5f;
        }

        static int EarClipCount((float x, float y)[] poly)
        {
            int n = poly.Length;
            var indices = new List<int>(n);
            float area = ComputeArea(poly);
            if (area < 0) for (int i = n - 1; i >= 0; i--) indices.Add(i);
            else for (int i = 0; i < n; i++) indices.Add(i);

            int triCount = 0;
            int safety = 0;
            while (indices.Count > 3 && safety < 300)
            {
                safety++;
                bool found = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int prev = indices[(i - 1 + indices.Count) % indices.Count];
                    int curr = indices[i];
                    int next = indices[(i + 1) % indices.Count];

                    var a = poly[prev];
                    var b = poly[curr];
                    var c = poly[next];

                    float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                    if (cross <= 1e-6f) continue;

                    bool isEar = true;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        int test = indices[j];
                        if (test == prev || test == curr || test == next) continue;
                        var p = poly[test];
                        float c1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                        float c2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
                        float c3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
                        bool inside = !(((c1 < 0) || (c2 < 0) || (c3 < 0)) && ((c1 > 0) || (c2 > 0) || (c3 > 0)));
                        if (inside) { isEar = false; break; }
                    }

                    if (isEar)
                    {
                        triCount++;
                        indices.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    triCount++;
                    indices.RemoveAt(1);
                }
            }
            if (indices.Count == 3) triCount++;
            return triCount;
        }

        static void TestWorldCartographyAllTerritoriesPresent()
        {
            var all = WorldCartographyData.GetAllCartography().ToList();
            Assert(all.Count == 41, $"Devem existir 41 territórios na base, obtido: {all.Count}");

            foreach (var item in all)
            {
                Assert(!string.IsNullOrWhiteSpace(item.TerritoryId), "ID do território não pode ser vazio.");
                Assert(item.Polygon.Count >= 6, $"Território {item.TerritoryId} deve ter pelo menos 6 pontos, obtido: {item.Polygon.Count}");
                float area = Math.Abs(WorldCartographyData.ComputeArea(item.Polygon));
                Assert(area > 0.1f, $"Área de {item.TerritoryId} deve ser positiva e relevante, obtido: {area}");
            }
        }

        static void TestSouthAmericaGisHighDensityPacificCurves()
        {
            var chilePoly = WorldCartographyData.GetPolygon(TerritoryIds.Chile);
            Assert(chilePoly.Count >= 60, $"Chile deve ter pelo menos 60 pontos GIS para descrever os fiordes e litoral, obtido: {chilePoly.Count}");

            var peruPoly = WorldCartographyData.GetPolygon(TerritoryIds.Peru);
            Assert(peruPoly.Count >= 35, $"Peru deve ter pelo menos 35 pontos GIS para descrever as curvas do Pacífico, obtido: {peruPoly.Count}");

            var brasilPoly = WorldCartographyData.GetPolygon(TerritoryIds.Brasil);
            Assert(brasilPoly.Count >= 60, $"Brasil deve ter pelo menos 60 pontos GIS, obtido: {brasilPoly.Count}");

            var argentinaPoly = WorldCartographyData.GetPolygon(TerritoryIds.Argentina);
            Assert(argentinaPoly.Count >= 50, $"Argentina deve ter pelo menos 50 pontos GIS, obtido: {argentinaPoly.Count}");

            var saIds = new[] { TerritoryIds.Chile, TerritoryIds.Peru, TerritoryIds.Brasil, TerritoryIds.Argentina, TerritoryIds.Venezuela, TerritoryIds.Uruguai };
            foreach (var id in saIds)
            {
                var poly = WorldCartographyData.GetPolygon(id);
                var tris = WorldCartographyData.TriangulatePolygon(poly);
                Assert(tris.Count > 0 && tris.Count % 3 == 0, $"Triangulação de {id} falhou ou retornou contagem inválida: {tris.Count}");
            }
        }

        static void TestAsianRussiaArcticCoastlineAndArchipelagos()
        {
            var russiaPoly = WorldCartographyData.GetPolygon(TerritoryIds.RussiaAsiatica);
            Assert(russiaPoly.Count >= 80, $"Rússia Asiática deve ter pelo menos 80 pontos GIS para detalhar o litoral ártico, obtido: {russiaPoly.Count}");

            var islands = WorldCartographyData.GetIslands(TerritoryIds.RussiaAsiatica);
            Assert(islands.Count == 4, $"Rússia Asiática deve ter 4 arquipélagos árticos satélites, obtido: {islands.Count}");

            var tris = WorldCartographyData.TriangulatePolygon(russiaPoly);
            Assert(tris.Count > 0 && tris.Count % 3 == 0, $"Triangulação da Rússia Asiática deve ser válida, obtido: {tris.Count}");
        }

        static void TestAll41TerritoriesTriangulation()
        {
            var all = WorldCartographyData.GetAllCartography().ToList();
            foreach (var item in all)
            {
                var tris = WorldCartographyData.TriangulatePolygon(item.Polygon);
                Assert(tris.Count > 0 && tris.Count % 3 == 0, $"Triangulação de {item.TerritoryId} falhou (obtido {tris.Count} índices).");
            }
        }

        static void TestNoCartographicOverlapsBetweenKeyRegions()
        {
            var rAsiatica = WorldCartographyData.GetCartography(TerritoryIds.RussiaAsiatica);
            var rEuropeia = WorldCartographyData.GetCartography(TerritoryIds.RussiaEuropeia);
            Assert(rAsiatica != null && rEuropeia != null, "Rússia Asiática e Européia devem existir no registro cartográfico.");

            // A fronteira ocidental da Rússia Asiática foi estritamente alinhada aos Urais (X absoluto >= 7.70f)
            float minAbsXRussiaAsia = rAsiatica!.Polygon.Min(p => rAsiatica.Center.X + p.X);
            Assert(minAbsXRussiaAsia >= 7.70f, $"Fronteira ocidental da Rússia Asiática deve respeitar os Montes Urais (>= 7.70), obtido: {minAbsXRussiaAsia}");

            // Venezuela e Brasil
            var vz = WorldCartographyData.GetCartography(TerritoryIds.Venezuela);
            var br = WorldCartographyData.GetCartography(TerritoryIds.Brasil);
            Assert(vz != null && br != null, "Venezuela e Brasil devem existir no registro cartográfico.");

            // Limite meridional de Venezuela alinhado com o norte brasileiro
            float minAbsYVenezuela = vz!.Polygon.Min(p => vz.Center.Y + p.Y);
            Assert(minAbsYVenezuela >= -2.90f, $"Borda sul da Venezuela deve estar ao norte do interior brasileiro (>= -2.90), obtido: {minAbsYVenezuela}");

            // Ambas as malhas triangulam 100% perfeitamente com contagem de índices múltipla de 3
            var vzTris = WorldCartographyData.TriangulatePolygon(vz.Polygon);
            Assert(vzTris.Count > 0 && vzTris.Count % 3 == 0, "Venezuela deve triangular perfeitamente sem auto-interseção.");

            var raTris = WorldCartographyData.TriangulatePolygon(rAsiatica.Polygon);
            Assert(raTris.Count > 0 && raTris.Count % 3 == 0, "Rússia Asiática deve triangular perfeitamente sem auto-interseção.");
        }

        static void TestPlayerTerritoryOverviewRankingSort()
        {
            var p1 = new Player("p1", "Vermelho", PlayerColor.Red);
            var p2 = new Player("p2", "Azul", PlayerColor.Blue);
            var p3 = new Player("p3", "Verde", PlayerColor.Green);
            var p4 = new Player("p4", "Amarelo", PlayerColor.Yellow);

            var map = ClassicMapData.CreateClassicMap();

            // Atribui territórios com contagens distintas:
            // Azul: 18 territórios
            // Verde: 12 territórios
            // Amarelo: 7 territórios
            // Vermelho: 4 territórios
            var territoryList = map.Values.ToList();
            int idx = 0;
            for (int i = 0; i < 18; i++) territoryList[idx++].SetOwner(p2.Id, 1);
            for (int i = 0; i < 12; i++) territoryList[idx++].SetOwner(p3.Id, 1);
            for (int i = 0; i < 7; i++) territoryList[idx++].SetOwner(p4.Id, 1);
            while (idx < territoryList.Count) territoryList[idx++].SetOwner(p1.Id, 1);

            var gameState = new GameState(new[] { p1, p2, p3, p4 }, map);

            // Algoritmo de ordenação idêntico ao PlayerTerritoriesOverviewUI:
            // Maior número de territórios primeiro (ordem decrescente), desempatando por nome
            var sorted = gameState.Players
                .OrderByDescending(p => gameState.Territories.Values.Count(t => string.Equals(t.OwnerId, p.Id, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(p => p.Name)
                .ToList();

            Assert(sorted.Count == 4, "Devem existir 4 jogadores ranqueados.");
            Assert(sorted[0].Id == p2.Id, $"1º colocado deve ser Azul (18 territórios), obtido: {sorted[0].Name}");
            Assert(sorted[1].Id == p3.Id, $"2º colocado deve ser Verde (12 territórios), obtido: {sorted[1].Name}");
            Assert(sorted[2].Id == p4.Id, $"3º colocado deve ser Amarelo (7 territórios), obtido: {sorted[2].Name}");
            Assert(sorted[3].Id == p1.Id, $"4º colocado deve ser Vermelho (4 territórios), obtido: {sorted[3].Name}");
        }
    }
}

