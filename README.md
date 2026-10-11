# War Strategy Game (Digital Board Game)

Projeto de jogo de estratégia em turnos inspirado no clássico WAR / Risk, desenvolvido em **Unity** com arquitetura desacoplada (Core Engine em C# POCO + Camada de Apresentação + Suporte a Multiplayer Online).

---

## 🏛️ Arquitetura dos Agentes Especialistas

O desenvolvimento deste projeto é distribuído em agentes autônomos especializados:

1. **`core-rules-agent` (Arquiteto de Regras & Lógica Pura)**:
   - Grafo de adjacências dos 42 territórios e 6 continentes.
   - Máquina de estados de turnos (FSM): Distribuição de Tropas ➔ Ataque ➔ Remanejamento ➔ Compra de Cartas.
   - Combate probabilístico com dados (3x3 com vantagem do defensor).
   - Objetivos secretos e validação de vitória.
   - Código 100% C# POCO (Plain Old C# Objects) sem dependência de Unity, permitindo execução idêntica no servidor.

2. **`unity-visual-agent` (Apresentação Visual & UI/UX)**:
   - Renderização gráfica do mapa e territórios (seleção, hover, feedback visual).
   - Animações 3D de rolagem de dados e movimentação de tropas.
   - Interface de usuário (HUD de fases, painel de ataque, inventário de cartas).
   - Otimização para compilação WebGL / PC / Mobile.

3. **`ai-bot-agent` (Inteligência Artificial para Partidas Solo)**:
   - Tomada de decisão em turnos (heurísticas de reforço de fronteira, cálculo de risco em ataques).
   - Níveis de dificuldade (Fácil, Médio, Difícil).
   - Consome estritamente a API de comandos do Core Engine.

4. **`network-agent` (Infraestrutura Multiplayer Online)**:
   - Servidor autoritativo reutilizando o Core Engine.
   - Protocolo de sincronização de ações de jogo (WebSockets / Photon / Netcode).
   - Lobby, salas de partida, reconexão e chat.
