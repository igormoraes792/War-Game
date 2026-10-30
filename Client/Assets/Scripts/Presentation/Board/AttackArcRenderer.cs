#nullable enable
using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace War.Presentation.Board
{
    /// <summary>
    /// Renderiza um arco balístico 3D estilizado com animação direcional de avanço,
    /// flecha de mira e badge flutuante de tropas entre o território atacante e o defensor.
    /// Inspirado nas animações táticas de estratégia (estilo vídeo Mago do Algoritmo / War 3D).
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackArcRenderer : MonoBehaviour
    {
        [Header("Configurações do Arco Balístico")]
        [SerializeField] private int curveResolution = 28;
        [SerializeField] private float arcStartWidth = 0.16f;
        [SerializeField] private float arcEndWidth = 0.12f;
        [SerializeField] private float scrollSpeed = 2.4f;
        [SerializeField] private float heightMultiplier = 0.28f;
        [SerializeField] private float minHeight = 1.8f;
        [SerializeField] private float maxHeight = 5.2f;

        [Header("Configurações do Projétil Rastreador")]
        [SerializeField] private float attackTravelDuration = 0.45f;

        // Componentes visuais
        private LineRenderer? _lineRenderer;
        private Material? _arcMaterial;
        private Texture2D? _chevronTexture;
        private Transform? _arrowHead;
        private Transform? _badgeRoot;
        private TMP_Text? _badgeText;
        private MeshRenderer? _badgeBgRenderer;
        private Material? _badgeBgMaterial;
        private MaterialPropertyBlock? _badgePropBlock;

        // Projétil e impacto
        private Transform? _tracerObject;
        private TrailRenderer? _tracerTrail;
        private Transform? _impactRing;
        private Material? _tracerMaterial;
        private Material? _impactMaterial;

        // Estado do arco
        private bool _isActive;
        private float _textureOffset;
        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private Vector3 _controlPosition;
        private Color _currentColor = Color.white;
        private Coroutine? _attackRoutine;

        private Vector3[]? _bezierPoints;

        private void Awake()
        {
            EnsureComponents();
            HideArc();
        }

        private void OnDestroy()
        {
            if (_chevronTexture != null) DestroyImmediate(_chevronTexture);
            if (_arcMaterial != null) DestroyImmediate(_arcMaterial);
            if (_badgeBgMaterial != null) DestroyImmediate(_badgeBgMaterial);
            if (_tracerMaterial != null) DestroyImmediate(_tracerMaterial);
            if (_impactMaterial != null) DestroyImmediate(_impactMaterial);
        }

        private void Update()
        {
            if (!_isActive || _lineRenderer == null || _arcMaterial == null) return;

            // Animação de fluxo contínuo dos chevrons/setas em direção ao alvo
            _textureOffset -= Time.deltaTime * scrollSpeed;
            _arcMaterial.mainTextureOffset = new Vector2(_textureOffset, 0f);
        }

        private void LateUpdate()
        {
            if (!_isActive || _badgeRoot == null || !_badgeRoot.gameObject.activeSelf) return;

            // Mantém o badge de tropas voltado perfeitamente para a câmera tática (Billboard 3D)
            var cam = Camera.main;
            if (cam != null)
            {
                _badgeRoot.rotation = cam.transform.rotation;
            }
        }

        /// <summary>
        /// Exibe o arco balístico em tempo real conectando a origem ao destino,
        /// exibindo a flecha e o badge com a quantidade de dados ou tropas de ataque.
        /// </summary>
        public void ShowArc(Vector3 startWorldPos, Vector3 endWorldPos, Color arcColor, int troopsOrDice, string? customLabel = null)
        {
            EnsureComponents();
            _startPosition = startWorldPos;
            _endPosition = endWorldPos;
            _currentColor = arcColor;

            // Eleva levemente os pontos sobre a superfície dos países
            Vector3 p0 = _startPosition + Vector3.up * 0.28f;
            Vector3 p1 = _endPosition + Vector3.up * 0.28f;

            // Calcula o ponto de controle da curva de Bézier (ápice parabólico)
            float dist = Vector3.Distance(new Vector3(p0.x, 0f, p0.z), new Vector3(p1.x, 0f, p1.z));
            float apexHeight = Mathf.Clamp(dist * heightMultiplier, minHeight, maxHeight);
            Vector3 midPoint = Vector3.Lerp(p0, p1, 0.5f);
            midPoint.y = Mathf.Max(p0.y, p1.y) + apexHeight;
            _controlPosition = midPoint;

            // Preenche os vértices da curva
            if (_bezierPoints == null || _bezierPoints.Length != curveResolution)
            {
                _bezierPoints = new Vector3[curveResolution];
            }

            for (int i = 0; i < curveResolution; i++)
            {
                float t = i / (float)(curveResolution - 1);
                _bezierPoints[i] = EvaluateQuadraticBezier(p0, _controlPosition, p1, t);
            }

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = curveResolution;
                _lineRenderer.SetPositions(_bezierPoints);
                _lineRenderer.startWidth = arcStartWidth;
                _lineRenderer.endWidth = arcEndWidth;
                _lineRenderer.startColor = Color.white;
                _lineRenderer.endColor = Color.white;
                _lineRenderer.enabled = true;
            }

            // Atualiza a cor do material da linha
            if (_arcMaterial != null)
            {
                _arcMaterial.color = arcColor;
                if (_arcMaterial.HasProperty("_BaseColor"))
                {
                    _arcMaterial.SetColor("_BaseColor", arcColor);
                }
            }

            // Posiciona a ponta da flecha no destino, alinhada com a tangente da curva
            if (_arrowHead != null)
            {
                _arrowHead.position = p1;
                Vector3 tangent = (p1 - _bezierPoints[curveResolution - 2]).normalized;
                if (tangent != Vector3.zero)
                {
                    _arrowHead.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                }
                _arrowHead.gameObject.SetActive(true);

                var arrowRend = _arrowHead.GetComponent<MeshRenderer>();
                if (arrowRend != null && _arcMaterial != null)
                {
                    arrowRend.sharedMaterial = _arcMaterial;
                }
            }

            // Posiciona e atualiza o Badge Flutuante no ápice da parábola
            if (_badgeRoot != null)
            {
                Vector3 apexPos = EvaluateQuadraticBezier(p0, _controlPosition, p1, 0.5f);
                _badgeRoot.position = apexPos + Vector3.up * 0.40f;
                _badgeRoot.gameObject.SetActive(true);

                if (_badgeText != null)
                {
                    _badgeText.text = customLabel ?? (troopsOrDice > 0 ? $"⚔ {troopsOrDice} DADOS" : "ATAQUE");
                }

                if (_badgeBgRenderer != null && _badgePropBlock != null)
                {
                    Color rimColor = Color.Lerp(arcColor, Color.white, 0.25f);
                    _badgePropBlock.SetColor("_BaseColor", new Color(0.08f, 0.10f, 0.14f, 0.95f));
                    _badgeBgRenderer.SetPropertyBlock(_badgePropBlock);
                }
            }

            _isActive = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Dispara a animação física de ataque (projétil rastreador viaja pelo arco até o impacto),
        /// acionando o callback <paramref name="onImpact"/> exatamente no momento da colisão no destino.
        /// </summary>
        public void AnimateAttack(Vector3 startWorldPos, Vector3 endWorldPos, Color arcColor, int troopsOrDice, Action? onImpact = null)
        {
            ShowArc(startWorldPos, endWorldPos, arcColor, troopsOrDice);

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
            }
            _attackRoutine = StartCoroutine(AttackTravelRoutine(onImpact));
        }

        private IEnumerator AttackTravelRoutine(Action? onImpact)
        {
            if (_tracerObject == null) yield break;

            Vector3 p0 = _startPosition + Vector3.up * 0.28f;
            Vector3 p1 = _endPosition + Vector3.up * 0.28f;

            _tracerObject.position = p0;
            _tracerObject.gameObject.SetActive(true);

            if (_tracerTrail != null)
            {
                _tracerTrail.Clear();
                _tracerTrail.emitting = true;
                _tracerTrail.startColor = _currentColor;
                _tracerTrail.endColor = new Color(_currentColor.r, _currentColor.g, _currentColor.b, 0f);
            }

            if (_tracerMaterial != null)
            {
                Color emissive = _currentColor * 1.5f;
                emissive.a = 1f;
                _tracerMaterial.color = emissive;
                if (_tracerMaterial.HasProperty("_BaseColor")) _tracerMaterial.SetColor("_BaseColor", emissive);
            }

            float elapsed = 0f;
            while (elapsed < attackTravelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / attackTravelDuration);
                _tracerObject.position = EvaluateQuadraticBezier(p0, _controlPosition, p1, t);
                yield return null;
            }

            _tracerObject.position = p1;
            if (_tracerTrail != null)
            {
                _tracerTrail.emitting = false;
            }
            _tracerObject.gameObject.SetActive(false);

            // Dispara efeito de impacto no alvo
            StartCoroutine(ImpactBurstRoutine(p1));

            // Callback no exato instante do impacto
            onImpact?.Invoke();

            // Mantém o arco brevemente e depois oculta
            yield return new WaitForSeconds(0.35f);
            HideArc();
            _attackRoutine = null;
        }

        private IEnumerator ImpactBurstRoutine(Vector3 impactPos)
        {
            if (_impactRing == null) yield break;

            _impactRing.position = impactPos + Vector3.up * 0.05f;
            _impactRing.localScale = Vector3.zero;
            _impactRing.gameObject.SetActive(true);

            if (_impactMaterial != null)
            {
                _impactMaterial.color = _currentColor;
                if (_impactMaterial.HasProperty("_BaseColor")) _impactMaterial.SetColor("_BaseColor", _currentColor);
            }

            float duration = 0.30f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Animação de anel expansivo que desaparece
                float scale = Mathf.Lerp(0.2f, 1.6f, Mathf.Sqrt(progress));
                _impactRing.localScale = new Vector3(scale, 0.04f, scale);

                if (_impactMaterial != null)
                {
                    Color fadeCol = _currentColor;
                    fadeCol.a = Mathf.Lerp(0.85f, 0f, progress);
                    _impactMaterial.color = fadeCol;
                    if (_impactMaterial.HasProperty("_BaseColor")) _impactMaterial.SetColor("_BaseColor", fadeCol);
                }

                yield return null;
            }

            _impactRing.gameObject.SetActive(false);
        }

        public bool IsAnimatingAttack => _attackRoutine != null;

        /// <summary>
        /// Oculta o arco balístico e desativa a renderização.
        /// </summary>
        public void HideArc(bool force = false)
        {
            if (!force && _attackRoutine != null)
            {
                return;
            }

            _isActive = false;
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_lineRenderer != null) _lineRenderer.enabled = false;
            if (_arrowHead != null) _arrowHead.gameObject.SetActive(false);
            if (_badgeRoot != null) _badgeRoot.gameObject.SetActive(false);
            if (_tracerObject != null) _tracerObject.gameObject.SetActive(false);
            if (_impactRing != null) _impactRing.gameObject.SetActive(false);
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private void EnsureComponents()
        {
            var shader = FindSafeShader();

            // 1. LineRenderer do Arco
            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
                if (_lineRenderer == null)
                {
                    _lineRenderer = gameObject.AddComponent<LineRenderer>();
                }

                _lineRenderer.useWorldSpace = true;
                _lineRenderer.alignment = LineAlignment.View;
                _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lineRenderer.receiveShadows = false;
                _lineRenderer.textureMode = LineTextureMode.Tile;
            }

            // 2. Material com Textura de Setas Direcionais (Chevrons)
            if (_chevronTexture == null)
            {
                _chevronTexture = CreateChevronTexture();
            }

            if (_arcMaterial == null)
            {
                _arcMaterial = new Material(shader)
                {
                    name = "War_AttackArc_Chevron_Mat",
                    mainTexture = _chevronTexture
                };
                _arcMaterial.enableInstancing = true;
                _arcMaterial.color = Color.white;
                if (_arcMaterial.HasProperty("_BaseColor")) _arcMaterial.SetColor("_BaseColor", Color.white);
            }

            if (_lineRenderer != null)
            {
                _lineRenderer.sharedMaterial = _arcMaterial;
            }

            // 3. Ponta da Flecha (Arrowhead)
            if (_arrowHead == null)
            {
                var arrowGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arrowGo.name = "Arc_ArrowHead";
                arrowGo.transform.SetParent(transform, false);
                arrowGo.transform.localScale = new Vector3(0.38f, 0.45f, 0.38f);

                var col = arrowGo.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);

                _arrowHead = arrowGo.transform;
                _arrowHead.gameObject.SetActive(false);
            }

            // 4. Badge Flutuante no Ápice
            if (_badgeRoot == null)
            {
                var badgeGo = new GameObject("Arc_TroopBadge");
                badgeGo.transform.SetParent(transform, false);

                // Fundo estilizado tipo cartela/pill
                var bgObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bgObj.name = "Badge_Background";
                bgObj.transform.SetParent(badgeGo.transform, false);
                bgObj.transform.localScale = new Vector3(1.70f, 0.46f, 0.04f);
                var bgCol = bgObj.GetComponent<Collider>();
                if (bgCol != null) DestroyImmediate(bgCol);

                _badgeBgRenderer = bgObj.GetComponent<MeshRenderer>();
                if (_badgeBgMaterial == null)
                {
                    _badgeBgMaterial = new Material(shader)
                    {
                        name = "War_ArcBadge_BgMat"
                    };
                }
                if (_badgeBgRenderer != null)
                {
                    _badgeBgRenderer.sharedMaterial = _badgeBgMaterial;
                }
                _badgePropBlock = new MaterialPropertyBlock();

                // Texto TextMeshPro SDF
                var textObj = new GameObject("Badge_Text");
                textObj.transform.SetParent(badgeGo.transform, false);
                textObj.transform.localPosition = new Vector3(0f, 0f, -0.04f);

                _badgeText = textObj.AddComponent<TextMeshPro>();
                _badgeText.fontSize = 2.4f;
                _badgeText.fontStyle = FontStyles.Bold;
                _badgeText.alignment = TextAlignmentOptions.Center;
                _badgeText.color = Color.white;
                _badgeText.outlineWidth = 0.15f;
                _badgeText.outlineColor = Color.black;
                _badgeText.textWrappingMode = TextWrappingModes.NoWrap;

                var sdfFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (sdfFont != null) _badgeText.font = sdfFont;

                var rt = textObj.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(2.0f, 0.5f);

                _badgeRoot = badgeGo.transform;
                _badgeRoot.gameObject.SetActive(false);
            }

            // 5. Projétil Rastreador (Tracer Bullet)
            if (_tracerObject == null)
            {
                var tracerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tracerGo.name = "Arc_TracerProjectile";
                tracerGo.transform.SetParent(transform, false);
                tracerGo.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);

                var tracerCol = tracerGo.GetComponent<Collider>();
                if (tracerCol != null) DestroyImmediate(tracerCol);

                if (_tracerMaterial == null)
                {
                    _tracerMaterial = new Material(shader) { name = "War_Tracer_Mat" };
                }
                var tracerRend = tracerGo.GetComponent<MeshRenderer>();
                if (tracerRend != null) tracerRend.sharedMaterial = _tracerMaterial;

                _tracerTrail = tracerGo.AddComponent<TrailRenderer>();
                _tracerTrail.time = 0.22f;
                _tracerTrail.startWidth = 0.18f;
                _tracerTrail.endWidth = 0.02f;
                _tracerTrail.material = _tracerMaterial;

                _tracerObject = tracerGo.transform;
                _tracerObject.gameObject.SetActive(false);
            }

            // 6. Anel de Impacto (Shockwave Ring)
            if (_impactRing == null)
            {
                var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "Arc_ImpactShockwave";
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localScale = Vector3.zero;

                var rCol = ringGo.GetComponent<Collider>();
                if (rCol != null) DestroyImmediate(rCol);

                if (_impactMaterial == null)
                {
                    _impactMaterial = new Material(shader) { name = "War_ImpactRing_Mat" };
                }
                var rRend = ringGo.GetComponent<MeshRenderer>();
                if (rRend != null) rRend.sharedMaterial = _impactMaterial;

                _impactRing = ringGo.transform;
                _impactRing.gameObject.SetActive(false);
            }
        }

        private static Shader FindSafeShader()
        {
            var isUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (isUrp)
            {
                var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit != null) return urpUnlit;
            }

            return Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        }

        private static Texture2D CreateChevronTexture()
        {
            int w = 64;
            int h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "War_Chevron_Attack_Pattern",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[w * h];
            float centerY = (h - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            {
                float distY = Mathf.Abs(y - centerY);

                for (int x = 0; x < w; x++)
                {
                    int lx = x % 16;
                    float chevronPeakX = 13.0f - distY * 0.95f;
                    bool isArrow = (lx >= chevronPeakX - 3.8f && lx <= chevronPeakX);
                    float edgeAlpha = Mathf.Clamp01((centerY - distY + 1.2f) / 1.5f);

                    if (isArrow)
                    {
                        pixels[y * w + x] = new Color(1f, 1f, 1f, 0.95f * edgeAlpha);
                    }
                    else
                    {
                        pixels[y * w + x] = new Color(1f, 1f, 1f, 0.18f * edgeAlpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
