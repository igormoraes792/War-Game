#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using War.Core.Data;
using War.Presentation.Board;
using War.Presentation.CameraControl;
using War.Presentation.Game;

namespace War.Presentation.Editor
{
    /// <summary>
    /// Ferramenta de Editor para gerar toda a hierarquia 3D inicial da mesa tática de WAR.
    /// Cria a Mesa, os 42 territórios clássicos posicionados no mapa, Câmera Tática, Iluminação e GameController.
    /// </summary>
    public static class BoardSetupTool
    {
        [MenuItem("War Game/Gerar Tabuleiro 3D Básico na Cena")]
        public static void GenerateTacticalBoard()
        {
            const string rootName = "War_Mesa_Tatica";

            // Remove montagem anterior caso já exista para evitar duplicatas
            var existing = GameObject.Find(rootName);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Substituir Tabuleiro Existente?",
                    $"Um objeto '{rootName}' já existe na cena aberta. Deseja substituí-lo?",
                    "Substituir", "Cancelar"))
                {
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            var rootObj = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(rootObj, "Gerar Tabuleiro 3D Básico");

            // 1. Criação da Mesa de Madeira
            var tableObj = CreateWoodTable(rootObj.transform);

            // 2. Criação do Tabuleiro e dos 42 territórios
            var boardObj = new GameObject("Tabuleiro");
            boardObj.transform.SetParent(rootObj.transform, false);
            var boardView = boardObj.AddComponent<BoardView>();

            var territoryViews = CreateTerritories(boardObj.transform);

            // 3. Criação da Câmera Tática
            var cameraObj = SetupTacticalCamera(rootObj.transform);

            // 4. Criação da Iluminação Direcional com Sombras
            SetupDirectionalLight(rootObj.transform);

            // 5. Criação do GameController (Presenter + View)
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

            // Seleciona o root na Hierarchy e foca a SceneView
            Selection.activeGameObject = rootObj;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[War Setup] Tabuleiro 3D básico gerado com sucesso com os 42 territórios e Câmera Tática!");
        }

        private static GameObject CreateWoodTable(Transform parent)
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Mesa_Madeira";
            table.transform.SetParent(parent, false);
            table.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            table.transform.localScale = new Vector3(56f, 1.2f, 36f);

            var renderer = table.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var woodShader = GetSafeLitShader();
                var mat = new Material(woodShader)
                {
                    name = "Mesa_Madeira_Mat",
                    color = new Color(0.25f, 0.16f, 0.10f, 1f) // Tom escuro amadeirado
                };
                renderer.sharedMaterial = mat;
            }

            return table;
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
                color = new Color(0.72f, 0.72f, 0.75f, 1f)
            };

            foreach (var def in defs)
            {
                positions.TryGetValue(def.Id, out var coord);

                var terrObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                terrObj.name = $"Territory_{def.Name} ({def.Id})";
                terrObj.transform.SetParent(boardTransform, false);
                terrObj.transform.localPosition = new Vector3(coord.x, 0.15f, coord.y);
                terrObj.transform.localScale = new Vector3(2.4f, 0.25f, 2.4f);

                var meshRenderer = terrObj.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = defaultTerritoryMat;
                }

                var terrView = terrObj.AddComponent<TerritoryView>();
                terrView.TerritoryId = def.Id;

                // Cria o ponto de ancoragem para tropas
                var spawnPoint = new GameObject("ArmySpawnPoint");
                spawnPoint.transform.SetParent(terrObj.transform, false);
                spawnPoint.transform.localPosition = new Vector3(0f, 0.6f, 0f);

                // Cria o contador de tropas (Texto 3D com TextMeshPro)
                var textObj = new GameObject("Text_ArmyCount");
                textObj.transform.SetParent(terrObj.transform, false);
                textObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                textObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Virado para cima para visão superior da câmera

                var tmp = textObj.AddComponent<TextMeshPro>();
                tmp.text = "1";
                tmp.fontSize = 7f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                var rectTransform = textObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(4f, 4f);
                }

                // Cria o rótulo do nome do território
                var nameObj = new GameObject("Text_Name");
                nameObj.transform.SetParent(terrObj.transform, false);
                nameObj.transform.localPosition = new Vector3(0f, 0.75f, -1.1f);
                nameObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var tmpName = nameObj.AddComponent<TextMeshPro>();
                tmpName.text = def.Name;
                tmpName.fontSize = 3.5f;
                tmpName.alignment = TextAlignmentOptions.Center;
                tmpName.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
                var rectName = nameObj.GetComponent<RectTransform>();
                if (rectName != null)
                {
                    rectName.sizeDelta = new Vector2(6f, 2f);
                }

                // Serializa os campos privados no TerritoryView
                var viewSo = new SerializedObject(terrView);
                viewSo.FindProperty("territoryId").stringValue = def.Id;
                viewSo.FindProperty("territoryRenderer").objectReferenceValue = meshRenderer;
                viewSo.FindProperty("armySpawnPoint").objectReferenceValue = spawnPoint.transform;
                viewSo.FindProperty("armyCountText").objectReferenceValue = tmp;
                viewSo.FindProperty("territoryNameText").objectReferenceValue = tmpName;
                viewSo.ApplyModifiedProperties();

                views.Add(terrView);
            }

            return views;
        }

        private static GameObject SetupTacticalCamera(Transform parent)
        {
            var cam = Camera.main;
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

            cameraObj.transform.SetParent(parent, false);

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
            lightObj.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            lightComponent.color = new Color(1f, 0.97f, 0.92f);
            lightComponent.intensity = 1.25f;
            lightComponent.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// Mapeamento de coordenadas (X, Z) proporcionais para os 42 territórios clássicos no plano da mesa.
        /// </summary>
        private static Dictionary<string, Vector2> GetTerritoryWorldCoordinates()
        {
            return new Dictionary<string, Vector2>(System.StringComparer.OrdinalIgnoreCase)
            {
                // América do Norte (9)
                [TerritoryIds.Alasca] = new Vector2(-23f, 13f),
                [TerritoryIds.Mackenzie] = new Vector2(-18f, 13f),
                [TerritoryIds.Groenlandia] = new Vector2(-11f, 14f),
                [TerritoryIds.Vancouver] = new Vector2(-20f, 9.5f),
                [TerritoryIds.Ottawa] = new Vector2(-16f, 9.5f),
                [TerritoryIds.Labrador] = new Vector2(-12f, 9.5f),
                [TerritoryIds.California] = new Vector2(-20f, 5.5f),
                [TerritoryIds.NovaYork] = new Vector2(-15.5f, 5.5f),
                [TerritoryIds.Mexico] = new Vector2(-17.5f, 1.5f),

                // América do Sul (4)
                [TerritoryIds.Venezuela] = new Vector2(-14.5f, -2.5f),
                [TerritoryIds.Peru] = new Vector2(-15.5f, -6.5f),
                [TerritoryIds.Brasil] = new Vector2(-11f, -5.5f),
                [TerritoryIds.Argentina] = new Vector2(-13.5f, -11.5f),

                // Europa (7)
                [TerritoryIds.Islandia] = new Vector2(-6.5f, 12.5f),
                [TerritoryIds.Inglaterra] = new Vector2(-5.5f, 9f),
                [TerritoryIds.Franca] = new Vector2(-4.5f, 5.5f),
                [TerritoryIds.Alemanha] = new Vector2(-0.5f, 7f),
                [TerritoryIds.Suecia] = new Vector2(0.5f, 12f),
                [TerritoryIds.Polonia] = new Vector2(3.5f, 7.5f),
                [TerritoryIds.Moscou] = new Vector2(5.5f, 11f),

                // África (6)
                [TerritoryIds.Nigeria] = new Vector2(-3.5f, -0.5f),
                [TerritoryIds.Egito] = new Vector2(1.5f, 0.5f),
                [TerritoryIds.Sudao] = new Vector2(2f, -3.5f),
                [TerritoryIds.Congo] = new Vector2(-0.5f, -6.5f),
                [TerritoryIds.AfricaDoSul] = new Vector2(0.5f, -11f),
                [TerritoryIds.Madagascar] = new Vector2(5f, -10f),

                // Ásia (12)
                [TerritoryIds.OrienteMedio] = new Vector2(6f, 2f),
                [TerritoryIds.Aral] = new Vector2(9.5f, 7.5f),
                [TerritoryIds.Omsk] = new Vector2(10.5f, 12f),
                [TerritoryIds.Dudinka] = new Vector2(14.5f, 13f),
                [TerritoryIds.Siberia] = new Vector2(18.5f, 13.5f),
                [TerritoryIds.Tchita] = new Vector2(18f, 10f),
                [TerritoryIds.Mongolia] = new Vector2(16f, 7f),
                [TerritoryIds.China] = new Vector2(14f, 3.5f),
                [TerritoryIds.India] = new Vector2(10.5f, 0.5f),
                [TerritoryIds.Vietna] = new Vector2(15.5f, -0.5f),
                [TerritoryIds.Vladivostok] = new Vector2(22f, 9.5f),
                [TerritoryIds.Japao] = new Vector2(23f, 4.5f),

                // Oceania (4)
                [TerritoryIds.Sumatra] = new Vector2(16.5f, -4.5f),
                [TerritoryIds.Borneo] = new Vector2(20.5f, -3.5f),
                [TerritoryIds.NovaGuine] = new Vector2(23f, -6.5f),
                [TerritoryIds.Australia] = new Vector2(20.5f, -10.5f)
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
    }
}

