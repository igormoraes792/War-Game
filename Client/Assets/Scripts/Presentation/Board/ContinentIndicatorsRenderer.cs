#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza marcadores sutis e elegantes no estilo de marca d'água cartográfica
    /// na superfície do tabuleiro (sem caixas flutuantes, sem linhas cruzando países e sem shaders quebrados).
    /// </summary>
    public class ContinentIndicatorsRenderer : MonoBehaviour
    {
        [System.Serializable]
        public struct ContinentCallout
        {
            public string Name;
            public int ArmyBonus;
            public Color ThemeColor;
            public Vector3 WatermarkPosition;

            public ContinentCallout(string name, int armyBonus, Color themeColor, Vector3 pos)
            {
                Name = name;
                ArmyBonus = armyBonus;
                ThemeColor = themeColor;
                WatermarkPosition = pos;
            }
        }

        [Header("Configurações Cartográficas")]
        [SerializeField] private bool showWatermarks = true;

        private readonly List<GameObject> _spawnedWatermarks = new();

        // Posições nos oceanos abertos, livres de territórios ou rotas marinhas
        public static readonly ContinentCallout[] ClassicContinents = new ContinentCallout[]
        {
            new ContinentCallout("AMÉRICA DO NORTE", 5, new Color(0.92f, 0.45f, 0.14f), new Vector3(-24.5f, 0.05f, 13.5f)),
            new ContinentCallout("AMÉRICA DO SUL", 2, new Color(0.35f, 0.78f, 0.25f), new Vector3(-23.0f, 0.05f, -11.5f)),
            new ContinentCallout("EUROPA", 5, new Color(0.18f, 0.75f, 0.92f), new Vector3(1.5f, 0.05f, 14.0f)),
            new ContinentCallout("ÁFRICA", 3, new Color(0.85f, 0.22f, 0.55f), new Vector3(2.5f, 0.05f, -13.5f)),
            new ContinentCallout("ÁSIA", 7, new Color(0.92f, 0.72f, 0.10f), new Vector3(23.5f, 0.05f, 13.5f)),
            new ContinentCallout("OCEANIA", 2, new Color(0.88f, 0.25f, 0.25f), new Vector3(23.0f, 0.05f, -13.5f))
        };

        private void Start()
        {
            if (_spawnedWatermarks.Count == 0)
            {
                RebuildIndicators();
            }
        }

        /// <summary>
        /// Reconstrói as marcas cartográficas dos continentes planas sobre o oceano.
        /// Não cria nenhuma geometria 3D bloqueante nem traça linhas sobre os países.
        /// </summary>
        public void RebuildIndicators()
        {
            foreach (var go in _spawnedWatermarks)
            {
                if (go != null) DestroyImmediate(go);
            }
            _spawnedWatermarks.Clear();

            if (!showWatermarks) return;

            foreach (var continent in ClassicContinents)
            {
                var labelGo = new GameObject($"Watermark_{continent.Name}");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.position = continent.WatermarkPosition;
                labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Plano contra a mesa/oceano

                var textMesh = labelGo.AddComponent<TextMesh>();
                textMesh.text = $"{continent.Name} • +{continent.ArmyBonus}";
                textMesh.fontSize = 28;
                textMesh.characterSize = 0.08f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                
                // Cor sutil com opacidade suave integrada à cartografia náutica
                var col = Color.Lerp(continent.ThemeColor, Color.white, 0.35f);
                col.a = 0.65f;
                textMesh.color = col;
                textMesh.fontStyle = FontStyle.Bold;

                _spawnedWatermarks.Add(labelGo);
            }
        }
    }
}
