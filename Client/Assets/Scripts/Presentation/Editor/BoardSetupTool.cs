#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using War.Core.Data;
using War.Presentation.Board;
using War.Presentation.CameraControl;
using War.Presentation.Game;
using War.Presentation.Performance;
using War.Presentation.UI;

namespace War.Presentation.Editor
{
    /// <summary>
    /// Ferramenta de Editor para gerar toda a hierarquia 3D inicial da mesa tática de WAR.
    /// Recompilação automática via [InitializeOnLoad] e hook de Play Mode para que o usuário NUNCA precise clicar em menu.
    /// </summary>
    [InitializeOnLoad]
    public static class BoardSetupTool
    {
        static BoardSetupTool()
        {
            // Registra hook automático: sempre que você der Play ou o Unity compilar, ele reconstrói a cena sozinho se necessário
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Auto-regenera tudo de forma limpa antes de iniciar a simulação
                GenerateTacticalBoardSilent();
            }
        }

        [MenuItem("War Game/Gerar Tabuleiro 3D Básico na Cena")]
        public static void GenerateTacticalBoard()
        {
            GenerateTacticalBoardSilent();
        }

        public static void GenerateTacticalBoardSilent()
        {
            const string rootName = "War_Mesa_Tatica";

            // Limpeza automática completa de qualquer versão anterior na cena aberta
            foreach (var oldName in new[] { "War_Mesa_Tatica", "War_Tabletop_Root", "War_Canvas", "EventSystem" })
            {
                var oldObj = GameObject.Find(oldName);
                if (oldObj != null)
                {
                    Undo.DestroyObjectImmediate(oldObj);
                }
            }

            var rootObj = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(rootObj, "Gerar Tabuleiro 3D Básico");

            // Otimizador de desempenho e limitação de FPS (60 FPS + VSync)
            rootObj.AddComponent<PerformanceOptimizer>();

            // 1. Criação da Mesa de Madeira com Moldura Nobre Biselada
            var tableObj = CreateWoodTable(rootObj.transform);

            // 2. Criação da Superfície Texturizada Náutica/Tática do Mapa Mundi
            var mapSurfaceObj = CreateBoardGraphicSurface(rootObj.transform);

            // 3. Criação do Tabuleiro e dos 42 territórios
            var boardObj = new GameObject("Tabuleiro");
            boardObj.transform.SetParent(rootObj.transform, false);
            var boardView = boardObj.AddComponent<BoardView>();

            var territoryViews = CreateTerritories(boardObj.transform);

            // 4. Criação das Linhas de Conexão Táticas e Rotas Marítimas
            var linesObj = new GameObject("Linhas_Conexao");
            linesObj.transform.SetParent(boardObj.transform, false);
            var linesRenderer = linesObj.AddComponent<ConnectionLinesRenderer>();

            var coords = GetTerritoryWorldCoordinates();
            var positions3D = new Dictionary<string, Vector3>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in coords)
            {
                positions3D[kvp.Key] = new Vector3(kvp.Value.x, 0.35f, kvp.Value.y);
            }
            linesRenderer.BuildConnectionsFromMapData(positions3D);

            // Vincula o ConnectionLinesRenderer ao BoardView
            var boardViewSo = new SerializedObject(boardView);
            var linesProp = boardViewSo.FindProperty("connectionLinesRenderer");
            if (linesProp != null)
            {
                linesProp.objectReferenceValue = linesRenderer;
                boardViewSo.ApplyModifiedProperties();
            }

            // 4.1 Criação dos Marcadores e Badges dos Continentes com Bônus Táticos e Linhas Indicadoras
            var continentsObj = new GameObject("Indicadores_Continentes");
            continentsObj.transform.SetParent(boardObj.transform, false);
            var continentRenderer = continentsObj.AddComponent<ContinentIndicatorsRenderer>();
            var continentSo = new SerializedObject(continentRenderer);
            var bvProp = continentSo.FindProperty("boardView");
            if (bvProp != null)
            {
                bvProp.objectReferenceValue = boardView;
                continentSo.ApplyModifiedProperties();
            }
            continentRenderer.RebuildIndicators();

            // 5. Criação da Câmera Tática
            var cameraObj = SetupTacticalCamera(rootObj.transform);

            // 6. Criação da Iluminação Direcional com Sombras
            SetupDirectionalLight(rootObj.transform);

            // 7. Criação do GameController (Presenter + View)
            var controllerObj = new GameObject("GameController");
            controllerObj.transform.SetParent(rootObj.transform, false);
            var presenter = controllerObj.AddComponent<GamePresenter>();

            // Vincula referências via SerializedObject para persistir no prefab/cena
            var presenterSo = new SerializedObject(presenter);
            var boardViewProp = presenterSo.FindProperty("boardView");
            if (boardViewProp != null)
            {
                boardViewProp.objectReferenceValue = boardView;
                presenterSo.ApplyModifiedProperties();
            }

            // Atualiza o BoardView com todos os territórios criados
            boardView.ScanAndIndexTerritories();

            // 8. Criação do War_Canvas (GameHUD, CombatPanelUI, ObjectiveModalUI)
            SetupGameCanvas(presenter);

            // Seleciona o root na Hierarchy e foca a SceneView
            Selection.activeGameObject = rootObj;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[War Setup] Tabuleiro 3D tático completo gerado com sucesso: Superfície Cartográfica, 42 Territórios, Conexões e Rotas Marítimas, Câmera e HUD!");
        }

        [MenuItem("War Game/Gerar Canvas de HUD e Batalha na Cena")]
        public static void GenerateCanvasOnly()
        {
            var presenter = Object.FindAnyObjectByType<GamePresenter>();
            var canvasObj = SetupGameCanvas(presenter);
            Selection.activeGameObject = canvasObj;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[War Setup] War_Canvas gerado com sucesso com GameHUD, CombatPanelUI e ObjectiveModalUI!");
        }

        private static GameObject CreateWoodTable(Transform parent)
        {
            var tableRoot = new GameObject("Quadro_Canvas_Arte");
            tableRoot.transform.SetParent(parent, false);

            var canvasBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canvasBlock.name = "Canvas_Chassi_Bloco";
            canvasBlock.transform.SetParent(tableRoot.transform, false);
            canvasBlock.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            canvasBlock.transform.localScale = new Vector3(60.0f, 1.2f, 38.0f);

            var litShader = GetSafeLitShader();
            // Laterais da tela em azul celeste de galeria (#8EBDE0 / #7CAFD7) com acabamento fosco suave
            var canvasSideMat = new Material(litShader)
            {
                name = "Canvas_Borda_Celeste_Mat",
                color = new Color(0.29f, 0.54f, 0.74f, 1f),
                enableInstancing = true
            };
            if (canvasSideMat.HasProperty("_BaseColor")) canvasSideMat.SetColor("_BaseColor", new Color(0.29f, 0.54f, 0.74f, 1f));
            if (canvasSideMat.HasProperty("_Smoothness"))
            {
                canvasSideMat.SetFloat("_Smoothness", 0.08f);
            }

            var renderer = canvasBlock.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = canvasSideMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return tableRoot;
        }

        private static void CreateRimPart(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = name;
            rim.transform.SetParent(parent, false);
            rim.transform.localPosition = localPos;
            rim.transform.localScale = localScale;

            var r = rim.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        private static GameObject CreateBoardGraphicSurface(Transform parent)
        {
            // Usamos um Quad rotacionado em 90 graus (olhando para cima) para garantir coordenadas UV perfeitas [0..1] na textura do mapa
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Superficie_Mapa_Tatico";
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            surface.transform.localScale = new Vector3(60.0f, 38.0f, 1f);

            var surfRenderer = surface.GetComponent<MeshRenderer>();
            if (surfRenderer != null)
            {
                surfRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                surfRenderer.receiveShadows = true;
            }

            // Remove o colisor da superfície para que raycasts e cliques atinjam os territórios sem obstrução
            var col = surface.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            var graphic = surface.AddComponent<MapBoardGraphic>();
            graphic.GenerateAndApplyTexture();

            return surface;
        }

        private static List<TerritoryView> CreateTerritories(Transform boardTransform)
        {
            var views = new List<TerritoryView>();
            var positions = GetTerritoryWorldCoordinates();
            var defs = ClassicMapData.GetAllDefinitions();

            var defaultShader = GetSafeLitShader();
            var defaultTerritoryMat = new Material(defaultShader)
            {
                name = "Territorio_Base_Mat",
                color = new Color(0.72f, 0.72f, 0.76f, 1f),
                enableInstancing = true
            };
            defaultTerritoryMat.EnableKeyword("_EMISSION");
            if (defaultTerritoryMat.HasProperty("_Smoothness"))
            {
                defaultTerritoryMat.SetFloat("_Smoothness", 0.12f);
            }

            // Material do pedestal de latão/bronze do pin tático
            var brassMat = new Material(defaultShader)
            {
                name = "Pin_Pedestal_Brass_Mat",
                color = new Color(0.24f, 0.20f, 0.14f, 1f),
                enableInstancing = true
            };
            if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.65f);
            if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.75f);

            // Material do badge do pin tático (disco escuro de alto contraste com aro)
            var badgeMat = new Material(defaultShader)
            {
                name = "Pin_Badge_Mat",
                color = new Color(0.10f, 0.13f, 0.18f, 1f),
                enableInstancing = true
            };
            badgeMat.EnableKeyword("_EMISSION");
            if (badgeMat.HasProperty("_Smoothness")) badgeMat.SetFloat("_Smoothness", 0.5f);

            // Material da plaqueta de identificação do território
            var plaqueMat = new Material(defaultShader)
            {
                name = "Territory_Plaque_Mat",
                color = new Color(0.06f, 0.08f, 0.12f, 0.95f),
                enableInstancing = true
            };
            if (plaqueMat.HasProperty("_Smoothness")) plaqueMat.SetFloat("_Smoothness", 0.45f);

            foreach (var def in defs)
            {
                positions.TryGetValue(def.Id, out var coord);

                // 1. Cria o GameObject do território com a malha 3D recortada
                var terrObj = new GameObject($"Territory_{def.Name} ({def.Id})");
                terrObj.transform.SetParent(boardTransform, false);
                terrObj.transform.localPosition = new Vector3(coord.x, 0.05f, coord.y);

                var meshFilter = terrObj.AddComponent<MeshFilter>();
                var territoryMesh = TerritoryMeshBuilder.CreateTerritoryMesh(def.Id);
                meshFilter.sharedMesh = territoryMesh;

                var meshRenderer = terrObj.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = defaultTerritoryMat;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;

                // MeshCollider preciso seguindo o contorno exato do polígono recortado
                var meshCollider = terrObj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = territoryMesh;
                meshCollider.convex = false;

                var terrView = terrObj.AddComponent<TerritoryView>();
                terrView.TerritoryId = def.Id;

                // 2. Calcula o centroide do polígono para centralizar o pin tático
                var poly = TerritoryMeshBuilder.GetTerritoryPolygon(def.Id);
                var centroid = TerritoryMeshBuilder.GetPolygonCentroid(poly);

                // 3. Estandarte / Pin Tático Elegante de Alto Contraste (Estilo Token Militar War)
                // Posicionado levemente elevado em Y+ para acomodar harmoniosamente o nome do país logo abaixo
                var pinRoot = new GameObject("Pin_TacticalRoot");
                pinRoot.transform.SetParent(terrObj.transform, false);
                pinRoot.transform.localPosition = new Vector3(centroid.x, TerritoryMeshBuilder.DefaultExtrusionHeight, centroid.y + 0.35f);

                // 3.1 Aro exterior (Rim) com a cor do jogador - Garante separação perfeita contra qualquer país
                var rimObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rimObj.name = "Pin_Rim";
                rimObj.transform.SetParent(pinRoot.transform, false);
                rimObj.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                rimObj.transform.localScale = new Vector3(1.35f, 0.04f, 1.35f);
                var rimRend = rimObj.GetComponent<MeshRenderer>();
                if (rimRend != null)
                {
                    rimRend.sharedMaterial = badgeMat;
                    rimRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var rimCol = rimObj.GetComponent<Collider>();
                if (rimCol != null) Object.DestroyImmediate(rimCol);

                // 3.2 Disco central interno (Preto/Grafite profundo para contraste máximo do número)
                var badge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                badge.name = "Pin_Badge";
                badge.transform.SetParent(pinRoot.transform, false);
                badge.transform.localPosition = new Vector3(0f, 0.10f, 0f);
                badge.transform.localScale = new Vector3(1.12f, 0.05f, 1.12f);
                var badgeRend = badge.GetComponent<MeshRenderer>();
                if (badgeRend != null)
                {
                    badgeRend.sharedMaterial = badgeMat;
                    badgeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var badgeCol = badge.GetComponent<Collider>();
                if (badgeCol != null) Object.DestroyImmediate(badgeCol);

                // 3.3 Ponto de spawn de tropas
                var spawnPoint = new GameObject("ArmySpawnPoint");
                spawnPoint.transform.SetParent(pinRoot.transform, false);
                spawnPoint.transform.localPosition = new Vector3(0f, 0.22f, 0f);

                // Carrega fonte SDF padrão TextMeshPro de alta definição
                var sdfFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

                // 3.4 Texto do contador de tropas no pin (TextMeshPro SDF de alta nitidez vetorial)
                var textObj = new GameObject("Text_ArmyCount");
                textObj.transform.SetParent(pinRoot.transform, false);
                textObj.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                textObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var armyCountTmp = textObj.AddComponent<TextMeshPro>();
                armyCountTmp.text = "1";
                armyCountTmp.fontSize = 8.5f;
                armyCountTmp.fontStyle = FontStyles.Bold;
                armyCountTmp.alignment = TextAlignmentOptions.Center;
                armyCountTmp.color = Color.white;
                armyCountTmp.textWrappingMode = TextWrappingModes.NoWrap;
                if (sdfFont != null) armyCountTmp.font = sdfFont;
                var armyCountRt = textObj.GetComponent<RectTransform>();
                if (armyCountRt != null) armyCountRt.sizeDelta = new Vector2(2.5f, 2.5f);

                // 3.5 Nome do País impresso com alta definição vetorial (TextMeshPro SDF)
                // Posicionado com folga limpa abaixo do pin (gap de segurança) para nunca sobrepor o token militar
                var nameTagRoot = new GameObject("NameTag_Root");
                nameTagRoot.transform.SetParent(terrObj.transform, false);
                nameTagRoot.transform.localPosition = new Vector3(centroid.x, TerritoryMeshBuilder.DefaultExtrusionHeight + 0.02f, centroid.y - 0.95f);
                nameTagRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var nameObj = new GameObject("Text_Name");
                nameObj.transform.SetParent(nameTagRoot.transform, false);
                nameObj.transform.localPosition = Vector3.zero;
                nameObj.transform.localRotation = Quaternion.identity;

                var nameTmp = nameObj.AddComponent<TextMeshPro>();
                nameTmp.text = def.GeopoliticalName.ToUpperInvariant();
                nameTmp.fontSize = 4.2f;
                nameTmp.fontStyle = FontStyles.Bold;
                nameTmp.alignment = TextAlignmentOptions.Center;
                nameTmp.color = Color.white;
                nameTmp.outlineWidth = 0.30f;
                nameTmp.outlineColor = Color.black;
                nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
                nameTmp.enableAutoSizing = true;
                nameTmp.fontSizeMin = 2.2f;
                nameTmp.fontSizeMax = 4.4f;
                if (sdfFont != null) nameTmp.font = sdfFont;
                var nameRt = nameObj.GetComponent<RectTransform>();
                if (nameRt != null) nameRt.sizeDelta = new Vector2(8.5f, 2.0f);

                // Serialização dos campos no TerritoryView
                var viewSo = new SerializedObject(terrView);
                viewSo.FindProperty("territoryId").stringValue = def.Id;
                viewSo.FindProperty("territoryRenderer").objectReferenceValue = meshRenderer;
                viewSo.FindProperty("tacticalPinRoot").objectReferenceValue = pinRoot.transform;
                viewSo.FindProperty("nameTagRoot").objectReferenceValue = nameTagRoot;
                viewSo.FindProperty("pinBadgeRenderer").objectReferenceValue = badgeRend;
                viewSo.FindProperty("pinRimRenderer").objectReferenceValue = rimRend;
                viewSo.FindProperty("armySpawnPoint").objectReferenceValue = spawnPoint.transform;
                viewSo.FindProperty("armyCountText").objectReferenceValue = armyCountTmp;
                viewSo.FindProperty("territoryNameText").objectReferenceValue = nameTmp;
                viewSo.ApplyModifiedProperties();

                views.Add(terrView);
            }

            return views;
        }

        private static GameObject SetupTacticalCamera(Transform parent)
        {
            var cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            GameObject cameraObj;

            if (cam == null)
            {
                cameraObj = new GameObject("TacticalCamera");
                cameraObj.tag = "MainCamera";
                cam = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
            }
            else
            {
                cameraObj = cam.gameObject;
            }

            cameraObj.SetActive(true);
            cam.enabled = true;
            cam.targetDisplay = 0;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 1f);

            cameraObj.transform.SetParent(parent, false);
            var defaultPitch = 50f;
            var defaultYaw = 0f;
            var defaultDistance = 35f;
            var rot = Quaternion.Euler(defaultPitch, defaultYaw, 0f);
            cameraObj.transform.position = rot * new Vector3(0f, 0f, -defaultDistance);
            cameraObj.transform.LookAt(Vector3.zero);

            var controller = cameraObj.GetComponent<TacticalCameraController>();
            if (controller == null)
            {
                controller = cameraObj.AddComponent<TacticalCameraController>();
            }

            return cameraObj;
        }

        private static void SetupDirectionalLight(Transform parent)
        {
            var lightObj = GameObject.Find("Directional Light");
            Light lightComponent;

            if (lightObj == null)
            {
                lightObj = new GameObject("Directional Light");
                lightComponent = lightObj.AddComponent<Light>();
                lightComponent.type = LightType.Directional;
            }
            else
            {
                lightComponent = lightObj.GetComponent<Light>();
            }

            lightObj.transform.SetParent(parent, false);
            lightObj.transform.rotation = Quaternion.Euler(58f, -28f, 0f);

            lightComponent.color = new Color(1.0f, 0.99f, 0.97f); // Luz difusa suave de estúdio/galeria
            lightComponent.intensity = 1.0f;
            lightComponent.shadows = LightShadows.Soft;
            lightComponent.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            lightComponent.shadowNearPlane = 0.5f;
            lightComponent.shadowBias = 0.04f;
            lightComponent.shadowNormalBias = 0.35f;

            // Iluminação de preenchimento ambiente suave (RenderSettings)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.44f, 0.52f, 1f); // Tom ambiente balanceado (evita queima de brancos)
        }

        /// <summary>
        /// Mapeamento de coordenadas (X, Z) centrais exatas para os 42 territórios clássicos no plano da mesa.
        /// Calibrado com encaixe milimétrico para as peças 3D volumétricas.
        /// </summary>
        private static Dictionary<string, Vector2> GetTerritoryWorldCoordinates()
        {
            return new Dictionary<string, Vector2>(System.StringComparer.OrdinalIgnoreCase)
            {
                // América do Sul (6)
                [TerritoryIds.Venezuela] = new Vector2(-13.02f, -1.97f),
                [TerritoryIds.Peru] = new Vector2(-13.20f, -5.40f),
                [TerritoryIds.Brasil] = new Vector2(-10.09f, -4.75f),
                [TerritoryIds.Chile] = new Vector2(-14.10f, -9.00f),
                [TerritoryIds.Argentina] = new Vector2(-12.38f, -10.40f),
                [TerritoryIds.Uruguai] = new Vector2(-10.20f, -8.70f),

                // América do Norte (8)
                [TerritoryIds.Alasca] = new Vector2(-21.66f, 9.99f),
                [TerritoryIds.Mackenzie] = new Vector2(-16.50f, 9.20f),
                [TerritoryIds.Groenlandia] = new Vector2(-7.91f, 11.69f),
                [TerritoryIds.Ottawa] = new Vector2(-14.18f, 7.13f),
                [TerritoryIds.Labrador] = new Vector2(-10.97f, 7.22f),
                [TerritoryIds.California] = new Vector2(-17.27f, 4.64f),
                [TerritoryIds.NovaYork] = new Vector2(-13.73f, 3.92f),
                [TerritoryIds.Mexico] = new Vector2(-16.91f, 0.96f),

                // Europa (7)
                [TerritoryIds.Islandia] = new Vector2(-3.75f, 8.82f),
                [TerritoryIds.Inglaterra] = new Vector2(-4.34f, 5.55f),
                [TerritoryIds.Franca] = new Vector2(-3.50f, 1.68f),
                [TerritoryIds.Alemanha] = new Vector2(-0.07f, 4.81f),
                [TerritoryIds.Suecia] = new Vector2(0.42f, 8.98f),
                [TerritoryIds.Polonia] = new Vector2(0.36f, 2.14f),
                [TerritoryIds.RussiaEuropeia] = new Vector2(4.68f, 6.81f),

                // África (6)
                [TerritoryIds.Argelia] = new Vector2(-1.63f, -3.62f),
                [TerritoryIds.Egito] = new Vector2(1.82f, -2.22f),
                [TerritoryIds.Sudao] = new Vector2(4.23f, -5.91f),
                [TerritoryIds.Congo] = new Vector2(1.93f, -7.31f),
                [TerritoryIds.AfricaDoSul] = new Vector2(2.31f, -10.93f),
                [TerritoryIds.Madagascar] = new Vector2(6.65f, -11.26f),

                // Ásia (9)
                [TerritoryIds.RussiaAsiatica] = new Vector2(14.50f, 8.80f),
                [TerritoryIds.Cazaquistao] = new Vector2(8.44f, 3.84f),
                [TerritoryIds.ArabiaSaudita] = new Vector2(5.20f, -2.60f),
                [TerritoryIds.Ira] = new Vector2(7.30f, 0.40f),
                [TerritoryIds.China] = new Vector2(14.26f, 1.86f),
                [TerritoryIds.India] = new Vector2(11.18f, -0.79f),
                [TerritoryIds.Mongolia] = new Vector2(15.20f, 5.20f),
                [TerritoryIds.Japao] = new Vector2(20.50f, 4.50f),
                [TerritoryIds.Vietna] = new Vector2(15.36f, -2.05f),

                // Oceania (5)
                [TerritoryIds.Australia] = new Vector2(19.80f, -10.20f),
                [TerritoryIds.NovaGuine] = new Vector2(19.80f, -5.50f),
                [TerritoryIds.IlhasSalomao] = new Vector2(22.60f, -5.80f),
                [TerritoryIds.Fiji] = new Vector2(24.50f, -8.20f),
                [TerritoryIds.NovaZelandia] = new Vector2(23.20f, -12.50f)
            };
        }

        /// <summary>
        /// Detecta o shader Lit apropriado com base no Pipeline ativo ou usa o shader padrão nativo
        /// para garantir que nenhum material fique rosa/magenta na cena.
        /// </summary>
        public static Shader GetSafeLitShader()
        {
            var isUsingUrp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;

            if (isUsingUrp)
            {
                var urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit != null) return urpLit;

                var urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpSimpleLit != null) return urpSimpleLit;

                var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpUnlit != null) return urpUnlit;
            }

            var standard = Shader.Find("Standard");
            if (standard != null) return standard;

            var diffuse = Shader.Find("Diffuse") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (diffuse != null) return diffuse;

            return Shader.Find("Unlit/Color");
        }

        #region Criação da Hierarquia War_Canvas (uGUI)
        /// <summary>
        /// Gera o Canvas de sobreposição de tela com toda a hierarquia de HUD, Combate e Objetivos.
        /// </summary>
        public static GameObject SetupGameCanvas(GamePresenter? presenter)
        {
            const string canvasName = "War_Canvas";
            var existing = GameObject.Find(canvasName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var canvasObj = new GameObject(canvasName);
            Undo.RegisterCreatedObjectUndo(canvasObj, "Gerar War_Canvas");

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Assegura EventSystem na cena
            EnsureEventSystem();

            // 1. Criação do HUD_Root e GameHUD
            var hudRoot = CreateUiElement("HUD_Root", canvasObj.transform);
            StretchFull(hudRoot.GetComponent<RectTransform>());
            var gameHud = hudRoot.AddComponent<GameHUD>();

            // 1.1 TopBar
            var topPanel = CreateUiElement("HUD_TopPanel", hudRoot.transform);
            var topRt = topPanel.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = Vector2.zero;
            topRt.sizeDelta = new Vector2(0f, 85f);
            var topImg = topPanel.AddComponent<Image>();
            topImg.color = new Color(0.09f, 0.11f, 0.15f, 0.95f);

            // Jogador Ativo (Badge)
            var playerBadge = CreateUiElement("PlayerBadge", topPanel.transform);
            var pbRt = playerBadge.GetComponent<RectTransform>();
            pbRt.anchorMin = new Vector2(0f, 0.5f);
            pbRt.anchorMax = new Vector2(0f, 0.5f);
            pbRt.pivot = new Vector2(0f, 0.5f);
            pbRt.anchoredPosition = new Vector2(25f, 0f);
            pbRt.sizeDelta = new Vector2(240f, 55f);
            var pbImg = playerBadge.AddComponent<Image>();
            pbImg.color = new Color(0.15f, 0.18f, 0.24f, 0.9f);

            var colorDot = CreateUiElement("PlayerColorDot", playerBadge.transform);
            var dotRt = colorDot.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0f, 0.5f);
            dotRt.anchorMax = new Vector2(0f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = new Vector2(25f, 0f);
            dotRt.sizeDelta = new Vector2(22f, 22f);
            var playerColorBadge = colorDot.AddComponent<Image>();
            playerColorBadge.color = new Color(0.88f, 0.22f, 0.22f, 1f);

            var playerNameObj = CreateUiElement("Text_PlayerName", playerBadge.transform);
            var pnRt = playerNameObj.GetComponent<RectTransform>();
            pnRt.anchorMin = new Vector2(0f, 0f);
            pnRt.anchorMax = new Vector2(1f, 1f);
            pnRt.offsetMin = new Vector2(45f, 0f);
            pnRt.offsetMax = new Vector2(-10f, 0f);
            var playerNameText = CreateTMP(playerNameObj, "Exército Vermelho", 17f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

            // Fase Atual (Container central)
            var phaseContainer = CreateUiElement("PhaseContainer", topPanel.transform);
            var pcRt = phaseContainer.GetComponent<RectTransform>();
            pcRt.anchorMin = new Vector2(0.5f, 0.5f);
            pcRt.anchorMax = new Vector2(0.5f, 0.5f);
            pcRt.pivot = new Vector2(0.5f, 0.5f);
            pcRt.anchoredPosition = Vector2.zero;
            pcRt.sizeDelta = new Vector2(500f, 65f);
            var pcImg = phaseContainer.AddComponent<Image>();
            pcImg.color = new Color(0.13f, 0.16f, 0.22f, 0.85f);

            var phaseTitleObj = CreateUiElement("Text_PhaseTitle", phaseContainer.transform);
            var ptRt = phaseTitleObj.GetComponent<RectTransform>();
            ptRt.anchorMin = new Vector2(0f, 0.5f);
            ptRt.anchorMax = new Vector2(1f, 1f);
            ptRt.offsetMin = new Vector2(10f, 0f);
            ptRt.offsetMax = new Vector2(-10f, -4f);
            var phaseTitleText = CreateTMP(phaseTitleObj, "FASE DE REFORÇO", 18f, new Color(1f, 0.82f, 0.25f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            var phaseDescObj = CreateUiElement("Text_PhaseInstruction", phaseContainer.transform);
            var pdRt = phaseDescObj.GetComponent<RectTransform>();
            pdRt.anchorMin = new Vector2(0f, 0f);
            pdRt.anchorMax = new Vector2(1f, 0.5f);
            pdRt.offsetMin = new Vector2(10f, 4f);
            pdRt.offsetMax = new Vector2(-10f, 0f);
            var phaseInstructionText = CreateTMP(phaseDescObj, "Clique nos seus territórios para posicionar tropas de reforço.", 13f, new Color(0.85f, 0.88f, 0.92f, 1f), TextAlignmentOptions.Center);

            // Reforços a posicionar
            var armiesBadge = CreateUiElement("ArmiesBadge", topPanel.transform);
            var abRt = armiesBadge.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(1f, 0.5f);
            abRt.anchorMax = new Vector2(1f, 0.5f);
            abRt.pivot = new Vector2(1f, 0.5f);
            abRt.anchoredPosition = new Vector2(-385f, 0f);
            abRt.sizeDelta = new Vector2(230f, 48f);
            var abImg = armiesBadge.AddComponent<Image>();
            abImg.color = new Color(0.12f, 0.23f, 0.45f, 0.95f);

            var armiesTextObj = CreateUiElement("Text_ArmiesToDeploy", armiesBadge.transform);
            var atRt = armiesTextObj.GetComponent<RectTransform>();
            StretchFull(atRt);
            var armiesToDeployText = CreateTMP(armiesTextObj, "Tropas para posicionar: 5", 14f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Botões de Ação
            var buttonsGroup = CreateUiElement("ButtonsGroup", topPanel.transform);
            var bgRt = buttonsGroup.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(1f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(1f, 0.5f);
            bgRt.anchoredPosition = new Vector2(-20f, 0f);
            bgRt.sizeDelta = new Vector2(350f, 52f);

            var advBtnObj = CreateUiElement("Btn_AdvancePhase", buttonsGroup.transform);
            var advRt = advBtnObj.GetComponent<RectTransform>();
            advRt.anchorMin = new Vector2(0f, 0.5f);
            advRt.anchorMax = new Vector2(0f, 0.5f);
            advRt.pivot = new Vector2(0f, 0.5f);
            advRt.anchoredPosition = Vector2.zero;
            advRt.sizeDelta = new Vector2(190f, 48f);
            var advBtnImg = advBtnObj.AddComponent<Image>();
            advBtnImg.color = new Color(0.04f, 0.6f, 0.4f, 1f);
            var advBtn = advBtnObj.AddComponent<Button>();

            var advBtnTextObj = CreateUiElement("Text_Label", advBtnObj.transform);
            StretchFull(advBtnTextObj.GetComponent<RectTransform>());
            var advancePhaseButtonText = CreateTMP(advBtnTextObj, "Avançar para Ataque", 14f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            var missionBtnObj = CreateUiElement("Btn_MyMission", buttonsGroup.transform);
            var misRt = missionBtnObj.GetComponent<RectTransform>();
            misRt.anchorMin = new Vector2(1f, 0.5f);
            misRt.anchorMax = new Vector2(1f, 0.5f);
            misRt.pivot = new Vector2(1f, 0.5f);
            misRt.anchoredPosition = Vector2.zero;
            misRt.sizeDelta = new Vector2(145f, 48f);
            var misBtnImg = missionBtnObj.AddComponent<Image>();
            misBtnImg.color = new Color(0.85f, 0.47f, 0.05f, 1f);
            var myMissionButton = missionBtnObj.AddComponent<Button>();

            var misBtnTextObj = CreateUiElement("Text_Label", missionBtnObj.transform);
            StretchFull(misBtnTextObj.GetComponent<RectTransform>());
            CreateTMP(misBtnTextObj, "Minha Missão", 14f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // 1.15 Fita Tática dos Bônus de Continente (HUD Ribbon elegante)
            var ribbonPanel = CreateUiElement("HUD_ContinentsRibbon", hudRoot.transform);
            var rbRt = ribbonPanel.GetComponent<RectTransform>();
            rbRt.anchorMin = new Vector2(0.5f, 1f);
            rbRt.anchorMax = new Vector2(0.5f, 1f);
            rbRt.pivot = new Vector2(0.5f, 1f);
            rbRt.anchoredPosition = new Vector2(0f, -92f);
            rbRt.sizeDelta = new Vector2(880f, 32f);
            var rbImg = ribbonPanel.AddComponent<Image>();
            rbImg.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);

            var rbTextObj = CreateUiElement("Text_ContinentsBonus", ribbonPanel.transform);
            StretchFull(rbTextObj.GetComponent<RectTransform>());
            CreateTMP(rbTextObj, "<b>BÔNUS:</b>  Am. Norte +5  •  Am. Sul +2  •  Europa +5  •  África +3  •  Ásia +7  •  Oceania +2", 13f, new Color(0.85f, 0.88f, 0.94f, 1f), TextAlignmentOptions.Center);

            // 1.2 BottomBar (Status)
            var bottomPanel = CreateUiElement("HUD_BottomPanel", hudRoot.transform);
            var botRt = bottomPanel.GetComponent<RectTransform>();
            botRt.anchorMin = new Vector2(0f, 0f);
            botRt.anchorMax = new Vector2(1f, 0f);
            botRt.pivot = new Vector2(0.5f, 0f);
            botRt.anchoredPosition = Vector2.zero;
            botRt.sizeDelta = new Vector2(0f, 48f);
            var botImg = bottomPanel.AddComponent<Image>();
            botImg.color = new Color(0.06f, 0.09f, 0.16f, 0.95f);

            var statusTextObj = CreateUiElement("Text_StatusMessage", bottomPanel.transform);
            var stRt = statusTextObj.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.offsetMin = new Vector2(30f, 0f);
            stRt.offsetMax = new Vector2(-30f, 0f);
            var statusMessageText = CreateTMP(statusTextObj, "Bem-vindo ao WAR! Posicione seus reforços para iniciar a marcha global.", 15f, new Color(0.95f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);

            // 2. Criação do Modal de Combate (CombatPanelUI)
            var combatPanelObj = SetupCombatPanelUI(canvasObj.transform, presenter);
            var combatPanelUI = combatPanelObj.GetComponent<CombatPanelUI>();

            // 3. Criação do Modal de Objetivo Secreto (ObjectiveModalUI)
            var objectiveModalObj = SetupObjectiveModalUI(canvasObj.transform);
            var objectiveModalUI = objectiveModalObj.GetComponent<ObjectiveModalUI>();

            // 4. Criação do Mini HUD Modal de Alocação de Tropas (DeployArmiesDialogUI)
            var deployDialogObj = SetupDeployArmiesDialogUI(canvasObj.transform);
            var deployDialogUI = deployDialogObj.GetComponent<DeployArmiesDialogUI>();

            // Conecta deployDialog no GamePresenter se presente
            if (presenter != null)
            {
                var presSo = new SerializedObject(presenter);
                var deployProp = presSo.FindProperty("deployDialog");
                if (deployProp != null)
                {
                    deployProp.objectReferenceValue = deployDialogUI;
                    presSo.ApplyModifiedProperties();
                }
            }

            // Serializa e conecta tudo no GameHUD
            var hudSo = new SerializedObject(gameHud);
            hudSo.FindProperty("presenter").objectReferenceValue = presenter;
            hudSo.FindProperty("objectiveModal").objectReferenceValue = objectiveModalUI;
            hudSo.FindProperty("combatPanel").objectReferenceValue = combatPanelUI;
            hudSo.FindProperty("deployDialog").objectReferenceValue = deployDialogUI;
            hudSo.FindProperty("playerColorBadge").objectReferenceValue = playerColorBadge;
            hudSo.FindProperty("playerNameText").objectReferenceValue = playerNameText;
            hudSo.FindProperty("phaseNameText").objectReferenceValue = phaseTitleText;
            hudSo.FindProperty("phaseInstructionText").objectReferenceValue = phaseInstructionText;
            hudSo.FindProperty("armiesBadgeRoot").objectReferenceValue = armiesBadge;
            hudSo.FindProperty("armiesToDeployText").objectReferenceValue = armiesToDeployText;
            hudSo.FindProperty("advancePhaseButton").objectReferenceValue = advBtn;
            hudSo.FindProperty("advancePhaseButtonText").objectReferenceValue = advancePhaseButtonText;
            hudSo.FindProperty("myMissionButton").objectReferenceValue = myMissionButton;
            hudSo.FindProperty("statusBarRoot").objectReferenceValue = bottomPanel;
            hudSo.FindProperty("statusMessageText").objectReferenceValue = statusMessageText;
            hudSo.ApplyModifiedProperties();

            return canvasObj;
        }

        private static GameObject SetupCombatPanelUI(Transform canvasTransform, GamePresenter? presenter)
        {
            var modalRoot = CreateUiElement("Modal_CombatPanel", canvasTransform);
            StretchFull(modalRoot.GetComponent<RectTransform>());

            var backdropImg = modalRoot.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.72f);

            var combatUI = modalRoot.AddComponent<CombatPanelUI>();

            // Card Central
            var cardObj = CreateUiElement("CombatCard", modalRoot.transform);
            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(760f, 580f);
            var cardImg = cardObj.AddComponent<Image>();
            cardImg.color = new Color(0.11f, 0.13f, 0.18f, 1f);

            // Título
            var titleObj = CreateUiElement("Text_HeaderTitle", cardObj.transform);
            var tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -20f);
            tRt.sizeDelta = new Vector2(0f, 40f);
            CreateTMP(titleObj, "RELATÓRIO DE COMBATE", 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Linha de Territórios (Atacante vs Defensor)
            var terrRow = CreateUiElement("TerritoriesRow", cardObj.transform);
            var trRt = terrRow.GetComponent<RectTransform>();
            trRt.anchorMin = new Vector2(0f, 1f);
            trRt.anchorMax = new Vector2(1f, 1f);
            trRt.pivot = new Vector2(0.5f, 1f);
            trRt.anchoredPosition = new Vector2(0f, -65f);
            trRt.sizeDelta = new Vector2(700f, 50f);

            // Badge Atacante
            var atkBadge = CreateUiElement("AttackerBadge", terrRow.transform);
            var abRt = atkBadge.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(0f, 0.5f);
            abRt.anchorMax = new Vector2(0f, 0.5f);
            abRt.pivot = new Vector2(0f, 0.5f);
            abRt.anchoredPosition = Vector2.zero;
            abRt.sizeDelta = new Vector2(290f, 48f);
            var attackerColorBadge = atkBadge.AddComponent<Image>();
            attackerColorBadge.color = new Color(0.6f, 0.15f, 0.15f, 1f);

            var atkTextObj = CreateUiElement("Text_AttackerName", atkBadge.transform);
            StretchFull(atkTextObj.GetComponent<RectTransform>());
            var attackerTerritoryText = CreateTMP(atkTextObj, "⚔ ATACANTE", 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // VS Label
            var vsObj = CreateUiElement("Text_VS", terrRow.transform);
            var vsRt = vsObj.GetComponent<RectTransform>();
            vsRt.anchorMin = new Vector2(0.5f, 0.5f);
            vsRt.anchorMax = new Vector2(0.5f, 0.5f);
            vsRt.pivot = new Vector2(0.5f, 0.5f);
            vsRt.anchoredPosition = Vector2.zero;
            vsRt.sizeDelta = new Vector2(60f, 48f);
            CreateTMP(vsObj, "VS", 18f, new Color(0.7f, 0.75f, 0.85f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Badge Defensor
            var defBadge = CreateUiElement("DefenderBadge", terrRow.transform);
            var dbRt = defBadge.GetComponent<RectTransform>();
            dbRt.anchorMin = new Vector2(1f, 0.5f);
            dbRt.anchorMax = new Vector2(1f, 0.5f);
            dbRt.pivot = new Vector2(1f, 0.5f);
            dbRt.anchoredPosition = Vector2.zero;
            dbRt.sizeDelta = new Vector2(290f, 48f);
            var defenderColorBadge = defBadge.AddComponent<Image>();
            defenderColorBadge.color = new Color(0.55f, 0.42f, 0.1f, 1f);

            var defTextObj = CreateUiElement("Text_DefenderName", defBadge.transform);
            StretchFull(defTextObj.GetComponent<RectTransform>());
            var defenderTerritoryText = CreateTMP(defTextObj, "🛡 DEFENSOR", 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Seção de Dados (Attacker & Defender)
            var diceSection = CreateUiElement("DiceSection", cardObj.transform);
            var dsRt = diceSection.GetComponent<RectTransform>();
            dsRt.anchorMin = new Vector2(0.5f, 0.5f);
            dsRt.anchorMax = new Vector2(0.5f, 0.5f);
            dsRt.pivot = new Vector2(0.5f, 0.5f);
            dsRt.anchoredPosition = new Vector2(0f, 95f);
            dsRt.sizeDelta = new Vector2(700f, 85f);

            // Dados do Atacante (3 caixas vermelhas)
            var atkDiceBoxes = new GameObject[3];
            var atkDiceTexts = new TMP_Text[3];
            for (var i = 0; i < 3; i++)
            {
                var box = CreateUiElement($"Die_Atk_{i + 1}", diceSection.transform);
                var bRt = box.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0f, 0.5f);
                bRt.anchorMax = new Vector2(0f, 0.5f);
                bRt.pivot = new Vector2(0.5f, 0.5f);
                bRt.anchoredPosition = new Vector2(50f + i * 85f, 0f);
                bRt.sizeDelta = new Vector2(65f, 65f);
                var bImg = box.AddComponent<Image>();
                bImg.color = new Color(0.86f, 0.15f, 0.15f, 1f);

                var txtObj = CreateUiElement("Text_DieVal", box.transform);
                StretchFull(txtObj.GetComponent<RectTransform>());
                var dTxt = CreateTMP(txtObj, "6", 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

                atkDiceBoxes[i] = box;
                atkDiceTexts[i] = dTxt;
            }

            // Dados do Defensor (3 caixas amarelas)
            var defDiceBoxes = new GameObject[3];
            var defDiceTexts = new TMP_Text[3];
            for (var i = 0; i < 3; i++)
            {
                var box = CreateUiElement($"Die_Def_{i + 1}", diceSection.transform);
                var bRt = box.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(1f, 0.5f);
                bRt.anchorMax = new Vector2(1f, 0.5f);
                bRt.pivot = new Vector2(0.5f, 0.5f);
                bRt.anchoredPosition = new Vector2(-220f + i * 85f, 0f);
                bRt.sizeDelta = new Vector2(65f, 65f);
                var bImg = box.AddComponent<Image>();
                bImg.color = new Color(0.92f, 0.72f, 0.1f, 1f);

                var txtObj = CreateUiElement("Text_DieVal", box.transform);
                StretchFull(txtObj.GetComponent<RectTransform>());
                var dTxt = CreateTMP(txtObj, "5", 32f, new Color(0.1f, 0.1f, 0.15f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

                defDiceBoxes[i] = box;
                defDiceTexts[i] = dTxt;
            }

            // Comparações Par a Par Box
            var compBox = CreateUiElement("ComparisonsPanel", cardObj.transform);
            var cpRt = compBox.GetComponent<RectTransform>();
            cpRt.anchorMin = new Vector2(0.5f, 0.5f);
            cpRt.anchorMax = new Vector2(0.5f, 0.5f);
            cpRt.pivot = new Vector2(0.5f, 0.5f);
            cpRt.anchoredPosition = new Vector2(0f, -15f);
            cpRt.sizeDelta = new Vector2(680f, 105f);
            var cpImg = compBox.AddComponent<Image>();
            cpImg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

            var compTextObj = CreateUiElement("Text_Comparisons", compBox.transform);
            var ctRt = compTextObj.GetComponent<RectTransform>();
            ctRt.anchorMin = new Vector2(0f, 0f);
            ctRt.anchorMax = new Vector2(1f, 1f);
            ctRt.offsetMin = new Vector2(15f, 8f);
            ctRt.offsetMax = new Vector2(-15f, -8f);
            var comparisonsText = CreateTMP(compTextObj, "Dado 1: [6] vs [5] ➜ Vitória do Atacante\nDado 2: [4] vs [4] ➜ Vitória da Defesa (Empate favorece a defesa)", 14f, new Color(0.9f, 0.93f, 0.98f, 1f), TextAlignmentOptions.Left);

            // Resumo de Baixas
            var lossesObj = CreateUiElement("Text_LossesSummary", cardObj.transform);
            var lRt = lossesObj.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0.5f, 0.5f);
            lRt.anchorMax = new Vector2(0.5f, 0.5f);
            lRt.pivot = new Vector2(0.5f, 0.5f);
            lRt.anchoredPosition = new Vector2(0f, -95f);
            lRt.sizeDelta = new Vector2(680f, 32f);
            var lossesSummaryText = CreateTMP(lossesObj, "Baixas: Atacante perdeu 1 | Defensor perdeu 1", 16f, new Color(1f, 0.45f, 0.45f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Banner de Conquista
            var bannerObj = CreateUiElement("ConquestBanner", cardObj.transform);
            var bnRt = bannerObj.GetComponent<RectTransform>();
            bnRt.anchorMin = new Vector2(0.5f, 0.5f);
            bnRt.anchorMax = new Vector2(0.5f, 0.5f);
            bnRt.pivot = new Vector2(0.5f, 0.5f);
            bnRt.anchoredPosition = new Vector2(0f, -145f);
            bnRt.sizeDelta = new Vector2(680f, 44f);
            var bnImg = bannerObj.AddComponent<Image>();
            bnImg.color = new Color(0.09f, 0.64f, 0.29f, 1f);

            var bannerTextObj = CreateUiElement("Text_ConquestLabel", bannerObj.transform);
            StretchFull(bannerTextObj.GetComponent<RectTransform>());
            var conquestBannerText = CreateTMP(bannerTextObj, "★ TERRITÓRIO CONQUISTADO! ★", 16f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Botão Fechar / Continuar
            var closeBtnObj = CreateUiElement("Btn_CloseCombat", cardObj.transform);
            var cbRt = closeBtnObj.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0.5f, 0f);
            cbRt.anchorMax = new Vector2(0.5f, 0f);
            cbRt.pivot = new Vector2(0.5f, 0f);
            cbRt.anchoredPosition = new Vector2(0f, 25f);
            cbRt.sizeDelta = new Vector2(220f, 48f);
            var cbImg = closeBtnObj.AddComponent<Image>();
            cbImg.color = new Color(0.15f, 0.45f, 0.85f, 1f);
            var closeBtn = closeBtnObj.AddComponent<Button>();

            var cbTxtObj = CreateUiElement("Text_Label", closeBtnObj.transform);
            StretchFull(cbTxtObj.GetComponent<RectTransform>());
            CreateTMP(cbTxtObj, "Continuar", 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Serializa campos no CombatPanelUI
            var cSo = new SerializedObject(combatUI);
            cSo.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            cSo.FindProperty("attackerTerritoryText").objectReferenceValue = attackerTerritoryText;
            cSo.FindProperty("attackerColorBadge").objectReferenceValue = attackerColorBadge;
            cSo.FindProperty("defenderTerritoryText").objectReferenceValue = defenderTerritoryText;
            cSo.FindProperty("defenderColorBadge").objectReferenceValue = defenderColorBadge;
            cSo.FindProperty("comparisonsText").objectReferenceValue = comparisonsText;
            cSo.FindProperty("lossesSummaryText").objectReferenceValue = lossesSummaryText;
            cSo.FindProperty("conquestBanner").objectReferenceValue = bannerObj;
            cSo.FindProperty("conquestBannerText").objectReferenceValue = conquestBannerText;
            cSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            cSo.FindProperty("presenter").objectReferenceValue = presenter;

            var atkBoxesProp = cSo.FindProperty("attackerDiceBoxes");
            atkBoxesProp.arraySize = 3;
            for (var i = 0; i < 3; i++) atkBoxesProp.GetArrayElementAtIndex(i).objectReferenceValue = atkDiceBoxes[i];

            var atkTextsProp = cSo.FindProperty("attackerDiceTexts");
            atkTextsProp.arraySize = 3;
            for (var i = 0; i < 3; i++) atkTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = atkDiceTexts[i];

            var defBoxesProp = cSo.FindProperty("defenderDiceBoxes");
            defBoxesProp.arraySize = 3;
            for (var i = 0; i < 3; i++) defBoxesProp.GetArrayElementAtIndex(i).objectReferenceValue = defDiceBoxes[i];

            var defTextsProp = cSo.FindProperty("defenderDiceTexts");
            defTextsProp.arraySize = 3;
            for (var i = 0; i < 3; i++) defTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = defDiceTexts[i];

            cSo.ApplyModifiedProperties();

            // Começa fechado
            modalRoot.SetActive(false);

            return modalRoot;
        }

        private static GameObject SetupObjectiveModalUI(Transform canvasTransform)
        {
            var modalRoot = CreateUiElement("Modal_ObjectivePanel", canvasTransform);
            StretchFull(modalRoot.GetComponent<RectTransform>());

            var backdropImg = modalRoot.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.72f);

            var objUI = modalRoot.AddComponent<ObjectiveModalUI>();

            // Card Central
            var cardObj = CreateUiElement("ObjectiveCard", modalRoot.transform);
            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(680f, 500f);
            var cardImg = cardObj.AddComponent<Image>();
            cardImg.color = new Color(0.1f, 0.13f, 0.18f, 1f);

            // Cabeçalho
            var headerObj = CreateUiElement("Text_HeaderTitle", cardObj.transform);
            var hRt = headerObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = new Vector2(0f, -20f);
            hRt.sizeDelta = new Vector2(0f, 35f);
            CreateTMP(headerObj, "ORDEM SECRETA DO ESTADO-MAIOR", 20f, new Color(0.96f, 0.62f, 0.12f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Identificador do Jogador
            var playerInfo = CreateUiElement("PlayerInfoRow", cardObj.transform);
            var piRt = playerInfo.GetComponent<RectTransform>();
            piRt.anchorMin = new Vector2(0.5f, 1f);
            piRt.anchorMax = new Vector2(0.5f, 1f);
            piRt.pivot = new Vector2(0.5f, 1f);
            piRt.anchoredPosition = new Vector2(0f, -60f);
            piRt.sizeDelta = new Vector2(300f, 32f);

            var dotObj = CreateUiElement("Dot", playerInfo.transform);
            var dRt = dotObj.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0f, 0.5f);
            dRt.anchorMax = new Vector2(0f, 0.5f);
            dRt.pivot = new Vector2(0.5f, 0.5f);
            dRt.anchoredPosition = new Vector2(16f, 0f);
            dRt.sizeDelta = new Vector2(18f, 18f);
            var playerColorBadge = dotObj.AddComponent<Image>();
            playerColorBadge.color = new Color(0.88f, 0.22f, 0.22f, 1f);

            var pnObj = CreateUiElement("Text_PlayerName", playerInfo.transform);
            var pnRt = pnObj.GetComponent<RectTransform>();
            pnRt.anchorMin = new Vector2(0f, 0f);
            pnRt.anchorMax = new Vector2(1f, 1f);
            pnRt.offsetMin = new Vector2(35f, 0f);
            pnRt.offsetMax = Vector2.zero;
            var playerNameText = CreateTMP(pnObj, "Missão de: Exército Vermelho", 15f, new Color(0.75f, 0.8f, 0.88f, 1f), TextAlignmentOptions.Left);

            // Título da Missão
            var titleObj = CreateUiElement("Text_MissionTitle", cardObj.transform);
            var tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 1f);
            tRt.anchorMax = new Vector2(0.5f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -100f);
            tRt.sizeDelta = new Vector2(620f, 40f);
            var titleText = CreateTMP(titleObj, "Conquistar 24 Territórios", 20f, new Color(0.22f, 0.74f, 0.97f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Descrição da Missão
            var descPanel = CreateUiElement("DescPanel", cardObj.transform);
            var dpRt = descPanel.GetComponent<RectTransform>();
            dpRt.anchorMin = new Vector2(0.5f, 0.5f);
            dpRt.anchorMax = new Vector2(0.5f, 0.5f);
            dpRt.pivot = new Vector2(0.5f, 0.5f);
            dpRt.anchoredPosition = new Vector2(0f, 40f);
            dpRt.sizeDelta = new Vector2(620f, 110f);
            var dpImg = descPanel.AddComponent<Image>();
            dpImg.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

            var descTextObj = CreateUiElement("Text_Desc", descPanel.transform);
            var dtRt = descTextObj.GetComponent<RectTransform>();
            dtRt.anchorMin = new Vector2(0f, 0f);
            dtRt.anchorMax = new Vector2(1f, 1f);
            dtRt.offsetMin = new Vector2(20f, 10f);
            dtRt.offsetMax = new Vector2(-20f, -10f);
            var descriptionText = CreateTMP(descTextObj, "Conquiste 24 territórios à sua escolha pelo mundo para consagrar a vitória militar sobre todos os outros generais.", 15f, new Color(0.92f, 0.94f, 0.97f, 1f), TextAlignmentOptions.Left);

            // Painel de Progresso
            var progPanel = CreateUiElement("ProgressPanel", cardObj.transform);
            var ppRt = progPanel.GetComponent<RectTransform>();
            ppRt.anchorMin = new Vector2(0.5f, 0.5f);
            ppRt.anchorMax = new Vector2(0.5f, 0.5f);
            ppRt.pivot = new Vector2(0.5f, 0.5f);
            ppRt.anchoredPosition = new Vector2(0f, -75f);
            ppRt.sizeDelta = new Vector2(620f, 95f);
            var ppImg = progPanel.AddComponent<Image>();
            ppImg.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

            var progTextObj = CreateUiElement("Text_Progress", progPanel.transform);
            var prRt = progTextObj.GetComponent<RectTransform>();
            prRt.anchorMin = new Vector2(0f, 0f);
            prRt.anchorMax = new Vector2(1f, 1f);
            prRt.offsetMin = new Vector2(20f, 10f);
            prRt.offsetMax = new Vector2(-20f, -10f);
            var progressText = CreateTMP(progTextObj, "Progresso de Territórios:\n• Dominados: 12 / 24 (50%)", 14f, new Color(0.2f, 0.83f, 0.6f, 1f), TextAlignmentOptions.Left);

            // Botão Fechar
            var closeBtnObj = CreateUiElement("Btn_CloseObjective", cardObj.transform);
            var cbRt = closeBtnObj.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0.5f, 0f);
            cbRt.anchorMax = new Vector2(0.5f, 0f);
            cbRt.pivot = new Vector2(0.5f, 0f);
            cbRt.anchoredPosition = new Vector2(0f, 22f);
            cbRt.sizeDelta = new Vector2(180f, 44f);
            var cbImg = closeBtnObj.AddComponent<Image>();
            cbImg.color = new Color(0.28f, 0.33f, 0.41f, 1f);
            var closeBtn = closeBtnObj.AddComponent<Button>();

            var cbTxtObj = CreateUiElement("Text_Label", closeBtnObj.transform);
            StretchFull(cbTxtObj.GetComponent<RectTransform>());
            CreateTMP(cbTxtObj, "Fechar Missão", 14f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Serializa campos no ObjectiveModalUI
            var oSo = new SerializedObject(objUI);
            oSo.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            oSo.FindProperty("titleText").objectReferenceValue = titleText;
            oSo.FindProperty("descriptionText").objectReferenceValue = descriptionText;
            oSo.FindProperty("playerNameText").objectReferenceValue = playerNameText;
            oSo.FindProperty("progressText").objectReferenceValue = progressText;
            oSo.FindProperty("playerColorBadge").objectReferenceValue = playerColorBadge;
            oSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            oSo.ApplyModifiedProperties();

            // Começa fechado
            modalRoot.SetActive(false);

            return modalRoot;
        }

        private static GameObject SetupDeployArmiesDialogUI(Transform canvasTransform)
        {
            var modalRoot = CreateUiElement("Modal_DeployArmiesDialog", canvasTransform);
            StretchFull(modalRoot.GetComponent<RectTransform>());

            var backdropImg = modalRoot.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.65f);

            var deployUI = modalRoot.AddComponent<DeployArmiesDialogUI>();

            // Card Central
            var cardObj = CreateUiElement("DeployCard", modalRoot.transform);
            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(520f, 410f);
            var cardImg = cardObj.AddComponent<Image>();
            cardImg.color = new Color(0.12f, 0.15f, 0.20f, 0.98f);

            // Botão Fechar (X no canto superior direito)
            var closeBtnObj = CreateUiElement("Btn_CloseX", cardObj.transform);
            var closeRt = closeBtnObj.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-15f, -15f);
            closeRt.sizeDelta = new Vector2(36f, 36f);
            var closeBtnImg = closeBtnObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.24f, 0.28f, 0.35f, 1f);
            var closeBtn = closeBtnObj.AddComponent<Button>();

            var closeTxtObj = CreateUiElement("Text_X", closeBtnObj.transform);
            StretchFull(closeTxtObj.GetComponent<RectTransform>());
            CreateTMP(closeTxtObj, "✕", 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Título
            var titleObj = CreateUiElement("Text_Title", cardObj.transform);
            var tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -22f);
            tRt.sizeDelta = new Vector2(-100f, 38f);
            var titleText = CreateTMP(titleObj, "Posicionar Tropas", 20f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Painel de Informações do Território
            var infoPanel = CreateUiElement("TerritoryInfoPanel", cardObj.transform);
            var infoRt = infoPanel.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0.5f, 1f);
            infoRt.anchorMax = new Vector2(0.5f, 1f);
            infoRt.pivot = new Vector2(0.5f, 1f);
            infoRt.anchoredPosition = new Vector2(0f, -72f);
            infoRt.sizeDelta = new Vector2(460f, 46f);
            var infoBg = infoPanel.AddComponent<Image>();
            infoBg.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);

            // Badge de Cor do Território
            var badgeObj = CreateUiElement("TerritoryColorBadge", infoPanel.transform);
            var bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.5f);
            bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(14f, 0f);
            bRt.sizeDelta = new Vector2(18f, 18f);
            var colorBadge = badgeObj.AddComponent<Image>();
            colorBadge.color = new Color(0.88f, 0.22f, 0.22f, 1f);

            // Texto de Detalhes do Território
            var detailsObj = CreateUiElement("Text_Details", infoPanel.transform);
            var dRt = detailsObj.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0f, 0f);
            dRt.anchorMax = new Vector2(1f, 1f);
            dRt.offsetMin = new Vector2(42f, 0f);
            dRt.offsetMax = new Vector2(-14f, 0f);
            var territoryInfoText = CreateTMP(detailsObj, "Continente • Tropas atuais: 1", 14f, new Color(0.78f, 0.84f, 0.92f, 1f), TextAlignmentOptions.Left);

            // Indicador de Saldo de Tropas Disponíveis
            var availObj = CreateUiElement("Text_AvailableArmies", cardObj.transform);
            var aRt = availObj.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0.5f, 1f);
            aRt.anchorMax = new Vector2(0.5f, 1f);
            aRt.pivot = new Vector2(0.5f, 1f);
            aRt.anchoredPosition = new Vector2(0f, -132f);
            aRt.sizeDelta = new Vector2(460f, 32f);
            var availableArmiesText = CreateTMP(availObj, "Tropas de reforço disponíveis: 5", 15f, new Color(1f, 0.84f, 0.32f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Linha de Controle de Quantidade ([-] [1] [+] [Máx])
            var counterRow = CreateUiElement("CounterRow", cardObj.transform);
            var crRt = counterRow.GetComponent<RectTransform>();
            crRt.anchorMin = new Vector2(0.5f, 1f);
            crRt.anchorMax = new Vector2(0.5f, 1f);
            crRt.pivot = new Vector2(0.5f, 1f);
            crRt.anchoredPosition = new Vector2(0f, -195f);
            crRt.sizeDelta = new Vector2(460f, 64f);

            // Botão Diminuir [-]
            var decBtnObj = CreateUiElement("Btn_Decrease", counterRow.transform);
            var decRt = decBtnObj.GetComponent<RectTransform>();
            decRt.anchorMin = new Vector2(0.5f, 0.5f);
            decRt.anchorMax = new Vector2(0.5f, 0.5f);
            decRt.pivot = new Vector2(0.5f, 0.5f);
            decRt.anchoredPosition = new Vector2(-110f, 0f);
            decRt.sizeDelta = new Vector2(56f, 56f);
            var decImg = decBtnObj.AddComponent<Image>();
            decImg.color = new Color(0.24f, 0.29f, 0.38f, 1f);
            var decreaseBtn = decBtnObj.AddComponent<Button>();

            var decTxtObj = CreateUiElement("Text_Label", decBtnObj.transform);
            StretchFull(decTxtObj.GetComponent<RectTransform>());
            CreateTMP(decTxtObj, "-", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Display do Contador [ 1 ]
            var countBoxObj = CreateUiElement("CountBox", counterRow.transform);
            var cbRt = countBoxObj.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0.5f, 0.5f);
            cbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);
            cbRt.anchoredPosition = new Vector2(0f, 0f);
            cbRt.sizeDelta = new Vector2(100f, 56f);
            var countBg = countBoxObj.AddComponent<Image>();
            countBg.color = new Color(0.06f, 0.08f, 0.12f, 1f);

            var countTxtObj = CreateUiElement("Text_Count", countBoxObj.transform);
            StretchFull(countTxtObj.GetComponent<RectTransform>());
            var countText = CreateTMP(countTxtObj, "1", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Botão Aumentar [+]
            var incBtnObj = CreateUiElement("Btn_Increase", counterRow.transform);
            var incRt = incBtnObj.GetComponent<RectTransform>();
            incRt.anchorMin = new Vector2(0.5f, 0.5f);
            incRt.anchorMax = new Vector2(0.5f, 0.5f);
            incRt.pivot = new Vector2(0.5f, 0.5f);
            incRt.anchoredPosition = new Vector2(110f, 0f);
            incRt.sizeDelta = new Vector2(56f, 56f);
            var incImg = incBtnObj.AddComponent<Image>();
            incImg.color = new Color(0.24f, 0.29f, 0.38f, 1f);
            var increaseBtn = incBtnObj.AddComponent<Button>();

            var incTxtObj = CreateUiElement("Text_Label", incBtnObj.transform);
            StretchFull(incTxtObj.GetComponent<RectTransform>());
            CreateTMP(incTxtObj, "+", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Botão [Máx]
            var maxBtnObj = CreateUiElement("Btn_Max", counterRow.transform);
            var maxRt = maxBtnObj.GetComponent<RectTransform>();
            maxRt.anchorMin = new Vector2(0.5f, 0.5f);
            maxRt.anchorMax = new Vector2(0.5f, 0.5f);
            maxRt.pivot = new Vector2(0.5f, 0.5f);
            maxRt.anchoredPosition = new Vector2(195f, 0f);
            maxRt.sizeDelta = new Vector2(64f, 44f);
            var maxImg = maxBtnObj.AddComponent<Image>();
            maxImg.color = new Color(0.18f, 0.44f, 0.68f, 1f);
            var maxBtn = maxBtnObj.AddComponent<Button>();

            var maxTxtObj = CreateUiElement("Text_Label", maxBtnObj.transform);
            StretchFull(maxTxtObj.GetComponent<RectTransform>());
            CreateTMP(maxTxtObj, "Máx", 14f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Linha Inferior de Botões (Cancelar e Confirmar)
            var actionRow = CreateUiElement("ActionRow", cardObj.transform);
            var arRt = actionRow.GetComponent<RectTransform>();
            arRt.anchorMin = new Vector2(0f, 0f);
            arRt.anchorMax = new Vector2(1f, 0f);
            arRt.pivot = new Vector2(0.5f, 0f);
            arRt.anchoredPosition = new Vector2(0f, 24f);
            arRt.sizeDelta = new Vector2(-60f, 48f);

            // Botão Cancelar
            var cancelBtnObj = CreateUiElement("Btn_Cancel", actionRow.transform);
            var canRt = cancelBtnObj.GetComponent<RectTransform>();
            canRt.anchorMin = new Vector2(0.5f, 0.5f);
            canRt.anchorMax = new Vector2(0.5f, 0.5f);
            canRt.pivot = new Vector2(0.5f, 0.5f);
            canRt.anchoredPosition = new Vector2(-110f, 0f);
            canRt.sizeDelta = new Vector2(180f, 48f);
            var canImg = cancelBtnObj.AddComponent<Image>();
            canImg.color = new Color(0.28f, 0.32f, 0.39f, 1f);
            var cancelBtn = cancelBtnObj.AddComponent<Button>();

            var canTxtObj = CreateUiElement("Text_Label", cancelBtnObj.transform);
            StretchFull(canTxtObj.GetComponent<RectTransform>());
            CreateTMP(canTxtObj, "Cancelar", 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Botão Confirmar
            var confirmBtnObj = CreateUiElement("Btn_Confirm", actionRow.transform);
            var cnfRt = confirmBtnObj.GetComponent<RectTransform>();
            cnfRt.anchorMin = new Vector2(0.5f, 0.5f);
            cnfRt.anchorMax = new Vector2(0.5f, 0.5f);
            cnfRt.pivot = new Vector2(0.5f, 0.5f);
            cnfRt.anchoredPosition = new Vector2(110f, 0f);
            cnfRt.sizeDelta = new Vector2(210f, 48f);
            var cnfImg = confirmBtnObj.AddComponent<Image>();
            cnfImg.color = new Color(0.16f, 0.68f, 0.38f, 1f);
            var confirmBtn = confirmBtnObj.AddComponent<Button>();

            var cnfTxtObj = CreateUiElement("Text_Label", confirmBtnObj.transform);
            StretchFull(cnfTxtObj.GetComponent<RectTransform>());
            CreateTMP(cnfTxtObj, "Confirmar", 16f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Serializa no DeployArmiesDialogUI
            var dSo = new SerializedObject(deployUI);
            dSo.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            dSo.FindProperty("titleText").objectReferenceValue = titleText;
            dSo.FindProperty("territoryInfoText").objectReferenceValue = territoryInfoText;
            dSo.FindProperty("availableArmiesText").objectReferenceValue = availableArmiesText;
            dSo.FindProperty("countText").objectReferenceValue = countText;
            dSo.FindProperty("territoryColorBadge").objectReferenceValue = colorBadge;
            dSo.FindProperty("decreaseButton").objectReferenceValue = decreaseBtn;
            dSo.FindProperty("increaseButton").objectReferenceValue = increaseBtn;
            dSo.FindProperty("maxButton").objectReferenceValue = maxBtn;
            dSo.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            dSo.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            dSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            dSo.ApplyModifiedProperties();

            // Começa desativado/fechado
            modalRoot.SetActive(false);

            return modalRoot;
        }

        private static GameObject CreateUiElement(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateTMP(
            GameObject obj,
            string initialText,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles fontStyle = FontStyles.Normal)
        {
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = initialText;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = fontStyle;
            return tmp;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }
        #endregion
    }
}

