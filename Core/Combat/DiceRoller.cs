#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace War.Core.Combat
{
    /// <summary>
    /// Contrato para rolagem de dados no WAR, permitindo implementações aleatórias, determinísticas para testes ou sincronizadas em rede.
    /// </summary>
    public interface IDiceRoller
    {
        /// <summary>
        /// Rola um único dado D6 (1 a 6).
        /// </summary>
        int Roll();

        /// <summary>
        /// Rola uma quantidade de dados D6 e retorna a lista de resultados.
        /// </summary>
        IReadOnlyList<int> RollDice(int count);
    }

    /// <summary>
    /// Rolador padrão de dados baseado em System.Random.
    /// Permite semente fixa para reprodutibilidade determinística.
    /// </summary>
    public sealed class DiceRoller : IDiceRoller
    {
        private readonly Random _random;

        public DiceRoller(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public DiceRoller(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int Roll() => _random.Next(1, 7);

        public IReadOnlyList<int> RollDice(int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "A quantidade de dados deve ser maior ou igual a 1.");

            var results = new int[count];
            for (var i = 0; i < count; i++)
            {
                results[i] = Roll();
            }
            return results;
        }
    }

    /// <summary>
    /// Rolador determinístico útil para testes unitários ou simulações com resultados pré-programados.
    /// </summary>
    public sealed class FixedDiceRoller : IDiceRoller
    {
        private readonly Queue<int> _presetRolls;

        public FixedDiceRoller(IEnumerable<int> presetRolls)
        {
            if (presetRolls == null) throw new ArgumentNullException(nameof(presetRolls));
            _presetRolls = new Queue<int>(presetRolls);
        }

        public int Roll()
        {
            if (_presetRolls.Count == 0)
                throw new InvalidOperationException("Não há mais rolagens pré-definidas na fila.");
            return _presetRolls.Dequeue();
        }

        public IReadOnlyList<int> RollDice(int count)
        {
            var results = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                results.Add(Roll());
            }
            return results;
        }
    }
}
