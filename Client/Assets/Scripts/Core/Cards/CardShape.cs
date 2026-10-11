#nullable enable

namespace War.Core.Cards
{
    /// <summary>
    /// Formas geométricas clássicas presentes nas cartas de território do WAR.
    /// </summary>
    public enum CardShape
    {
        /// <summary>
        /// Círculo / Esfera.
        /// </summary>
        Circle,

        /// <summary>
        /// Quadrado / Bloco.
        /// </summary>
        Square,

        /// <summary>
        /// Triângulo.
        /// </summary>
        Triangle,

        /// <summary>
        /// Coringa (contém os 3 símbolos e substitui qualquer formato).
        /// </summary>
        Wildcard
    }
}
