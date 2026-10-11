# 🎖️ War Strategy Game (3D Digital Board Game)

Projeto de jogo de estratégia em turnos inspirado no clássico WAR brasileiro / Risk, desenvolvido em **Unity 6 URP** com arquitetura desacoplada (Core Engine em C# POCO + Camada de Apresentação 3D + Inteligência Artificial + Suporte a Multiplayer Online).

---

## 👥 Organização do Esquadrão de Agentes

Cada parte do projeto possui um agente especialista dedicado, com escopo estrito e responsabilidades claras:

| Agente | Nome Amigável | Especialidade & Responsabilidade Principal |
| :--- | :--- | :--- |
| **`agente-01-regras-core`** | **[Agente 01] Regras & Core Engine** | Grafo dos 42 territórios clássicos, 6 continentes, máquina de estados de turno (FSM), cartas, objetivos secretos e combate 3x3 de dados. 100% C# POCO sem dependência de Unity. |
| **`agente-02-unity-visual`** | **[Agente 02] Unity 3D & Apresentação** | Camada visual no Unity 6 URP: Mesa 3D, mapa de territórios interativo, miniaturas, dados físicos, câmera orbital, shaders e HUD. |
| **`agente-03-ia-bots`** | **[Agente 03] Inteligência Artificial** | Bots para partidas individuais: heurísticas de reforço em fronteiras, análise de risco para ataque e persecução de objetivos secretos (Fácil, Médio, Difícil). |
| **`agente-04-multiplayer-rede`**| **[Agente 04] Multiplayer & Rede** | Infraestrutura multiplayer online: servidor autoritativo, protocolo de sincronização em turnos, lobby, salas e reconexão. |
| **`qa-inspector-agent`** | **[Agente 05] Inspetor Geral de Qualidade** | **Guardião do Projeto:** audita o repositório após grandes alterações, executa suítes de testes automatizados, valida compilação, integridade arquitetural e coerência entre os módulos. |

---

## 🏗️ Estrutura de Pastas do Projeto

```
War/
├── Core/                   # Regras puras em C# (.NET Standard 2.1)
│   ├── Cards/              # 44 cartas clássicas e escala progressiva de troca
│   ├── Combat/             # Rolagem e resolução de combate 3x3
│   ├── Data/               # Grafo dos 42 territórios e conexões marítimas
│   ├── Game/               # FSM de turnos, GameState e TurnManager
│   ├── Models/             # Territórios e Continentes
│   └── Objectives/         # 14 objetivos secretos clássicos e validador de vitória
│
├── Tests/                  # Suíte de testes automatizados das regras
│
└── Client/                 # Projeto Unity 6 (Universal Render Pipeline - URP)
    ├── Assets/
    │   ├── Scripts/
    │   │   ├── Core/       # Cópia síncrona do Core Engine para a Unity
    │   │   └── Presentation/
    │   │       ├── Board/  # BoardView, TerritoryView (seleção, cores, tropas)
    │   │       ├── Camera/ # TacticalCameraController (órbita, zoom, pan)
    │   │       ├── Game/   # GamePresenter (ponte Core -> Visual)
    │   │       └── Editor/ # BoardSetupTool (menu automatizado de criação 3D)
    │   ├── Materials/      # Materiais PBR (madeira, plástico, dados)
    │   ├── Prefabs/        # Peças de exércitos e dados
    │   └── Scenes/         # Cenas do jogo
    └── Packages/           # Dependências do Unity 6 (URP, TextMeshPro, etc.)
```
