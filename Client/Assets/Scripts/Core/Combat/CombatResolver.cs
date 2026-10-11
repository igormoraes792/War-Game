#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using War.Core.Models;

namespace War.Core.Combat
{
    /// <summary>
    /// Representa o confronto individual entre um par de dados (atacante vs defensor).
    /// </summary>
    public sealed class DicePairComparison
    {
        public int AttackerDie { get; }
        public int DefenderDie { get; }

        /// <summary>
        /// No WAR clássico, em caso de empate o defensor vence.
        /// </summary>
        public bool DefenderWon => DefenderDie >= AttackerDie;
        public bool AttackerWon => AttackerDie > DefenderDie;

        public DicePairComparison(int attackerDie, int defenderDie)
        {
            AttackerDie = attackerDie;
            DefenderDie = defenderDie;
        }

        public override string ToString() =>
            $"Atacante {AttackerDie} vs {DefenderDie} Defensor => {(DefenderWon ? "Defensor Venceu" : "Atacante Venceu")}";
    }

    /// <summary>
    /// Resultado consolidado de uma rodada de combate no WAR.
    /// </summary>
    public sealed class CombatResult
    {
        public IReadOnlyList<int> RawAttackerDice { get; }
        public IReadOnlyList<int> RawDefenderDice { get; }
        public IReadOnlyList<int> SortedAttackerDice { get; }
        public IReadOnlyList<int> SortedDefenderDice { get; }
        public IReadOnlyList<DicePairComparison> Comparisons { get; }

        public int AttackerArmiesLost { get; }
        public int DefenderArmiesLost { get; }

        /// <summary>
        /// Indica se o defensor perdeu todas as tropas no território atacado durante este combate.
        /// </summary>
        public bool TerritoryConquered { get; }

        public CombatResult(
            IEnumerable<int> attackerDice,
            IEnumerable<int> defenderDice,
            IEnumerable<DicePairComparison> comparisons,
            int attackerArmiesLost,
            int defenderArmiesLost,
            bool territoryConquered = false)
        {
            RawAttackerDice = new ReadOnlyCollection<int>(attackerDice.ToList());
            RawDefenderDice = new ReadOnlyCollection<int>(defenderDice.ToList());
            SortedAttackerDice = new ReadOnlyCollection<int>(RawAttackerDice.OrderByDescending(d => d).ToList());
            SortedDefenderDice = new ReadOnlyCollection<int>(RawDefenderDice.OrderByDescending(d => d).ToList());
            Comparisons = new ReadOnlyCollection<DicePairComparison>(comparisons.ToList());
            AttackerArmiesLost = attackerArmiesLost;
            DefenderArmiesLost = defenderArmiesLost;
            TerritoryConquered = territoryConquered;
        }

        public override string ToString() =>
            $"Combate: Atacante [{string.Join(",", SortedAttackerDice)}] vs [{string.Join(",", SortedDefenderDice)}] Defensor | " +
            $"Perdas: Atacante -{AttackerArmiesLost}, Defensor -{DefenderArmiesLost}" +
            (TerritoryConquered ? " [TERRITÓRIO CONQUISTADO!]" : "");
    }

    /// <summary>
    /// Motor de resolução de combate clássico do WAR brasileiro (Grow / Risk 3x3).
    /// </summary>
    public static class CombatResolver
    {
        public const int MaxAttackerDice = 3;
        public const int MaxDefenderDice = 3;

        /// <summary>
        /// Calcula a quantidade máxima de dados que o território atacante pode rolar.
        /// O atacante precisa manter ao menos 1 tropa ocupando seu território de origem.
        /// </summary>
        public static int GetMaxAttackerDice(int attackerArmies)
        {
            if (attackerArmies <= 1) return 0;
            return Math.Min(MaxAttackerDice, attackerArmies - 1);
        }

        /// <summary>
        /// Calcula a quantidade máxima de dados que o território defensor pode rolar.
        /// O defensor pode defender com até 3 dados, limitados ao total de tropas que possui.
        /// </summary>
        public static int GetMaxDefenderDice(int defenderArmies)
        {
            if (defenderArmies <= 0) return 0;
            return Math.Min(MaxDefenderDice, defenderArmies);
        }

        /// <summary>
        /// Resolve o combate a partir de rolagens de dados já realizadas (determinístico / puramente funcional).
        /// </summary>
        public static CombatResult ResolveRolls(
            IEnumerable<int> attackerDice,
            IEnumerable<int> defenderDice,
            bool territoryConquered = false)
        {
            if (attackerDice == null) throw new ArgumentNullException(nameof(attackerDice));
            if (defenderDice == null) throw new ArgumentNullException(nameof(defenderDice));

            var sortedAttacker = attackerDice.OrderByDescending(d => d).ToList();
            var sortedDefender = defenderDice.OrderByDescending(d => d).ToList();

            var comparisonsCount = Math.Min(sortedAttacker.Count, sortedDefender.Count);
            if (comparisonsCount == 0)
            {
                throw new InvalidOperationException("Não é possível resolver combate com zero dados para um dos lados.");
            }

            var comparisons = new List<DicePairComparison>(comparisonsCount);
            var attackerLosses = 0;
            var defenderLosses = 0;

            for (var i = 0; i < comparisonsCount; i++)
            {
                var comparison = new DicePairComparison(sortedAttacker[i], sortedDefender[i]);
                comparisons.Add(comparison);

                if (comparison.DefenderWon)
                {
                    attackerLosses++;
                }
                else
                {
                    defenderLosses++;
                }
            }

            return new CombatResult(
                attackerDice,
                defenderDice,
                comparisons,
                attackerLosses,
                defenderLosses,
                territoryConquered);
        }

        /// <summary>
        /// Executa uma rodada completa de combate rolando dados através do IDiceRoller especificado.
        /// </summary>
        public static CombatResult ResolveBattleRound(
            int attackerDiceCount,
            int defenderDiceCount,
            IDiceRoller? diceRoller = null)
        {
            if (attackerDiceCount is < 1 or > MaxAttackerDice)
                throw new ArgumentOutOfRangeException(nameof(attackerDiceCount), $"Dados do atacante devem ser entre 1 e {MaxAttackerDice}.");
            if (defenderDiceCount is < 1 or > MaxDefenderDice)
                throw new ArgumentOutOfRangeException(nameof(defenderDiceCount), $"Dados do defensor devem ser entre 1 e {MaxDefenderDice}.");

            var roller = diceRoller ?? new DiceRoller();
            var attackerRolls = roller.RollDice(attackerDiceCount);
            var defenderRolls = roller.RollDice(defenderDiceCount);

            return ResolveRolls(attackerRolls, defenderRolls);
        }

        /// <summary>
        /// Executa um ataque entre dois territórios com todas as validações de regras do WAR,
        /// aplicando diretamente as perdas de tropas aos territórios envolvidos.
        /// </summary>
        public static CombatResult Attack(
            Territory attacker,
            Territory defender,
            int? attackerDiceCount = null,
            int? defenderDiceCount = null,
            IDiceRoller? diceRoller = null)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));

            if (string.Equals(attacker.OwnerId, defender.OwnerId, StringComparison.OrdinalIgnoreCase) && attacker.OwnerId != null)
            {
                throw new InvalidOperationException("Um jogador não pode atacar o próprio território.");
            }

            if (!attacker.IsAdjacentTo(defender.Id))
            {
                throw new InvalidOperationException($"Território atacante '{attacker.Name}' não faz fronteira com defensor '{defender.Name}'.");
            }

            var maxAtkDice = GetMaxAttackerDice(attacker.Armies);
            if (maxAtkDice < 1)
            {
                throw new InvalidOperationException($"Território atacante '{attacker.Name}' não possui tropas suficientes para atacar (mínimo 2 tropas).");
            }

            var maxDefDice = GetMaxDefenderDice(defender.Armies);
            if (maxDefDice < 1)
            {
                throw new InvalidOperationException($"Território defensor '{defender.Name}' não possui tropas.");
            }

            var finalAtkDice = attackerDiceCount ?? maxAtkDice;
            var finalDefDice = defenderDiceCount ?? maxDefDice;

            if (finalAtkDice < 1 || finalAtkDice > maxAtkDice)
            {
                throw new ArgumentOutOfRangeException(nameof(attackerDiceCount), $"Dados de ataque inválidos: {finalAtkDice}. Máximo permitido: {maxAtkDice}.");
            }

            if (finalDefDice < 1 || finalDefDice > maxDefDice)
            {
                throw new ArgumentOutOfRangeException(nameof(defenderDiceCount), $"Dados de defesa inválidos: {finalDefDice}. Máximo permitido: {maxDefDice}.");
            }

            var roller = diceRoller ?? new DiceRoller();
            var attackerRolls = roller.RollDice(finalAtkDice);
            var defenderRolls = roller.RollDice(finalDefDice);

            var roundResult = ResolveRolls(attackerRolls, defenderRolls);

            // Aplica as baixas nos exércitos
            attacker.RemoveArmies(roundResult.AttackerArmiesLost);
            defender.RemoveArmies(roundResult.DefenderArmiesLost);

            var isConquered = defender.Armies == 0;

            return new CombatResult(
                roundResult.RawAttackerDice,
                roundResult.RawDefenderDice,
                roundResult.Comparisons,
                roundResult.AttackerArmiesLost,
                roundResult.DefenderArmiesLost,
                territoryConquered: isConquered);
        }
    }
}
