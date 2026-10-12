#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza os marcadores e badges táticos dos 6 continentes com seus bônus oficiais
    /// e linhas de chamada (leader lines) apontando para o centro de cada região,
    /// conforme a arte de referência visual de mesa militar.
    /// </summary>
    public class ContinentIndicatorsRenderer : MonoBehaviour
    {
        [System.Serializable]
        public struct ContinentCallout
        {
            public string Name;
            public int ArmyBonus;
            public Color ThemeColor;
            public Vector3 IndicatorPosition;
            public Vector3 ContinentTargetPosition;

            public ContinentCallout(string name, int armyBonus, Color themeColor, Vector3 indicatorPos, Vector3 targetPos)
            {
                Name = name;
                ArmyBonus = armyBonus;
                ThemeColor = themeColor;
                IndicatorPosition = indicatorPos;
                ContinentTargetPosition = targetPos;
            }
        }

        [Header("Configurações Visuais")]
        [SerializeField] private float badgeScale = 1.0f;
        [SerializeField] private bool showLeaderLines = true;

        private readonly List<GameObject> _spawnedIndicators = new();
        private readonly List<LineRenderer> _leaderLines = new();

        public static readonly ContinentCallout[] ClassicContinents = new ContinentCallout[]
        {
            new ContinentCallout("AMÉRICA DO NORTE", 5, new Color(0.92f, 0.45f, 0.14f), new Vector3(-24.5f, 0.45f, 6.0f), new Vector3(-18.5f, 0.25f, 8.5f)),
            new ContinentCallout("AMÉRICA DO SUL", 2, new Color(0.32f, 0.76f, 0.22f), new Vector3(-18.5f, 0.45f, -5.0f), new Vector3(-11.5f, 0.25f, -6.5f)),
            new ContinentCallout("EUROPA", 5, new Color(0.14f, 0.74f, 0.90f), new Vector3(1.5f, 0.45f, 13.5f), new Vector3(1.2f, 0.25f, 8.5f)),
            new ContinentCallout("ÁFRICA", 3, new Color(0.85f, 0.16f, 0.52f), new Vector3(1.0f, 0.45f, -12.5f), new Vector3(2.5f, 0.25f, -4.5f)),
            new ContinentCallout("ÁSIA", 7, new Color(0.92f, 0.72f, 0.10f), new Vector3(22.5f, 0.45f, 11.5f), new Vector3(16.5f, 0.25f, 6.5f)),
            new ContinentCallout("OCEANIA", 2, new Color(0.85f, 0.20f, 0.20f), new Vector3(17.5f, 0.45f, -12.5f), new Vector3(20.5f, 0.25f, -7.5f))
        };

        private void Start()
        {
            if (_spawnedIndicators.Count == 0)
            {
                RebuildIndicators();
            }
        }

        /// <summary>
        /// Reconstrói ou gera os 6 marcadores táticos na cena.
        /// </summary>
        public void RebuildIndicators()
        {
            // Limpa instâncias anteriores
            foreach (var go in _spawnedIndicators)
            {
                if (go != null) DestroyImmediate(go);
            }
            _spawnedIndicators.Clear();
            _leaderLines.Clear();

            var litShader = Shader.Find("Universal Render Pipeline/Lit") 
                            ?? Shader.Find("Standard") 
                            ?? Shader.Find("Diffuse");

            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Unlit/Color")
                              ?? Shader.Find("Diffuse");

            foreach (var continent in ClassicContinents)
            {
                var indicatorGo = new GameObject($"ContinentBadge_{continent.Name}");
                indicatorGo.transform.SetParent(transform, false);
                indicatorGo.transform.position = continent.IndicatorPosition;
                _spawnedIndicators.Add(indicatorGo);

                // 1. Placa/Base de fundo escuro acetinado com moldura fina
                var backPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backPlate.name = "BackPlate";
                backPlate.transform.SetParent(indicatorGo.transform, false);
                backPlate.transform.localPosition = Vector3.zero;
                backPlate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // deitado na mesa
                backPlate.transform.localScale = new Vector3(6.2f * badgeScale, 1.8f * badgeScale, 0.04f);

                var plateCollider = backPlate.GetComponent<Collider>();
                if (plateCollider != null) DestroyImmediate(plateCollider);

                var plateRenderer = backPlate.GetComponent<MeshRenderer>();
                if (plateRenderer != null)
                {
                    var plateMat = new Material(litShader)
                    {
                        color = new Color(0.04f, 0.08f, 0.14f, 0.95f),
                        enableInstancing = true
                    };
                    plateRenderer.material = plateMat;
                }

                // 2. Ícone Circular Tático / Badge lateral (Disco com a cor do continente)
                var badgeRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                badgeRing.name = "ColorRing";
                badgeRing.transform.SetParent(indicatorGo.transform, false);
                badgeRing.transform.localPosition = new Vector3(-2.3f * badgeScale, 0.03f, 0f);
                badgeRing.transform.localScale = new Vector3(1.1f * badgeScale, 0.02f, 1.1f * badgeScale);

                var ringCollider = badgeRing.GetComponent<Collider>();
                if (ringCollider != null) DestroyImmediate(ringCollider);

                var ringRenderer = badgeRing.GetComponent<MeshRenderer>();
                if (ringRenderer != null)
                {
                    var ringMat = new Material(litShader)
                    {
                        color = continent.ThemeColor,
                        enableInstancing = true
                    };
                    if (ringMat.HasProperty("_EmissionColor"))
                    {
                        ringMat.EnableKeyword("_EMISSION");
                        ringMat.SetColor("_EmissionColor", continent.ThemeColor * 0.4f);
                    }
                    ringRenderer.material = ringMat;
                }

                // 3. Texto do Nome do Continente e Bônus
                var labelGo = new GameObject("LabelText");
                labelGo.transform.SetParent(indicatorGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0.3f * badgeScale, 0.04f, 0.25f * badgeScale);
                labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var textMesh = labelGo.AddComponent<TextMesh>();
                textMesh.text = $"{continent.Name}\n+{continent.ArmyBonus} EXÉRCITOS";
                textMesh.fontSize = 24;
                textMesh.characterSize = 0.085f * badgeScale;
                textMesh.anchor = TextAnchor.MiddleLeft;
                textMesh.alignment = TextAlignment.Left;
                textMesh.color = Color.white;
                textMesh.fontStyle = FontStyle.Bold;

                // 4. Linha de Chamada (Leader Line) tática conectando o indicador ao centro do continente
                if (showLeaderLines)
                {
                    var lineGo = new GameObject("LeaderLine");
                    lineGo.transform.SetParent(indicatorGo.transform, false);

                    var line = lineGo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.startWidth = 0.06f;
                    line.endWidth = 0.03f;
                    line.positionCount = 2;
                    line.SetPosition(0, continent.IndicatorPosition + new Vector3(0, 0.02f, 0));
                    line.SetPosition(1, continent.ContinentTargetPosition + new Vector3(0, 0.02f, 0));

                    var lineMat = new Material(unlitShader)
                    {
                        color = new Color(continent.ThemeColor.r, continent.ThemeColor.g, continent.ThemeColor.b, 0.5f),
                        enableInstancing = true
                    };
                    line.material = lineMat;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;

                    _leaderLines.Add(line);
                }
            }
        }
    }
}
