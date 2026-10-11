#nullable enable

namespace War.Core.Game
{
    /// <summary>
    /// Fases da máquina de estados finitos (FSM) de um turno no WAR.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary>
        /// Partida ainda não iniciada ou em configuração prévia.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Fase de reforço: cálculo e alocação de tropas nos territórios do jogador, incluindo trocas de cartas.
        /// </summary>
        Reinforcement,

        /// <summary>
        /// Fase de ataque: realização de combates contra territórios adversários adjacentes.
        /// </summary>
        Attack,

        /// <summary>
        /// Fase de remanejamento/fortificação: movimentação de tropas entre territórios conectados do mesmo jogador.
        /// </summary>
        Fortification,

        /// <summary>
        /// Fase de compra de carta: se conquistou território na rodada, recebe uma carta do baralho.
        /// </summary>
        DrawCard,

        /// <summary>
        /// Fim do turno atual; transição e preparação para o próximo jogador ativo.
        /// </summary>
        TurnEnded,

        /// <summary>
        /// Partida encerrada; um jogador atingiu seu objetivo secreto de vitória.
        /// </summary>
        GameOver
    }
}
