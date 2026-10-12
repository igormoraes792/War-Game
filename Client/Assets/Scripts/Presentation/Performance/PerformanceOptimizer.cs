#nullable enable
using UnityEngine;

namespace War.Presentation.Performance
{
    /// <summary>
    /// Componente e inicializador automático responsável por limitar o consumo de GPU e CPU.
    /// Trava a taxa de atualização em 60 FPS com VSync ativo para evitar que a GPU rode solta
    /// renderizando centenas de frames desnecessários no Unity Editor ou em runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class PerformanceOptimizer : MonoBehaviour
    {
        [Header("Configurações de Taxa de Quadros")]
        [Tooltip("Taxa de quadros alvo para o jogo.")]
        [SerializeField] private int targetFrameRate = 60;

        [Tooltip("Contagem de VSync (1 = 60Hz sincronizado com o monitor, 0 = desligado).")]
        [SerializeField] private int vSyncCount = 1;

        [Tooltip("Se verdadeiro, aplica as otimizações no Awake deste MonoBehaviour.")]
        [SerializeField] private bool applyOnAwake = true;

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplySettings(targetFrameRate, vSyncCount);
            }
        }

        /// <summary>
        /// Hook de inicialização de alta prioridade executado antes de qualquer cena carregar.
        /// Garante que mesmo sem nenhum GameObject na cena, o Unity já limite o framerate imediatamente.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializePerformanceBudget()
        {
            ApplySettings(60, 1);
        }

        /// <summary>
        /// Aplica as diretrizes de performance e economia de recursos da GPU/CPU.
        /// </summary>
        public static void ApplySettings(int fps = 60, int vsync = 1)
        {
            // Trava o VSync e o framerate alvo
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = fps;

            // Mantém a execução suave mesmo sem foco direto
            Application.runInBackground = true;

            // Evita suspensão de tela em dispositivos móveis/híbridos
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Debug.Log($"[PerformanceOptimizer] Taxa de quadros travada em {fps} FPS (VSync: {vsync}). Uso de GPU otimizado e protegido.");
        }
    }
}
