#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using War.Core.Data;
using War.Core.Models;
using War2D.Presentation.Board;
using War2D.Presentation.CameraControl;
using War.Presentation.Game;
using War.Presentation.UI;

namespace War2D.Presentation.Editor
{
    /// <summary>
    /// Ferramenta de Editor para gerar toda a hierarquia 2D do mapa tático de WAR.
    /// Cria a câmera ortográfica 2D, fundo náutico em alta resolução, 42 territórios planos com PolygonCollider2D,
    /// fichas militares 2D clássicas, linhas de conexão e HUD completo.
    /// </summary>
    [InitializeOnLoad]
    public static class BoardSetup2DTool
    {
        static BoardSetup2DTool()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += EnsureValidBoardInScene;
        }

        private static void EnsureValidBoardInScene()
        {
            var existingBoard = Object.FindAnyObjectByType<Board2DView>();
            if (existingBoard == null)
            {
                Generate2DBoardScene();
                return;
            }

            var ocean = GameObject.Find("Oceano_Mapa2D");
            if (ocean != null)
            {
                var rend = ocean.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    var mat = rend.sharedMaterial;
                    bool isPink = mat == null || mat.shader == null || !mat.shader.isSupported ||
                        (mat.shader.name.StartsWith("Universal Render Pipeline") && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null);

                    if (isPink)
                    {
                        Generate2DBoardScene();
                    }
                }
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                var existingBoard = Object.FindAnyObjectByType<Board2DView>();
                if (existingBoard == null)
                {
                    Generate2DBoardScene();
                }
            }
        }

        [MenuItem("War Game 2D/Gerar Tabuleiro 2D Completo na Cena", false, 10)]
        public static void Generate2DBoardScene()
        {
            var existingRoot = GameObject.Find("War2D_Root");
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }

            var rootObj = new GameObject("War2D_Root");
            Undo.RegisterCreatedObjectUndo(rootObj, "Gerar Tabuleiro 2D WAR");

            // 1. Câmera Ortográfica 2D
            var cameraObj = SetupCamera2D(rootObj.transform);

            // 2. Fundo Oceânico 2D de Alta Resolução
            var backgroundObj = SetupBackground2D(rootObj.transform);

            // 3. Tabuleiro 2D e Territórios
            var boardObj = new GameObject("Tabuleiro2D");
            boardObj.transform.SetParent(rootObj.transform, false);
            var boardView = boardObj.AddComponent<Board2DView>();

            var positions2D = GetTerritory2DCoordinates();
            var territoryViews = Create2DTerritories(boardObj.transform, positions2D);

            // 4. Linhas de Conexão Táticas 2D
            var linesObj = new GameObject("Conexoes_Taticas2D");
            linesObj.transform.SetParent(boardObj.transform, false);
            var linesRenderer = linesObj.AddComponent<ConnectionLines2DRenderer>();
            linesRenderer.BuildConnections(positions2D);

            var boardViewSo = new SerializedObject(boardView);
            var linesProp = boardViewSo.FindProperty("connectionLinesRenderer");
            if (linesProp != null)
            {
                linesProp.objectReferenceValue = linesRenderer;
                boardViewSo.ApplyModifiedProperties();
            }

            // 5. Inscrições dos 6 Continentes nos Oceanos
            SetupContinentPlaques(boardObj.transform, boardView);

            // 6. GameController (Presenter 2D)
            var controllerObj = new GameObject("GameController2D");
            controllerObj.transform.SetParent(rootObj.transform, false);
            var presenter = controllerObj.AddComponent<GamePresenter>();

            var presenterSo = new SerializedObject(presenter);
            var bvProp = presenterSo.FindProperty("boardView");
            if (bvProp != null)
            {
                bvProp.objectReferenceValue = boardView;
                presenterSo.ApplyModifiedProperties();
            }

            boardView.ScanAndIndexTerritories();

            // 7. War_Canvas 2D (HUD, Combate, Deploy, Missão)
            SetupGameCanvas(presenter);

            Selection.activeGameObject = rootObj;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[War 2D] Tabuleiro 2D tático gerado com sucesso: 42 Territórios Planos, Fichas Militares, Câmera Ortográfica e HUD!");
        }

        private static GameObject SetupCamera2D(Transform parent)
        {
            var cam = Camera.main;
            GameObject cameraObj;

            if (cam == null)
            {
                cameraObj = new GameObject("Main Camera");
                cameraObj.tag = "MainCamera";
                cam = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
            }
            else
            {
                cameraObj = cam.gameObject;
            }

            cameraObj.transform.SetParent(parent, false);
            cameraObj.transform.position = new Vector3(0f, 0f, -10f);
            cameraObj.transform.rotation = Quaternion.identity;

            cam.orthographic = true;
            cam.orthographicSize = 13.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.09f, 0.14f, 1f);

            var controller = cameraObj.GetComponent<CameraController2D>() ?? cameraObj.AddComponent<CameraController2D>();
            return cameraObj;
        }

        private static GameObject SetupBackground2D(Transform parent)
        {
            var bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgObj.name = "Oceano_Mapa2D";
            bgObj.transform.SetParent(parent, false);
            bgObj.transform.localPosition = new Vector3(0f, 0f, 0.1f); // Atrás dos territórios
            bgObj.transform.localScale = new Vector3(54f, 32f, 1f);

            var col = bgObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var graphic = bgObj.AddComponent<MapBoard2DGraphic>();
            var tex = graphic.GenerateMapTexture();

            var unlitShader = ShaderUtils2D.GetTextureShader();
            var mat = new Material(unlitShader)
            {
                name = "Mat_Ocean_2D",
                mainTexture = tex
            };

            var rend = bgObj.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            return bgObj;
        }

        private static List<Territory2DView> Create2DTerritories(Transform boardTransform, Dictionary<string, Vector2> positions)
        {
            var views = new List<Territory2DView>();
            var defs = ClassicMapData.GetAllDefinitions();

            var safeShader = ShaderUtils2D.GetColorShader();
            var defaultTerritoryMat = new Material(safeShader)
            {
                name = "Territorio2D_Base_Mat",
                color = new Color(0.72f, 0.74f, 0.78f, 1f),
                enableInstancing = true
            };

            var sdfFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // Materiais das fichas militares circulares
            var rimMat = new Material(safeShader) { name = "Ficha2D_Rim_Mat", color = Color.white };
            var badgeMat = new Material(safeShader) { name = "Ficha2D_Core_Mat", color = new Color(0.08f, 0.11f, 0.16f, 1f) };

            var rimCircleMesh = Territory2DMeshBuilder.CreateCircleMesh(0.68f);
            var badgeCircleMesh = Territory2DMeshBuilder.CreateCircleMesh(0.55f);

            foreach (var def in defs)
            {
                positions.TryGetValue(def.Id, out var coord);

                var terrObj = new GameObject($"Territory2D_{def.Name} ({def.Id})");
                terrObj.transform.SetParent(boardTransform, false);
                terrObj.transform.localPosition = new Vector3(coord.x, coord.y, 0f);

                // 1. Malha 2D Plana do País
                var meshFilter = terrObj.AddComponent<MeshFilter>();
                var territoryMesh = Territory2DMeshBuilder.CreateTerritory2DMesh(def.Id);
                meshFilter.sharedMesh = territoryMesh;

                var meshRenderer = terrObj.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = defaultTerritoryMat;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                // 2. Colisor 2D preciso (PolygonCollider2D)
                var poly = Territory2DMeshBuilder.GetTerritoryPolygon(def.Id);
                var polyCollider = terrObj.AddComponent<PolygonCollider2D>();
                polyCollider.points = poly;

                // 3. Contorno/Borda de Fronteira
                var borderObj = new GameObject("Border_Outline");
                borderObj.transform.SetParent(terrObj.transform, false);
                var lr = borderObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.positionCount = poly.Length;
                for (int i = 0; i < poly.Length; i++)
                {
                    lr.SetPosition(i, new Vector3(poly[i].x, poly[i].y, -0.02f));
                }
                lr.startWidth = 0.055f;
                lr.endWidth = 0.055f;
                lr.sharedMaterial = defaultTerritoryMat;
                lr.startColor = new Color(0.12f, 0.15f, 0.20f, 0.95f);
                lr.endColor = lr.startColor;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                var terrView = terrObj.AddComponent<Territory2DView>();
                terrView.TerritoryId = def.Id;

                var centroid = Territory2DMeshBuilder.GetPolygonCentroid(poly);

                // 4. Ficha Militar Clássica de Tropa (Token Circular 2D em Z = -0.05f)
                var tokenRoot = new GameObject("Token_Root");
                tokenRoot.transform.SetParent(terrObj.transform, false);
                tokenRoot.transform.localPosition = new Vector3(centroid.x, centroid.y + 0.35f, -0.06f);

                // 4.1 Aro exterior circular (cor do jogador)
                var rimObj = new GameObject("Token_Rim");
                rimObj.transform.SetParent(tokenRoot.transform, false);
                var rimMf = rimObj.AddComponent<MeshFilter>();
                rimMf.sharedMesh = rimCircleMesh;
                var rimRend = rimObj.AddComponent<MeshRenderer>();
                rimRend.sharedMaterial = rimMat;
                rimRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rimRend.receiveShadows = false;

                // 4.2 Núcleo central circular escuro (para contraste absoluto)
                var badgeObj = new GameObject("Token_Badge");
                badgeObj.transform.SetParent(tokenRoot.transform, false);
                badgeObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                var badgeMf = badgeObj.AddComponent<MeshFilter>();
                badgeMf.sharedMesh = badgeCircleMesh;
                var badgeRend = badgeObj.AddComponent<MeshRenderer>();
                badgeRend.sharedMaterial = badgeMat;
                badgeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                badgeRend.receiveShadows = false;

                // 4.3 Contador de tropas (TextMeshPro SDF Branco Puro)
                var countTextObj = new GameObject("Text_Count");
                countTextObj.transform.SetParent(tokenRoot.transform, false);
                countTextObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                var countTmp = countTextObj.AddComponent<TextMeshPro>();
                countTmp.text = "1";
                countTmp.fontSize = 8.5f;
                countTmp.fontStyle = FontStyles.Bold;
                countTmp.alignment = TextAlignmentOptions.Center;
                countTmp.color = Color.white;
                countTmp.textWrappingMode = TextWrappingModes.NoWrap;
                if (sdfFont != null) countTmp.font = sdfFont;
                var countRt = countTextObj.GetComponent<RectTransform>();
                if (countRt != null) countRt.sizeDelta = new Vector2(2.5f, 2.5f);

                // 5. Nome do País em TextMeshPro SDF (logo abaixo da ficha)
                var nameTagRoot = new GameObject("NameTag_Root");
                nameTagRoot.transform.SetParent(terrObj.transform, false);
                nameTagRoot.transform.localPosition = new Vector3(centroid.x, centroid.y - 0.95f, -0.04f);

                var nameTextObj = new GameObject("Text_Name");
                nameTextObj.transform.SetParent(nameTagRoot.transform, false);
                nameTextObj.transform.localPosition = Vector3.zero;

                var nameTmp = nameTextObj.AddComponent<TextMeshPro>();
                nameTmp.text = def.Name.ToUpperInvariant();
                nameTmp.fontSize = 4.2f;
                nameTmp.fontStyle = FontStyles.Bold;
                nameTmp.alignment = TextAlignmentOptions.Center;
                nameTmp.color = Color.white;
                nameTmp.outlineWidth = 0.32f;
                nameTmp.outlineColor = Color.black;
                nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
                nameTmp.enableAutoSizing = true;
                nameTmp.fontSizeMin = 2.4f;
                nameTmp.fontSizeMax = 4.4f;
                if (sdfFont != null) nameTmp.font = sdfFont;
                var nameRt = nameTextObj.GetComponent<RectTransform>();
                if (nameRt != null) nameRt.sizeDelta = new Vector2(7.5f, 2.0f);

                // Serialização de campos
                var viewSo = new SerializedObject(terrView);
                viewSo.FindProperty("territoryId").stringValue = def.Id;
                viewSo.FindProperty("territoryRenderer").objectReferenceValue = meshRenderer;
                viewSo.FindProperty("borderOutlineRenderer").objectReferenceValue = lr;
                viewSo.FindProperty("polygonCollider").objectReferenceValue = polyCollider;
                viewSo.FindProperty("pinTokenRoot").objectReferenceValue = tokenRoot.transform;
                viewSo.FindProperty("armyCountText").objectReferenceValue = countTmp;
                viewSo.FindProperty("territoryNameText").objectReferenceValue = nameTmp;
                viewSo.FindProperty("nameTagRoot").objectReferenceValue = nameTagRoot;
                viewSo.ApplyModifiedProperties();

                views.Add(terrView);
            }

            return views;
        }

        private static void SetupContinentPlaques(Transform boardTransform, Board2DView boardView)
        {
            var plaquesRoot = new GameObject("Cartelas_Continentes2D");
            plaquesRoot.transform.SetParent(boardTransform, false);

            var sdfFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var continents = new (string Name, ContinentType Type, int Bonus, Color Color, Vector2 Pos)[]
            {
                ("AMÉRICA DO NORTE", ContinentType.NorthAmerica, 5, new Color(0.95f, 0.48f, 0.15f), new Vector2(-23.5f, 13.5f)),
                ("AMÉRICA DO SUL", ContinentType.SouthAmerica, 2, new Color(0.35f, 0.85f, 0.28f), new Vector2(-22.0f, -9.5f)),
                ("EUROPA", ContinentType.Europe, 5, new Color(0.20f, 0.80f, 0.98f), new Vector2(-1.5f, 14.5f)),
                ("ÁFRICA", ContinentType.Africa, 3, new Color(0.92f, 0.28f, 0.65f), new Vector2(2.5f, -14.2f)),
                ("ÁSIA", ContinentType.Asia, 7, new Color(0.98f, 0.78f, 0.15f), new Vector2(22.5f, 14.2f)),
                ("OCEANIA", ContinentType.Oceania, 2, new Color(0.95f, 0.28f, 0.28f), new Vector2(22.5f, -13.5f))
            };

            foreach (var c in continents)
            {
                var plqObj = new GameObject($"Cartela2D_{c.Name}");
                plqObj.transform.SetParent(plaquesRoot.transform, false);
                plqObj.transform.localPosition = new Vector3(c.Pos.x, c.Pos.y, -0.05f);

                // Texto do Nome do Continente
                var nameObj = new GameObject("Text_Title");
                nameObj.transform.SetParent(plqObj.transform, false);
                nameObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                var nameTmp = nameObj.AddComponent<TextMeshPro>();
                nameTmp.text = c.Name;
                nameTmp.fontSize = 3.6f;
                nameTmp.fontStyle = FontStyles.Bold;
                nameTmp.alignment = TextAlignmentOptions.Center;
                nameTmp.color = c.Color;
                nameTmp.outlineWidth = 0.25f;
                nameTmp.outlineColor = Color.black;
                if (sdfFont != null) nameTmp.font = sdfFont;
                var nRt = nameObj.GetComponent<RectTransform>();
                if (nRt != null) nRt.sizeDelta = new Vector2(8.5f, 1.2f);

                // Texto do Bônus de Tropas
                var bonusObj = new GameObject("Text_Bonus");
                bonusObj.transform.SetParent(plqObj.transform, false);
                bonusObj.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                var bonusTmp = bonusObj.AddComponent<TextMeshPro>();
                bonusTmp.text = $"+{c.Bonus} EXÉRCITOS / RODADA";
                bonusTmp.fontSize = 2.4f;
                bonusTmp.fontStyle = FontStyles.Bold;
                bonusTmp.alignment = TextAlignmentOptions.Center;
                bonusTmp.color = new Color(0.90f, 0.94f, 1f, 0.95f);
                bonusTmp.outlineWidth = 0.20f;
                bonusTmp.outlineColor = Color.black;
                if (sdfFont != null) bonusTmp.font = sdfFont;
                var bRt = bonusObj.GetComponent<RectTransform>();
                if (bRt != null) bRt.sizeDelta = new Vector2(8.5f, 1.0f);
            }
        }

        private static Dictionary<string, Vector2> GetTerritory2DCoordinates()
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

        #region Criação do War_Canvas 2D
        public static GameObject SetupGameCanvas(GamePresenter? presenter)
        {
            const string canvasName = "War2D_Canvas";
            var existing = GameObject.Find(canvasName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var canvasObj = new GameObject(canvasName);
            Undo.RegisterCreatedObjectUndo(canvasObj, "Gerar War2D_Canvas");

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            // HUD Root
            var hudRoot = CreateUiElement("HUD_Root", canvasObj.transform);
            StretchFull(hudRoot.GetComponent<RectTransform>());
            var gameHud = hudRoot.AddComponent<GameHUD>();

            // Top Panel
            var topPanel = CreateUiElement("HUD_TopPanel", hudRoot.transform);
            var topRt = topPanel.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.sizeDelta = new Vector2(0f, 85f);
            var topImg = topPanel.AddComponent<Image>();
            topImg.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

            var topLayout = topPanel.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(25, 25, 12, 12);
            topLayout.spacing = 20;
            topLayout.childAlignment = TextAnchor.MiddleCenter;
            topLayout.childControlWidth = false;
            topLayout.childControlHeight = true;

            // Player Badge & Name
            var playerBadgeObj = CreateUiElement("Player_Badge", topPanel.transform);
            var playerBadgeImg = playerBadgeObj.AddComponent<Image>();
            playerBadgeImg.color = new Color(0.92f, 0.22f, 0.22f, 1f);
            playerBadgeObj.GetComponent<RectTransform>().sizeDelta = new Vector2(32f, 32f);

            var playerNameObj = CreateUiElement("Text_PlayerName", topPanel.transform);
            playerNameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 40f);
            var playerNameTmp = CreateTMP(playerNameObj, "General Vermelho", 22, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

            // Phase Name
            var phaseObj = CreateUiElement("Text_Phase", topPanel.transform);
            phaseObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 40f);
            var phaseTmp = CreateTMP(phaseObj, "FASE: REFORÇO", 20, new Color(1f, 0.88f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Armies to Deploy
            var armiesObj = CreateUiElement("Text_ArmiesToDeploy", topPanel.transform);
            armiesObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 40f);
            var armiesTmp = CreateTMP(armiesObj, "Tropas para posicionar: 5", 19, new Color(0.40f, 0.90f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Advance Phase Button
            var advanceBtnObj = CreateUiElement("Btn_AdvancePhase", topPanel.transform);
            advanceBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(230f, 50f);
            var advanceBtnImg = advanceBtnObj.AddComponent<Image>();
            advanceBtnImg.color = new Color(0.18f, 0.55f, 0.32f, 1f);
            var advanceBtn = advanceBtnObj.AddComponent<Button>();

            var advanceBtnTextObj = CreateUiElement("Text_Btn", advanceBtnObj.transform);
            StretchFull(advanceBtnTextObj.GetComponent<RectTransform>());
            var advanceBtnTmp = CreateTMP(advanceBtnTextObj, "AVANÇAR FASE ▸", 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Mission Button
            var missionBtnObj = CreateUiElement("Btn_Mission", topPanel.transform);
            missionBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 50f);
            var missionBtnImg = missionBtnObj.AddComponent<Image>();
            missionBtnImg.color = new Color(0.24f, 0.34f, 0.48f, 1f);
            var missionBtn = missionBtnObj.AddComponent<Button>();

            var missionBtnTextObj = CreateUiElement("Text_Btn", missionBtnObj.transform);
            StretchFull(missionBtnTextObj.GetComponent<RectTransform>());
            var missionBtnTmp = CreateTMP(missionBtnTextObj, "MINHA MISSÃO", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Status Bar
            var statusPanel = CreateUiElement("HUD_StatusBar", hudRoot.transform);
            var statusRt = statusPanel.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 0f);
            statusRt.anchorMax = new Vector2(0.5f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.sizeDelta = new Vector2(850f, 48f);
            statusRt.anchoredPosition = new Vector2(0f, 15f);
            var statusImg = statusPanel.AddComponent<Image>();
            statusImg.color = new Color(0.08f, 0.11f, 0.16f, 0.92f);

            var statusTextObj = CreateUiElement("Text_Status", statusPanel.transform);
            StretchFull(statusTextObj.GetComponent<RectTransform>());
            var statusTmp = CreateTMP(statusTextObj, "Partida 2D iniciada! Selecione seu território para reforçar.", 17, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            var hudSo = new SerializedObject(gameHud);
            hudSo.FindProperty("playerColorBadge").objectReferenceValue = playerBadgeImg;
            hudSo.FindProperty("playerNameText").objectReferenceValue = playerNameTmp;
            hudSo.FindProperty("phaseNameText").objectReferenceValue = phaseTmp;
            hudSo.FindProperty("armiesToDeployText").objectReferenceValue = armiesTmp;
            hudSo.FindProperty("advancePhaseButton").objectReferenceValue = advanceBtn;
            hudSo.FindProperty("advancePhaseButtonText").objectReferenceValue = advanceBtnTmp;
            hudSo.FindProperty("myMissionButton").objectReferenceValue = missionBtn;
            hudSo.FindProperty("statusText").objectReferenceValue = statusTmp;
            hudSo.FindProperty("statusBarRoot").objectReferenceValue = statusPanel;
            hudSo.ApplyModifiedProperties();

            // CombatPanelUI, ObjectiveModalUI e DeployArmiesDialogUI
            var combatPanel = SetupCombatPanel(canvasObj.transform);
            var objectiveModal = SetupObjectiveModal(canvasObj.transform);
            var deployDialog = SetupDeployDialog(canvasObj.transform);

            if (presenter != null)
            {
                var presSo = new SerializedObject(presenter);
                presSo.FindProperty("gameHud").objectReferenceValue = gameHud;
                presSo.FindProperty("combatPanel").objectReferenceValue = combatPanel;
                presSo.FindProperty("objectiveModal").objectReferenceValue = objectiveModal;
                presSo.FindProperty("deployArmiesDialog").objectReferenceValue = deployDialog;
                presSo.ApplyModifiedProperties();
            }

            return canvasObj;
        }

        private static CombatPanelUI SetupCombatPanel(Transform canvasTransform)
        {
            var modalRoot = CreateUiElement("Combat_ModalRoot", canvasTransform);
            StretchFull(modalRoot.GetComponent<RectTransform>());
            var modalImg = modalRoot.AddComponent<Image>();
            modalImg.color = new Color(0f, 0f, 0f, 0.70f);

            var box = CreateUiElement("Combat_Box", modalRoot.transform);
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(720f, 480f);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.14f, 0.20f, 0.98f);

            var combat = modalRoot.AddComponent<CombatPanelUI>();
            modalRoot.SetActive(false);
            return combat;
        }

        private static ObjectiveModalUI SetupObjectiveModal(Transform canvasTransform)
        {
            var modalRoot = CreateUiElement("Objective_ModalRoot", canvasTransform);
            StretchFull(modalRoot.GetComponent<RectTransform>());
            var modalImg = modalRoot.AddComponent<Image>();
            modalImg.color = new Color(0f, 0f, 0f, 0.70f);

            var box = CreateUiElement("Objective_Box", modalRoot.transform);
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(620f, 400f);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.14f, 0.20f, 0.98f);

            var modal = modalRoot.AddComponent<ObjectiveModalUI>();
            modalRoot.SetActive(false);
            return modal;
        }

        private static DeployArmiesDialogUI SetupDeployDialog(Transform canvasTransform)
        {
            var modalRoot = CreateUiElement("Deploy_ModalRoot", canvasTransform);
            StretchFull(modalRoot.GetComponent<RectTransform>());
            var modalImg = modalRoot.AddComponent<Image>();
            modalImg.color = new Color(0f, 0f, 0f, 0.55f);

            var box = CreateUiElement("Deploy_Box", modalRoot.transform);
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(500f, 320f);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.10f, 0.14f, 0.20f, 0.98f);

            var dialog = modalRoot.AddComponent<DeployArmiesDialogUI>();
            modalRoot.SetActive(false);
            return dialog;
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

        private static TextMeshProUGUI CreateTMP(GameObject obj, string text, float size, Color col, TextAlignmentOptions align, FontStyles style)
        {
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = col;
            tmp.alignment = align;
            tmp.fontStyle = style;
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
