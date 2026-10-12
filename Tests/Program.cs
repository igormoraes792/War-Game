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

            RunTest("Cartas: Inicialização do baralho com 42 cartas (40 territórios + 2 coringas)", TestCardDeckInitialization);
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
            RunTest("FSM: Vitória da partida encerra em GameOver", TestGameOverTrigger);
            RunTest("Cartografia: Polígonos de Mackenzie e Rússia Asiática (geometria contígua e triangulação)", TestMackenzieAndRussiaPolygons);

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

        // 1. Baralho de 42 cartas (40 territórios + 2 coringas)
        static void TestCardDeckInitialization()
        {
            var deck = new CardDeck(shuffle: false);
            Assert(deck.RemainingCardsCount == 42, $"Total de cartas deve ser 42, obtido: {deck.RemainingCardsCount}");

            var cards = new List<Card>();
            while (deck.RemainingCardsCount > 0)
            {
                cards.Add(deck.DrawCard()!);
            }

            Assert(cards.Count(c => c.IsWildcard) == 2, "Devem existir exatamente 2 Coringas.");
            var nonWildcards = cards.Where(c => !c.IsWildcard).ToList();
            Assert(nonWildcards.Count == 40, "Devem existir exatamente 40 cartas de territórios.");

            var territoryIds = nonWildcards.Select(c => c.TerritoryId).Distinct().ToList();
            Assert(territoryIds.Count == 40, "Todas as 40 cartas devem referenciar territórios únicos.");
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

        // 15. Cartografia: Polígonos de Mackenzie e Rússia Asiática
        static void TestMackenzieAndRussiaPolygons()
        {
            var mackenzie = new (float x, float y)[]
            {
                (-1.88f, -2.62f), (0.00f, -2.63f), (1.41f, -2.66f),
                (1.60f, -1.00f), (2.72f, -0.50f), (2.92f, 0.21f),
                (3.80f, 1.20f), (3.70f, 2.20f), (1.20f, 2.80f),
                (-1.30f, 2.80f), (-3.05f, 2.33f), (-3.13f, 0.42f),
                (-2.61f, 0.35f), (-2.11f, -0.83f), (-2.50f, -1.40f)
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
                (7.70f, 2.80f), (6.00f, 3.40f), (4.00f, 3.60f),
                (1.50f, 3.70f), (-1.00f, 3.50f), (-3.50f, 3.30f),
                (-5.50f, 2.70f)
            };

            float areaM = Math.Abs(ComputeArea(mackenzie));
            Assert(areaM > 10f, $"Área de Mackenzie deve ser substancial para cobrir todo o oeste canadense, obtido: {areaM}");

            float areaR = Math.Abs(ComputeArea(russiaAsiatica));
            Assert(areaR > 50f, $"Área da Rússia Asiática deve cobrir a vasta massa continental siberiana, obtido: {areaR}");

            int triM = EarClipCount(mackenzie);
            Assert(triM == mackenzie.Length - 2, $"Mackenzie deve triangular {mackenzie.Length - 2} triângulos, obtido: {triM}");

            int triR = EarClipCount(russiaAsiatica);
            Assert(triR == russiaAsiatica.Length - 2, $"Rússia Asiática deve triangular {russiaAsiatica.Length - 2} triângulos, obtido: {triR}");
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
    }
}

