using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GTA2Clone.EditorTools
{
    /// <summary>
    /// Gerador de mapa da Lapa (Rio de Janeiro) para a SampleScene utilizando os pacotes modulares Roads e City-kit.
    /// </summary>
    public static class LapaMapGenerator
    {
        private const float TileSize = 10f;
        private const float RoadScaleY = 3f;
        private const float BuildingScale = 8.5f;
        private const float PropScale = 8f;

        private const string RoadsFbxFolder = "Assets/Models/Roads/Models/FBX format/";
        private const string CityFbxFolder = "Assets/Models/City-kit/Models/FBX format/";

        private static readonly string[] StandardBuildings =
        {
            "building-type-a.fbx", "building-type-b.fbx", "building-type-c.fbx",
            "building-type-e.fbx", "building-type-f.fbx", "building-type-g.fbx",
            "building-type-h.fbx", "building-type-i.fbx", "building-type-j.fbx",
            "building-type-k.fbx", "building-type-l.fbx", "building-type-m.fbx",
            "building-type-o.fbx", "building-type-p.fbx", "building-type-q.fbx",
            "building-type-r.fbx", "building-type-s.fbx", "building-type-u.fbx"
        };

        [MenuItem("Tools/GTA2/Build Lapa Map")]
        public static void BuildLapaMapMenu()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            BuildLapaMap(urpLit);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[LapaMapGenerator] Mapa da Lapa construído com sucesso na SampleScene!");
        }

        /// <summary>
        /// Constrói toda a malha viária, pontos turísticos, quarteirões e iluminação do bairro da Lapa.
        /// </summary>
        public static void BuildLapaMap(Shader shader)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Material groundMat = CreateOrUpdateMaterial(
                "Assets/Materials/Ground.mat",
                null,
                new Color(0.20f, 0.22f, 0.24f, 1f),
                0.08f,
                shader
            );

            Material roadsMat = CreateOrUpdateMaterial(
                "Assets/Materials/RoadsColormap.mat",
                RoadsFbxFolder + "Textures/colormap.png",
                Color.white,
                0.12f,
                shader
            );

            Material cityMat = CreateOrUpdateMaterial(
                "Assets/Materials/CityColormap.mat",
                CityFbxFolder + "Textures/colormap.png",
                Color.white,
                0.15f,
                shader
            );

            // Configura o plano base Ground para cobrir todo o bairro sem costuras físicas
            GameObject existingGround = GameObject.Find("Ground");
            if (existingGround != null)
            {
                Object.DestroyImmediate(existingGround);
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(34f, 1f, 32f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // Remove instância anterior do mapa caso exista
            GameObject existingMap = GameObject.Find("LapaMap");
            if (existingMap != null)
            {
                Object.DestroyImmediate(existingMap);
            }

            GameObject mapRoot = new GameObject("LapaMap");
            Transform roadsRoot = CreateGroup(mapRoot.transform, "Roads_Network");
            Transform landmarksRoot = CreateGroup(mapRoot.transform, "Landmarks");
            Transform blocksRoot = CreateGroup(mapRoot.transform, "City_Blocks");
            Transform propsRoot = CreateGroup(mapRoot.transform, "Props_And_Streetlights");

            HashSet<Vector2Int> roadCells = BuildLapaRoadCoordinates();
            BuildRoadTiles(roadsRoot, roadCells, roadsMat);
            BuildLandmarks(landmarksRoot, roadsMat, cityMat);
            BuildCityBlocks(blocksRoot, roadCells, roadsMat, cityMat);
            BuildStreetPropsAndLights(propsRoot, roadCells, roadsMat, cityMat);
        }

        private static HashSet<Vector2Int> BuildLapaRoadCoordinates()
        {
            var cells = new HashSet<Vector2Int>();

            void AddLine(int x0, int z0, int x1, int z1)
            {
                int dx = x1 == x0 ? 0 : (x1 > x0 ? 1 : -1);
                int dz = z1 == z0 ? 0 : (z1 > z0 ? 1 : -1);
                int cx = x0;
                int cz = z0;
                cells.Add(new Vector2Int(cx, cz));
                while (cx != x1 || cz != z1)
                {
                    if (cx != x1) cx += dx;
                    else if (cz != z1) cz += dz;
                    cells.Add(new Vector2Int(cx, cz));
                }
            }

            // === Avenidas e Ruas Horizontais (Oeste -> Leste) ===
            // R. Fr. Caneca & Av. Visc. do Rio Branco (Norte)
            AddLine(-10, 9, 2, 9);
            // R. do Senado (Armazém do Senado)
            AddLine(-10, 6, 2, 6);
            // Conector Noroeste (R. Fr. Caneca <-> R. do Senado)
            AddLine(-10, 6, -10, 9);

            // Av. Henrique Valadares -> Av. República do Chile (Eixo Norte-Central)
            AddLine(-10, 3, 10, 3);

            // R. do Rezende (Oeste - Bar das Quengas)
            AddLine(-10, 0, -6, 0);
            // R. dos Arcos (Centro - Catedral & Circo Voador -> Arcos da Lapa)
            AddLine(2, 0, 6, 0);
            // R. Evaristo da Veiga (Leste)
            AddLine(6, 0, 10, 0);

            // Av. Mem de Sá (Grande Eixo Boêmio Central)
            AddLine(-10, -3, 10, -3);

            // R. Riachuelo -> R. Theotônio Regadas -> R. Teixeira de Freitas (Eixo Contínuo Sul)
            AddLine(-10, -6, 10, -6);

            // Fechamento Sul das ladeiras de Santa Teresa / Glória
            AddLine(-2, -10, 10, -10);

            // Via dos Arcos / Lapa Nordeste
            AddLine(6, 6, 10, 6);

            // === Ruas Verticais / Transversais (Norte -> Sul) ===
            // R. Ubaldino do Amaral (Oeste)
            AddLine(-10, -6, -10, 3);
            // R. dos Inválidos
            AddLine(-6, -6, -6, 9);
            // R. Gomes Freire (continua ao sul como R. Francisco Muratori)
            AddLine(-2, -10, -2, 9);
            // R. Sílvio Romero (Sul)
            AddLine(0, -10, 0, -6);
            // R. do Lavradio (continua ao sul como R. Joaquim Murtinho)
            AddLine(2, -10, 2, 9);
            // Ladeira de Santa Teresa (Sul)
            AddLine(4, -10, 4, -6);
            // Av. República do Paraguai & R. Joaquim Silva (passa pelos Arcos da Lapa e Escadaria)
            AddLine(6, -10, 6, 6);
            // R. das Marrecas (Centro-Leste) & R. da Lapa (Sudeste)
            AddLine(8, -3, 8, 0);
            AddLine(8, -10, 8, -6);
            // R. Sen. Dantas -> R. do Passeio -> R. Augusto Severo (Extremo Leste)
            AddLine(10, -10, 10, 6);

            return cells;
        }

        private static void BuildRoadTiles(Transform parent, HashSet<Vector2Int> roadCells, Material roadsMat)
        {
            GameObject straightPrefab = LoadRoadPrefab("road-straight.fbx");
            GameObject crossingPrefab = LoadRoadPrefab("road-crossing.fbx");
            GameObject bendPrefab = LoadRoadPrefab("road-bend-sidewalk.fbx");
            GameObject intersectionPrefab = LoadRoadPrefab("road-intersection-path.fbx");
            GameObject crossroadPrefab = LoadRoadPrefab("road-crossroad-path.fbx");
            GameObject endPrefab = LoadRoadPrefab("road-end-barrier.fbx");

            foreach (Vector2Int cell in roadCells)
            {
                bool n = roadCells.Contains(new Vector2Int(cell.x, cell.y + 1));
                bool s = roadCells.Contains(new Vector2Int(cell.x, cell.y - 1));
                bool e = roadCells.Contains(new Vector2Int(cell.x + 1, cell.y));
                bool w = roadCells.Contains(new Vector2Int(cell.x - 1, cell.y));

                int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);
                GameObject chosenPrefab = straightPrefab;
                float rotY = 0f;

                if (count == 4)
                {
                    chosenPrefab = crossroadPrefab;
                    rotY = 0f;
                }
                else if (count == 3)
                {
                    chosenPrefab = intersectionPrefab;
                    // No espaço importado do Unity, road-intersection-path tem meio-fio fechado ao Sul (-Z) em 0°
                    if (!s) rotY = 0f;
                    else if (!w) rotY = 90f;
                    else if (!n) rotY = 180f;
                    else rotY = 270f;
                }
                else if (count == 2)
                {
                    if ((n && s) || (e && w))
                    {
                        bool useCrossing = (Mathf.Abs(cell.x + cell.y) % 4 == 1);
                        chosenPrefab = useCrossing ? crossingPrefab : straightPrefab;
                        // road-straight conecta Leste (+X) e Oeste (-X) em 0°
                        rotY = (e && w) ? 0f : 90f;
                    }
                    else
                    {
                        chosenPrefab = bendPrefab;
                        // road-bend-sidewalk conecta Norte (+Z) e Leste (+X) em 0°
                        if (n && e) rotY = 0f;
                        else if (e && s) rotY = 90f;
                        else if (s && w) rotY = 180f;
                        else rotY = 270f;
                    }
                }
                else if (count == 1)
                {
                    chosenPrefab = endPrefab;
                    // road-end-barrier conecta Oeste (-X) em 0°
                    if (w) rotY = 0f;
                    else if (n) rotY = 90f;
                    else if (e) rotY = 180f;
                    else rotY = 270f;
                }

                Vector3 worldPos = new Vector3(cell.x * TileSize, 0.01f, cell.y * TileSize);
                InstantiateModel(
                    chosenPrefab,
                    $"Road_{cell.x}_{cell.y}",
                    parent,
                    worldPos,
                    Quaternion.Euler(0f, rotY, 0f),
                    new Vector3(TileSize, RoadScaleY, TileSize),
                    roadsMat,
                    addCollider: count == 1 // Adiciona colisor nas barreiras de fim de pista
                );
            }
        }

        private static void BuildLandmarks(Transform parent, Material roadsMat, Material cityMat)
        {
            // 1. ARCOS DA LAPA (Aqueduto da Carioca - Nordeste: gx = 7..9, gz = 4..5)
            Transform arcosGroup = CreateGroup(parent, "Landmark_ArcosDaLapa");
            CreatePavedPlaza(arcosGroup, 7, 4, 9, 5, roadsMat);
            GameObject bridgePrefab = LoadRoadPrefab("road-bridge.fbx");
            GameObject pillarWidePrefab = LoadRoadPrefab("bridge-pillar-wide.fbx");
            GameObject lightCurvedPrefab = LoadRoadPrefab("light-curved.fbx");

            // Fileira de arcos monumentais em dois níveis sobre a praça dos Arcos
            for (int gx = 7; gx <= 9; gx++)
            {
                Vector3 archPos = new Vector3(gx * TileSize, 0f, 4.5f * TileSize);
                // Primeiro nível de arcos (altura = 0.52 * 9 = 4.68m)
                InstantiateModel(bridgePrefab, $"Arcos_Nivel1_{gx}", arcosGroup,
                    archPos, Quaternion.identity, new Vector3(10f, 9f, 6.5f), roadsMat, addCollider: true);
                // Segundo nível de arcos (aqueduto superior + trilho do bonde)
                InstantiateModel(bridgePrefab, $"Arcos_Nivel2_{gx}", arcosGroup,
                    archPos + new Vector3(0f, 4.6f, 0f), Quaternion.identity, new Vector3(10f, 8.5f, 5.5f), roadsMat, addCollider: false);
                // Pilares de reforço nas junções entre os arcos
                InstantiateModel(pillarWidePrefab, $"Arcos_Coluna_{gx}", arcosGroup,
                    archPos + new Vector3(-5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(6.5f, 17.5f, 6.5f), roadsMat, addCollider: true);
            }
            InstantiateModel(pillarWidePrefab, "Arcos_Coluna_Leste", arcosGroup,
                new Vector3(9.5f * TileSize, 0f, 4.5f * TileSize), Quaternion.Euler(0f, 90f, 0f), new Vector3(6.5f, 17.5f, 6.5f), roadsMat, addCollider: true);
            // Poste clássico da Lapa ao lado dos Arcos
            InstantiateModel(lightCurvedPrefab, "Poste_ArcosLapa", arcosGroup,
                new Vector3(6.6f * TileSize, 0f, 3.8f * TileSize), Quaternion.Euler(0f, -45f, 0f), Vector3.one * 10f, roadsMat, addCollider: true);

            // 2. CATEDRAL DE SÃO SEBASTIÃO (Centro: gx = 3..5, gz = 1..2)
            Transform catedralGroup = CreateGroup(parent, "Landmark_CatedralDeSaoSebastiao");
            CreatePavedPlaza(catedralGroup, 3, 1, 5, 2, roadsMat);
            Vector3 catedralCenter = new Vector3(4f * TileSize, 0f, 1.5f * TileSize);
            // Estrutura central escalonada representando a Catedral Metropolitana
            InstantiateModel(LoadCityPrefab("building-type-t.fbx"), "Catedral_Base", catedralGroup,
                catedralCenter, Quaternion.identity, new Vector3(13f, 12f, 13f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-d.fbx"), "Catedral_TorreCentral", catedralGroup,
                catedralCenter + new Vector3(0f, 6f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(8.5f, 14f, 8.5f), cityMat, addCollider: false);
            InstantiateModel(LoadCityPrefab("building-type-k.fbx"), "Catedral_Cupula", catedralGroup,
                catedralCenter + new Vector3(0f, 13f, 0f), Quaternion.identity, new Vector3(6f, 9f, 6f), cityMat, addCollider: false);
            // Árvores e jardineiras ao redor da Catedral
            InstantiateModel(LoadCityPrefab("tree-large.fbx"), "Catedral_Tree_NW", catedralGroup,
                new Vector3(3f * TileSize, 0f, 2.2f * TileSize), Quaternion.identity, Vector3.one * 9f, cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("tree-large.fbx"), "Catedral_Tree_NE", catedralGroup,
                new Vector3(5f * TileSize, 0f, 2.2f * TileSize), Quaternion.identity, Vector3.one * 9f, cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("planter.fbx"), "Catedral_Planter_S", catedralGroup,
                new Vector3(4f * TileSize, 0f, 0.7f * TileSize), Quaternion.identity, Vector3.one * 9f, cityMat, addCollider: true);

            // 3. CIRCO VOADOR (Centro-Leste junto à R. dos Arcos: gx = 3..5, gz = -2..-1)
            Transform circoGroup = CreateGroup(parent, "Landmark_CircoVoador");
            CreatePavedPlaza(circoGroup, 3, -2, 5, -1, roadsMat);
            InstantiateModel(LoadCityPrefab("building-type-m.fbx"), "CircoVoador_LonaPrincipal", circoGroup,
                new Vector3(4f * TileSize, 0f, -1.5f * TileSize), Quaternion.identity, new Vector3(12f, 9.5f, 11f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-g.fbx"), "CircoVoador_PalcoAnexo", circoGroup,
                new Vector3(5f * TileSize, 0f, -1.5f * TileSize), Quaternion.Euler(0f, -90f, 0f), new Vector3(8.5f, 8f, 8.5f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("fence-2x3.fbx"), "CircoVoador_Cerca", circoGroup,
                new Vector3(3f * TileSize, 0f, -1.5f * TileSize), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 8f, cityMat, addCollider: true);
            InstantiateModel(LoadRoadPrefab("construction-light.fbx"), "CircoVoador_Refletor", circoGroup,
                new Vector3(3.2f * TileSize, 0f, -0.8f * TileSize), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 9f, roadsMat, addCollider: true);

            // 4. ARMAZÉM DO SENADO (Noroeste - R. do Senado: gx = -9..-7, gz = 7..8)
            Transform armazemGroup = CreateGroup(parent, "Landmark_ArmazemDoSenado");
            CreatePavedPlaza(armazemGroup, -9, 7, -7, 8, roadsMat);
            InstantiateModel(LoadCityPrefab("building-type-b.fbx"), "ArmazemDoSenado_Predio", armazemGroup,
                new Vector3(-8f * TileSize, 0f, 7.2f * TileSize), Quaternion.identity, new Vector3(10f, 9.5f, 9.5f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-f.fbx"), "ArmazemDoSenado_Esquina", armazemGroup,
                new Vector3(-6.8f * TileSize, 0f, 7.5f * TileSize), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 8.5f, cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("planter.fbx"), "Armazem_MesaCalçada", armazemGroup,
                new Vector3(-8.5f * TileSize, 0f, 6.6f * TileSize), Quaternion.identity, Vector3.one * 8f, cityMat, addCollider: true);

            // 5. BAR DAS QUENGAS (Oeste - R. do Rezende / Av. Mem de Sá: gx = -9..-7, gz = -2..-1)
            Transform quengasGroup = CreateGroup(parent, "Landmark_BarDasQuengas");
            CreatePavedPlaza(quengasGroup, -9, -2, -7, -1, roadsMat);
            InstantiateModel(LoadCityPrefab("building-type-c.fbx"), "BarDasQuengas_Sede", quengasGroup,
                new Vector3(-8.5f * TileSize, 0f, -1.5f * TileSize), Quaternion.Euler(0f, -90f, 0f), new Vector3(9.5f, 9.5f, 9.5f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-h.fbx"), "BarDasQuengas_Anexo", quengasGroup,
                new Vector3(-7f * TileSize, 0f, -1.5f * TileSize), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 8.5f, cityMat, addCollider: true);

            // 6. CARIOCA DA GEMA (Centro - Av. Mem de Sá x R. do Lavradio: gx = -1..1, gz = -5..-4)
            Transform cariocaGroup = CreateGroup(parent, "Landmark_CariocaDaGema");
            CreatePavedPlaza(cariocaGroup, -1, -5, 1, -4, roadsMat);
            InstantiateModel(LoadCityPrefab("building-type-a.fbx"), "CariocaDaGema_Casarao", cariocaGroup,
                new Vector3(0.5f * TileSize, 0f, -4f * TileSize), Quaternion.Euler(0f, 180f, 0f), new Vector3(10f, 10f, 9.5f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-e.fbx"), "CariocaDaGema_Lateral", cariocaGroup,
                new Vector3(-0.8f * TileSize, 0f, -4.5f * TileSize), Quaternion.identity, Vector3.one * 8.5f, cityMat, addCollider: true);

            // 7. LAPA 40° (Sudoeste - R. Riachuelo: gx = -5..-3, gz = -5..-4)
            Transform lapa40Group = CreateGroup(parent, "Landmark_Lapa40Graus");
            CreatePavedPlaza(lapa40Group, -5, -5, -3, -4, roadsMat);
            InstantiateModel(LoadCityPrefab("building-type-n.fbx"), "Lapa40Graus_SinucaHall", lapa40Group,
                new Vector3(-4f * TileSize, 0f, -4.8f * TileSize), Quaternion.identity, new Vector3(10f, 10f, 9.5f), cityMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-s.fbx"), "Lapa40Graus_Norte", lapa40Group,
                new Vector3(-4f * TileSize, 0f, -3.8f * TileSize), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 8.5f, cityMat, addCollider: true);

            // 8. ESCADARIA SELARÓN (Sudeste - R. Joaquim Silva / Ld. Santa Teresa: gx = 5, gz = -9..-7)
            Transform escadariaGroup = CreateGroup(parent, "Landmark_EscadariaSelaron");
            CreatePavedPlaza(escadariaGroup, 5, -9, 5, -7, roadsMat);
            // Rampas e degraus estilizados subindo para Santa Teresa
            InstantiateModel(LoadRoadPrefab("tile-slant.fbx"), "Escadaria_LanceBaixo", escadariaGroup,
                new Vector3(5f * TileSize, 0f, -7f * TileSize), Quaternion.Euler(0f, -90f, 0f), new Vector3(8f, 6f, 8f), roadsMat, addCollider: true);
            InstantiateModel(LoadRoadPrefab("tile-slantHigh.fbx"), "Escadaria_LanceAlto", escadariaGroup,
                new Vector3(5f * TileSize, 0f, -8f * TileSize), Quaternion.Euler(0f, -90f, 0f), new Vector3(8f, 6f, 8f), roadsMat, addCollider: true);
            InstantiateModel(LoadRoadPrefab("tile-high.fbx"), "Escadaria_PatamarTopo", escadariaGroup,
                new Vector3(5f * TileSize, 0f, -9f * TileSize), Quaternion.identity, new Vector3(8f, 6f, 8f), roadsMat, addCollider: true);
            InstantiateModel(LoadCityPrefab("building-type-r.fbx"), "Escadaria_AtelierSelaron", escadariaGroup,
                new Vector3(5f * TileSize, 3.0f, -9f * TileSize), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 7.5f, cityMat, addCollider: true);
        }

        private static void BuildCityBlocks(Transform parent, HashSet<Vector2Int> roadCells, Material roadsMat, Material cityMat)
        {
            // Quarteirões urbanos restantes preenchidos com calçamento e prédios variados
            var blocks = new List<(string name, int xMin, int zMin, int xMax, int zMax)>
            {
                ("Block_Senado_Valadares_W", -9, 4, -7, 5),
                ("Block_Valadares_Rezende_W", -9, 1, -7, 2),
                ("Block_MemDeSa_Riachuelo_W", -9, -5, -7, -4),
                ("Block_FrCaneca_Senado_C", -5, 7, -3, 8),
                ("Block_Senado_Valadares_C", -5, 4, -3, 5),
                ("Block_Invalidos_GomesFreire", -5, -2, -3, 2),
                ("Block_ViscRioBranco_Lavradio", -1, 7, 1, 8),
                ("Block_Senado_RepChile", -1, 4, 1, 5),
                ("Block_GomesFreire_Lavradio", -1, -2, 1, 2),
                ("Block_RepChile_EvaristoVeiga", 7, 1, 9, 2),
                ("Block_Marrecas_Oeste", 7, -2, 7, -1),
                ("Block_Passeio_Leste", 9, -2, 9, -1),
                ("Block_MemDeSa_SantaTeresa", 3, -5, 5, -4),
                ("Block_MemDeSa_TeixeiraFreitas", 7, -5, 9, -4),
                ("Block_Muratori_SilvioRomero", -1, -9, -1, -7),
                ("Block_SilvioRomero_Murtinho", 1, -9, 1, -7),
                ("Block_Murtinho_SantaTeresa", 3, -9, 3, -7),
                ("Block_JoaquimSilva_Lapa", 7, -9, 7, -7),
                ("Block_Lapa_AugustoSevero", 9, -9, 9, -7)
            };

            int buildingSeed = 0;
            foreach (var b in blocks)
            {
                Transform blockGroup = CreateGroup(parent, b.name);
                CreatePavedPlaza(blockGroup, b.xMin, b.zMin, b.xMax, b.zMax, roadsMat);

                for (int gx = b.xMin; gx <= b.xMax; gx++)
                {
                    for (int gz = b.zMin; gz <= b.zMax; gz++)
                    {
                        // Deixa o miolo de quarteirões 3x5 com árvores/jardim
                        bool isInnerCourtyard = (gx > b.xMin && gx < b.xMax && gz > b.zMin && gz < b.zMax);
                        Vector3 pos = new Vector3(gx * TileSize, 0f, gz * TileSize);

                        if (isInnerCourtyard)
                        {
                            string treeFile = ((gx + gz) % 2 == 0) ? "tree-large.fbx" : "tree-small.fbx";
                            InstantiateModel(LoadCityPrefab(treeFile), $"Tree_{gx}_{gz}", blockGroup,
                                pos, Quaternion.Euler(0f, (buildingSeed * 90) % 360, 0f), Vector3.one * PropScale, cityMat, addCollider: true);
                            continue;
                        }

                        // Determina para qual rua próxima o prédio deve olhar
                        float rotY = DetermineBuildingFacingAngle(gx, gz, b.xMin, b.zMin, b.xMax, b.zMax);
                        string buildingFile = StandardBuildings[buildingSeed % StandardBuildings.Length];
                        buildingSeed++;

                        InstantiateModel(
                            LoadCityPrefab(buildingFile),
                            $"Building_{gx}_{gz}",
                            blockGroup,
                            pos,
                            Quaternion.Euler(0f, rotY, 0f),
                            Vector3.one * BuildingScale,
                            cityMat,
                            addCollider: true
                        );
                    }
                }
            }
        }

        private static void BuildStreetPropsAndLights(Transform parent, HashSet<Vector2Int> roadCells, Material roadsMat, Material cityMat)
        {
            GameObject trafficLightPrefab = LoadRoadPrefab("traffic-light.fbx");
            GameObject streetLightPrefab = LoadRoadPrefab("light-curved.fbx");
            GameObject signStopPrefab = LoadRoadPrefab("road-sign-stop.fbx");
            GameObject treeSmallPrefab = LoadCityPrefab("tree-small.fbx");

            int propIndex = 0;
            foreach (Vector2Int cell in roadCells)
            {
                bool n = roadCells.Contains(new Vector2Int(cell.x, cell.y + 1));
                bool s = roadCells.Contains(new Vector2Int(cell.x, cell.y - 1));
                bool e = roadCells.Contains(new Vector2Int(cell.x + 1, cell.y));
                bool w = roadCells.Contains(new Vector2Int(cell.x - 1, cell.y));
                int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

                Vector3 tileCenter = new Vector3(cell.x * TileSize, 0f, cell.y * TileSize);

                // Semáforos nas esquinas dos cruzamentos de 4 vias
                if (count == 4)
                {
                    InstantiateModel(trafficLightPrefab, $"TrafficLight_{cell.x}_{cell.y}_NE", parent,
                        tileCenter + new Vector3(4.3f, 0f, 4.3f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * PropScale, roadsMat, addCollider: false);
                    InstantiateModel( signStopPrefab, $"RoadSign_{cell.x}_{cell.y}_SW", parent,
                        tileCenter + new Vector3(-4.3f, 0f, -4.3f), Quaternion.identity, Vector3.one * PropScale, roadsMat, addCollider: false);
                }
                // Postes de iluminação ao longo das retas a cada 2 tiles
                else if (count == 2 && ((n && s) || (e && w)) && (propIndex++ % 2 == 0))
                {
                    Vector3 offset = (e && w) ? new Vector3(0f, 0f, 4.4f) : new Vector3(4.4f, 0f, 0f);
                    float rot = (e && w) ? 180f : -90f;
                    InstantiateModel(streetLightPrefab, $"StreetLight_{cell.x}_{cell.y}", parent,
                        tileCenter + offset, Quaternion.Euler(0f, rot, 0f), Vector3.one * PropScale, roadsMat, addCollider: false);

                    if (propIndex % 4 == 0 && treeSmallPrefab != null)
                    {
                        InstantiateModel(treeSmallPrefab, $"SidewalkTree_{cell.x}_{cell.y}", parent,
                            tileCenter - offset, Quaternion.identity, Vector3.one * (PropScale * 0.85f), cityMat, addCollider: false);
                    }
                }
            }
        }

        private static void CreatePavedPlaza(Transform parent, int xMin, int zMin, int xMax, int zMax, Material roadsMat)
        {
            GameObject tileLowPrefab = LoadRoadPrefab("tile-low.fbx");
            if (tileLowPrefab == null) return;

            for (int gx = xMin; gx <= xMax; gx++)
            {
                for (int gz = zMin; gz <= zMax; gz++)
                {
                    Vector3 pos = new Vector3(gx * TileSize, 0.005f, gz * TileSize);
                    InstantiateModel(
                        tileLowPrefab,
                        $"SidewalkLot_{gx}_{gz}",
                        parent,
                        pos,
                        Quaternion.identity,
                        new Vector3(TileSize, 1.5f, TileSize),
                        roadsMat,
                        addCollider: false
                    );
                }
            }
        }

        private static float DetermineBuildingFacingAngle(int gx, int gz, int xMin, int zMin, int xMax, int zMax)
        {
            if (gz == zMin) return 0f;     // Olha para Sul (-Z)
            if (gz == zMax) return 180f;   // Olha para Norte (+Z)
            if (gx == xMin) return 90f;    // Olha para Oeste (-X)
            if (gx == xMax) return -90f;   // Olha para Leste (+X)
            return 0f;
        }

        private static GameObject InstantiateModel(
            GameObject prefab,
            string instanceName,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material overrideMaterial,
            bool addCollider)
        {
            if (prefab == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;

            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer mr in renderers)
            {
                if (overrideMaterial != null)
                {
                    mr.sharedMaterial = overrideMaterial;
                }
            }

            if (addCollider)
            {
                AttachFittedBoxCollider(instance);
            }

            return instance;
        }

        private static void AttachFittedBoxCollider(GameObject instance)
        {
            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return;

            bool hasBounds = false;
            Bounds localBounds = default;

            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                Bounds meshBounds = mf.sharedMesh.bounds;
                if (!hasBounds)
                {
                    localBounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(meshBounds);
                }
            }

            if (hasBounds)
            {
                BoxCollider col = instance.GetComponent<BoxCollider>();
                if (col == null) col = instance.AddComponent<BoxCollider>();
                col.center = localBounds.center;
                col.size = new Vector3(
                    Mathf.Max(0.08f, localBounds.size.x * 0.92f),
                    Mathf.Max(0.15f, localBounds.size.y),
                    Mathf.Max(0.08f, localBounds.size.z * 0.92f)
                );
            }
        }

        private static Material CreateOrUpdateMaterial(
            string matPath,
            string texturePath,
            Color baseColor,
            float smoothness,
            Shader shader)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (shader != null)
            {
                mat.shader = shader;
            }

            if (!string.IsNullOrEmpty(texturePath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                }
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Transform CreateGroup(Transform parent, string groupName)
        {
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject LoadRoadPrefab(string fileName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(RoadsFbxFolder + fileName);

        private static GameObject LoadCityPrefab(string fileName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(CityFbxFolder + fileName);
    }
}
