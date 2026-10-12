#nullable enable
using System.Collections;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace War.Presentation.Performance
{
    /// <summary>
    /// Otimizador de performance de alta precisão para GPU e CPU.
    /// Trava com precisão absoluta a taxa de quadros em 60 FPS, elimina renders desbloqueados no Unity Editor,
    /// reduz a sobrecarga de passes de sombra desnecessários e configura limites de qualidade inteligentes.
    /// </summary>
    [DisallowMultipleComponent]
    public class PerformanceOptimizer : MonoBehaviour
    {
        [Header("Configurações de Taxa de Quadros")]
        [Tooltip("Taxa de quadros alvo para o jogo (padrão: 60 FPS para jogo de tabuleiro tático).")]
        [SerializeField] private int targetFrameRate = 60;

        [Tooltip("Contagem de VSync (0 = desligado para permitir targetFrameRate estrito, 1 = sincronizado com monitor).")]
        [SerializeField] private int vSyncCount = 0;

        [Tooltip("Se verdadeiro, aplica controle ativo de ritmo de quadros via WaitForEndOfFrame.")]
        [SerializeField] private bool enableFramePacing = true;

        [Tooltip("Se verdadeiro, aplica as otimizações no Awake deste MonoBehaviour.")]
        [SerializeField] private bool applyOnAwake = true;

        private Coroutine? _pacingCoroutine;

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplySettings(targetFrameRate, vSyncCount);
            }
        }

        private void OnEnable()
        {
            if (enableFramePacing && _pacingCoroutine == null)
            {
                _pacingCoroutine = StartCoroutine(FramePacingRoutine());
            }
        }

        private void OnDisable()
        {
            if (_pacingCoroutine != null)
            {
                StopCoroutine(_pacingCoroutine);
                _pacingCoroutine = null;
            }
        }

        /// <summary>
        /// Hook de inicialização antes de qualquer cena carregar.
        /// Garante que o framerate e a qualidade já iniciem travados antes da GPU processar o primeiro frame.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializePerformanceBudget()
        {
            ApplySettings(60, 0);
        }

        /// <summary>
        /// Aplica todas as configurações de economia de recursos da GPU e CPU.
        /// </summary>
        public static void ApplySettings(int fps = 60, int vsync = 0)
        {
            // 1. Controle estrito de FPS (vSyncCount = 0 permite que Application.targetFrameRate tenha efeito real)
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = fps;

            // 2. Otimização de sombras: mesa de War possui 56x36 unidades, portanto distância de 45 cobre toda a mesa sem sobras
            QualitySettings.shadowDistance = 45f;
            QualitySettings.shadowCascades = 2; // 2 cascades oferece bordas limpas com metade dos passes de 4 cascades
            QualitySettings.shadowResolution = ShadowResolution.Medium;

            // 3. Desativação de sondas de reflexão em tempo real (estático para jogo de tabuleiro)
            QualitySettings.realtimeReflectionProbes = false;

            // 4. Antialiasing otimizado (2x MSAA é leve e eficiente em GPU intermediária/moderna)
            QualitySettings.antiAliasing = 2;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            // 5. Configurações gerais de processo
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Debug.Log($"[PerformanceOptimizer] GPU Otimizada: FPS travado em {fps} (vSync: {vsync}), Sombras (Dist: 45, Cascades: 2, Res: Medium), Reflexões RT: desligadas.");
        }

        /// <summary>
        /// Coroutine de ritmo de quadro de alta precisão.
        /// No Unity Editor, Application.targetFrameRate é frequentemente ignorado pela Game View se o VSync do Editor estiver desmarcado,
        /// fazendo com que GPUs potentes renderizem centenas ou milhares de FPS desnecessários.
        /// Esta rotina mede o tempo do frame no final da renderização e suspende a thread até a janela de 16.6ms.
        /// </summary>
        private IEnumerator FramePacingRoutine()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (true)
            {
                yield return new WaitForEndOfFrame();

                int targetFps = Mathf.Clamp(targetFrameRate, 30, 120);
                long targetTicks = Stopwatch.Frequency / targetFps;
                long elapsedTicks = stopwatch.ElapsedTicks;

                if (elapsedTicks < targetTicks)
                {
                    int sleepMs = (int)((targetTicks - elapsedTicks) * 1000 / Stopwatch.Frequency);
                    if (sleepMs > 1)
                    {
                        Thread.Sleep(sleepMs - 1);
                    }
                    while (stopwatch.ElapsedTicks < targetTicks)
                    {
                        Thread.SpinWait(10);
                    }
                }

                stopwatch.Restart();
            }
        }
    }
}
