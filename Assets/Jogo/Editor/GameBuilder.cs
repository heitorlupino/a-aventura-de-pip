using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta a fase inteira automaticamente a partir do mapa em texto abaixo.
/// Menu: Jogo > Montar Fase 1
/// Para editar a fase, altere o mapa e rode o menu de novo.
/// </summary>
public static class GameBuilder
{
    const string Root = "Assets/Jogo";
    const string SpriteDir = Root + "/Sprites";
    const string FontDir = Root + "/Fontes";
    const string ScenePath = Root + "/Cenas/Fase1.unity";
    const int PixelsPerUnit = 16;

    // Legenda do mapa (cada caractere = 1 bloco de 1x1 unidade):
    //  #  chão            =  plataforma (atravessável por baixo)
    //  ^  espinhos        C  moeda           H  coração (+1 vida)
    //  E  inimigo         P  jogador         K  checkpoint
    //  F  bandeira final  .  vazio
    static readonly string[] Map =
    {
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        ".......................................C",
        "......................................C.C...................................CHCC",
        ".......CC.............CCC..................E.....C........CC................====..........CC",
        "......C..C...........=====........C.###########.===......C..C.......CC................C...==...C",
        ".................................##############.....................==....E.......E...==......==...CC.E..C..F",
        "..P........E.............^...E...##############......K....^^...E........#############...........################",
        "###############...#############################.....###############.....#############...........################",
        "###############...#############################.....###############.....#############^^^^^^^^^^^################",
        "###############...#############################.....###############.....########################################"
    };

    [MenuItem("Jogo/Montar Fase 1")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        AssetDatabase.Refresh();
        ConfigureSprites();
        var sprites = LoadSprites();
        if (sprites == null) return;
        var noFriction = GetOrCreateNoFrictionMaterial();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int height = Map.Length;
        int width = 0;
        foreach (var line in Map) width = Mathf.Max(width, line.Length);

        char At(int x, int y)
        {
            int row = height - 1 - y;
            if (row < 0 || row >= height || x < 0) return '.';
            string line = Map[row];
            return x < line.Length ? line[x] : '.';
        }
        bool IsSolid(char c) => c == '#' || c == '=';
        // Fora do mapa conta como chão, para não desenhar bordas nos limites da fase.
        bool Ground(int x, int y) => (x < 0 || x >= width) ? At(Mathf.Clamp(x, 0, width - 1), y) == '#' : At(x, y) == '#';
        string GroundSprite(int x, int y)
        {
            bool top = !Ground(x, y + 1);
            bool left = !Ground(x - 1, y), right = !Ground(x + 1, y);
            if (top) return left && right ? "grama_unica" : left ? "grama_esq" : right ? "grama_dir" : "grama";
            if (left) return "terra_esq";
            if (right) return "terra_dir";
            return Ground(x, y + 2) && Ground(x, y + 3) ? "terra_funda" : "terra";
        }
        bool Walkable(int x, int y) => x >= 0 && x < width && !IsSolid(At(x, y)) && At(x, y) != '^' && IsSolid(At(x, y - 1));

        // ---------- Câmera e luz ----------
        var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.75f, 0.91f, 0.98f);
        camGO.transform.position = new Vector3(8f, 6f, -10f);
        camGO.AddComponent<AudioListener>();
        var follow = camGO.AddComponent<CameraFollow>();
        AddGlobalLight2D();

        // ---------- Terreno (CompositeCollider2D junta todos os blocos) ----------
        var terrain = new GameObject("Terreno");
        terrain.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var terrainComposite = terrain.AddComponent<CompositeCollider2D>();
        terrainComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        var platforms = new GameObject("Plataformas");
        platforms.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var platformComposite = platforms.AddComponent<CompositeCollider2D>();
        platformComposite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        platformComposite.usedByEffector = true;
        var effector = platforms.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 160f;

        var coinsParent = new GameObject("Moedas").transform;
        var itemsParent = new GameObject("Itens").transform;
        var hazardsParent = new GameObject("Perigos").transform;
        var enemiesParent = new GameObject("Inimigos").transform;

        Vector3 playerStart = new Vector3(2f, 3f, 0f);
        var enemyCells = new List<Vector2Int>();
        int coinCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = At(x, y);
                var pos = new Vector3(x, y, 0f);
                switch (c)
                {
                    case '#':
                    {
                        var tile = CreateSprite("Bloco", terrain.transform, pos, sprites[GroundSprite(x, y)], 0);
                        var box = tile.AddComponent<BoxCollider2D>();
                        box.compositeOperation = Collider2D.CompositeOperation.Merge;
                        break;
                    }
                    case '=':
                    {
                        var tile = CreateSprite("Plataforma", platforms.transform, pos, sprites["plataforma"], 0);
                        var box = tile.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(1f, 0.375f);
                        box.offset = new Vector2(0f, 0.3125f);
                        box.compositeOperation = Collider2D.CompositeOperation.Merge;
                        break;
                    }
                    case '^':
                    {
                        var spike = CreateSprite("Espinhos", hazardsParent, pos, sprites["espinho"], 1);
                        var box = spike.AddComponent<BoxCollider2D>();
                        box.isTrigger = true;
                        box.size = new Vector2(0.8f, 0.4f);
                        box.offset = new Vector2(0f, -0.28f);
                        spike.AddComponent<Hazard>();
                        break;
                    }
                    case 'C':
                        CreatePickup<Coin>("Moeda", coinsParent, pos, sprites["moeda"]);
                        coinCount++;
                        break;
                    case 'H':
                        CreatePickup<HeartPickup>("Coracao", itemsParent, pos, sprites["coracao"]);
                        break;
                    case 'E':
                        enemyCells.Add(new Vector2Int(x, y));
                        break;
                    case 'P':
                        playerStart = pos;
                        break;
                    case 'K':
                    {
                        var cp = CreateSprite("Checkpoint", itemsParent, pos + Vector3.down * 0.5f, sprites["checkpoint_inativo"], 1);
                        var box = cp.AddComponent<BoxCollider2D>();
                        box.isTrigger = true;
                        box.size = new Vector2(0.8f, 2f);
                        box.offset = new Vector2(0f, 1f);
                        var checkpoint = cp.AddComponent<Checkpoint>();
                        checkpoint.spriteRenderer = cp.GetComponent<SpriteRenderer>();
                        checkpoint.inactiveSprite = sprites["checkpoint_inativo"];
                        checkpoint.activeSprite = sprites["checkpoint_ativo"];
                        break;
                    }
                    case 'F':
                    {
                        var flag = CreateSprite("Bandeira Final", itemsParent, pos + Vector3.down * 0.5f, sprites["bandeira_final"], 1);
                        var box = flag.AddComponent<BoxCollider2D>();
                        box.isTrigger = true;
                        box.size = new Vector2(1f, 3f);
                        box.offset = new Vector2(0f, 1.5f);
                        flag.AddComponent<Goal>();
                        break;
                    }
                }
            }
        }
        terrainComposite.GenerateGeometry();
        platformComposite.GenerateGeometry();

        // ---------- Inimigos (limites de patrulha calculados pelo mapa) ----------
        foreach (var cell in enemyCells)
        {
            int left = cell.x, right = cell.x;
            while (Walkable(left - 1, cell.y)) left--;
            while (Walkable(right + 1, cell.y)) right++;

            var enemyGO = new GameObject("Inimigo");
            enemyGO.transform.SetParent(enemiesParent, false);
            enemyGO.transform.position = new Vector3(cell.x, cell.y, 0f);
            var erb = enemyGO.AddComponent<Rigidbody2D>();
            erb.bodyType = RigidbodyType2D.Kinematic;
            var box = enemyGO.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.8f, 0.55f);
            box.offset = new Vector2(0f, -0.2f);
            CreateSprite("Visual", enemyGO.transform, enemyGO.transform.position, sprites["inimigo1"], 5);
            var enemy = enemyGO.AddComponent<Enemy>();
            enemy.leftLimit = left;
            enemy.rightLimit = right;
            enemy.frame1 = sprites["inimigo1"];
            enemy.frame2 = sprites["inimigo2"];
            enemy.deadSprite = sprites["inimigo_morto"];
        }

        // ---------- Jogador ----------
        var player = new GameObject("Jogador") { tag = "Player" };
        player.transform.position = playerStart;
        var prb = player.AddComponent<Rigidbody2D>();
        prb.gravityScale = 3.2f;
        prb.constraints = RigidbodyConstraints2D.FreezeRotation;
        prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        prb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var capsule = player.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(0.6f, 0.9f);
        capsule.offset = new Vector2(0f, -0.05f);
        capsule.sharedMaterial = noFriction;
        var playerVisual = CreateSprite("Visual", player.transform, playerStart, sprites["jogador_parado1"], 10);
        var pc = player.AddComponent<PlayerController>();
        pc.visual = playerVisual.transform;
        pc.idleFrames = new[] { sprites["jogador_parado1"], sprites["jogador_parado2"] };
        pc.runFrames = new[] { sprites["jogador_correr1"], sprites["jogador_correr2"], sprites["jogador_correr3"], sprites["jogador_correr4"] };
        pc.jumpSprite = sprites["jogador_pulo"];
        pc.fallSprite = sprites["jogador_queda"];

        // ---------- Limites laterais invisíveis ----------
        CreateWall("Limite Esquerdo", -0.5f, height, noFriction);
        CreateWall("Limite Direito", width - 0.5f, height, noFriction);

        follow.target = player.transform;
        follow.levelMinX = -0.5f;
        follow.levelMaxX = width - 0.5f;
        follow.levelMinY = -0.5f;
        follow.levelMaxY = height + 2f;

        // ---------- Fundo: céu e sol presos à câmera, camadas com parallax ----------
        var sky = CreateSprite("Ceu", camGO.transform, Vector3.zero, sprites["ceu"], -100);
        sky.transform.localPosition = new Vector3(0f, 0f, 10f);
        sky.transform.localScale = new Vector3(160f, 1.7f, 1f);
        var sun = CreateSprite("Sol", camGO.transform, Vector3.zero, sprites["sol"], -99);
        sun.transform.localPosition = new Vector3(6.5f, 3.6f, 10f);

        var background = new GameObject("Fundo").transform;
        var rnd = new System.Random(42);
        AddParallaxLayer(background, sprites["montanhas"], "Montanhas", 5.8f, 0.85f, -60, width);
        AddParallaxLayer(background, sprites["colinas"], "Colinas", 4.2f, 0.65f, -50, width);
        float cloudFactor = 0.9f;
        int cloudIndex = 0;
        for (float x = -6f; x < width * (1f - cloudFactor) + 40f; x += 5f + (float)rnd.NextDouble() * 7f)
        {
            var sprite = (cloudIndex++ % 2 == 0) ? sprites["nuvem"] : sprites["nuvem2"];
            var cloud = CreateSprite("Nuvem", background, new Vector3(x, 8.5f + (float)rnd.NextDouble() * 4f, 0f), sprite, -70);
            var p = cloud.AddComponent<Parallax>();
            p.factor = cloudFactor;
            p.driftSpeed = -0.1f - (float)rnd.NextDouble() * 0.15f;
        }

        // ---------- Decoração sobre o chão (sem colisão) ----------
        var decor = new GameObject("Decoracao").transform;
        var drnd = new System.Random(7);
        CreateSprite("Placa", decor, new Vector3(playerStart.x + 2f, playerStart.y - 0.5f, 0f), sprites["placa"], 1);
        int lastTreeX = -10;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                if (!(At(x, y) == '#' && At(x, y + 1) == '.')) continue;
                if (Mathf.Abs(x - playerStart.x) < 1.5f || Mathf.Abs(x - (playerStart.x + 2f)) < 0.5f) continue;
                var basePos = new Vector3(x, y + 0.5f, 0f);
                double r = drnd.NextDouble();
                bool roomForTree = At(x, y + 2) == '.' && At(x, y + 3) == '.' && x - lastTreeX > 5
                                   && At(x - 1, y) == '#' && At(x + 1, y) == '#';
                if (roomForTree && r < 0.22) { CreateSprite("Arvore", decor, basePos, sprites["arvore"], -3); lastTreeX = x; }
                else if (r < 0.34 && At(x + 1, y) == '#' && At(x + 1, y + 1) == '.')
                    CreateSprite("Arbusto", decor, basePos + Vector3.right * 0.5f, sprites["arbusto"], -2);
                else if (r < 0.50) CreateSprite("Flores", decor, basePos, sprites["flores"], 1);
                else if (r < 0.68) CreateSprite("Capim", decor, basePos, sprites["capim"], 1);
                else if (r < 0.74) CreateSprite("Pedra", decor, basePos, sprites["pedra"], 1);
                else if (r < 0.79) CreateSprite("Cogumelo", decor, basePos, sprites["cogumelo"], 1);
            }
        }

        // ---------- GameManager ----------
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<AudioSource>().playOnAwake = false;
        gmGO.AddComponent<GameManager>().totalCoins = coinCount;

        // ---------- Interface ----------
        BuildUI(sprites);

        // ---------- Salvar ----------
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != ScenePath) buildScenes.Add(s);
        EditorBuildSettings.scenes = buildScenes.ToArray();

        Debug.Log($"[Jogo] Fase montada: {width}x{height} blocos, {coinCount} moedas, {enemyCells.Count} inimigos. Aperte Play!");
    }

    // =====================================================================
    // Interface (Canvas com fontes pixel art, molduras 9-slice e ícones)
    // =====================================================================
    static void BuildUI(Dictionary<string, Sprite> sprites)
    {
        var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var titleFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/PressStart2P.ttf") ?? builtin;
        var bodyFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/VT323.ttf") ?? builtin;
        var frame = sprites["ui_painel"];

        var canvasGO = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var ui = canvasGO.AddComponent<UIManager>();
        var root = canvasGO.transform;

        var white = new Color(1f, 0.98f, 0.94f);
        var gold = new Color(0.996f, 0.906f, 0.38f);
        var cyan = new Color(0.17f, 0.91f, 0.96f);
        var red = new Color(1f, 0.38f, 0.38f);
        var topLeft = new Vector2(0f, 1f);
        var topRight = new Vector2(1f, 1f);
        var top = new Vector2(0.5f, 1f);
        var center = new Vector2(0.5f, 0.5f);

        // ================= HUD =================
        var hud = CreatePanel(root, "HUD", Color.clear);

        var lifeBox = CreateFrame(hud.transform, "Caixa Vidas", frame, topLeft, new Vector2(24, -24), new Vector2(360, 136));
        ui.heartIcons = new Image[5];
        for (int i = 0; i < ui.heartIcons.Length; i++)
            ui.heartIcons[i] = CreateImage(lifeBox, $"Coracao {i + 1}", sprites["coracao"], topLeft, new Vector2(22 + i * 62, -18), new Vector2(54, 54));
        ui.heartFull = sprites["coracao"];
        ui.heartEmpty = sprites["coracao_vazio"];
        CreateImage(lifeBox, "Icone Moeda", sprites["moeda"], topLeft, new Vector2(24, -78), new Vector2(42, 42));
        ui.coinsText = CreateText(lifeBox, "Moedas", "x 0/0", 52, bodyFont, topLeft, new Vector2(80, -70), TextAnchor.UpperLeft, gold, 3);

        var timeBox = CreateFrame(hud.transform, "Caixa Tempo", frame, top, new Vector2(0, -24), new Vector2(240, 120));
        CreateText(timeBox, "Rotulo Tempo", "TEMPO", 20, titleFont, top, new Vector2(0, -22), TextAnchor.UpperCenter, cyan, 3);
        ui.timeText = CreateText(timeBox, "Tempo", "00:00", 60, bodyFont, top, new Vector2(0, -46), TextAnchor.UpperCenter, white, 3);

        var scoreBox = CreateFrame(hud.transform, "Caixa Pontos", frame, topRight, new Vector2(-24, -24), new Vector2(320, 120));
        CreateText(scoreBox, "Rotulo Pontos", "PONTOS", 20, titleFont, topRight, new Vector2(-26, -22), TextAnchor.UpperRight, cyan, 3);
        ui.scoreText = CreateText(scoreBox, "Pontos", "000000", 64, bodyFont, topRight, new Vector2(-26, -44), TextAnchor.UpperRight, white, 3);
        ui.hud = hud;

        // Flash vermelho ao levar dano
        var flash = CreatePanel(root, "Flash de Dano", new Color(1f, 0.1f, 0.15f, 0f));
        ui.damageFlash = flash.GetComponent<Image>();

        // ================= MENU =================
        var menu = CreatePanel(root, "Menu", new Color(0.05f, 0.04f, 0.12f, 0.55f));
        CreateText(menu.transform, "Subtitulo Topo", "A AVENTURA DE PIP", 32, titleFont, center, new Vector2(0, 340), TextAnchor.MiddleCenter, cyan, 4);
        CreateText(menu.transform, "Titulo", "VALE DOS SLIMES", 80, titleFont, center, new Vector2(0, 240), TextAnchor.MiddleCenter, gold, 8);
        CreateText(menu.transform, "Objetivo", "Atravesse o vale, junte moedas e alcance a bandeira dourada!", 50, bodyFont,
            center, new Vector2(0, 130), TextAnchor.MiddleCenter, white, 3);
        var controls = CreateFrame(menu.transform, "Caixa Controles", frame, center, new Vector2(0, -50), new Vector2(1020, 270));
        CreateText(controls, "Controles",
            "<color=#FEE761>A / D  ou  SETAS</color>    andar\n" +
            "<color=#FEE761>ESPAÇO  ou  W</color>    pular (segure para ir mais alto)\n" +
            "<color=#FEE761>PULE EM CIMA</color>    para derrotar os slimes\n" +
            "<color=#FEE761>ESC</color>    pausar",
            46, bodyFont, center, Vector2.zero, TextAnchor.MiddleCenter, white, 3);
        var startPrompt = CreateText(menu.transform, "Iniciar", "PRESSIONE ENTER", 36, titleFont, center, new Vector2(0, -270), TextAnchor.MiddleCenter, white, 5);
        ui.menuHighScoreText = CreateText(menu.transform, "Recorde", "Melhor pontuação: 0", 44, bodyFont, center, new Vector2(0, -345), TextAnchor.MiddleCenter, white, 3);
        ui.menuPanel = menu;

        // ================= PAUSA =================
        var pause = CreatePanel(root, "Pausa", new Color(0.05f, 0.04f, 0.12f, 0.6f));
        var pauseBox = CreateFrame(pause.transform, "Caixa Pausa", frame, center, Vector2.zero, new Vector2(720, 420));
        CreateText(pauseBox, "Titulo", "PAUSA", 64, titleFont, center, new Vector2(0, 110), TextAnchor.MiddleCenter, gold, 6);
        CreateText(pauseBox, "Opcoes",
            "<color=#FEE761>ESC</color>   continuar\n<color=#FEE761>R</color>   recomeçar a fase\n<color=#FEE761>M</color>   voltar ao menu",
            50, bodyFont, center, new Vector2(0, -50), TextAnchor.MiddleCenter, white, 3);
        ui.pausePanel = pause;

        // ================= FIM DE JOGO =================
        var gameOver = CreatePanel(root, "Fim de Jogo", new Color(0.18f, 0.02f, 0.06f, 0.7f));
        CreateText(gameOver.transform, "Titulo", "FIM DE JOGO", 72, titleFont, center, new Vector2(0, 290), TextAnchor.MiddleCenter, red, 8);
        CreateText(gameOver.transform, "Subtitulo", "Os slimes venceram desta vez...", 48, bodyFont, center, new Vector2(0, 195), TextAnchor.MiddleCenter, white, 3);
        var goBox = CreateFrame(gameOver.transform, "Caixa Resultado", frame, center, new Vector2(0, 10), new Vector2(680, 260));
        ui.gameOverInfoText = CreateText(goBox, "Info", "", 54, bodyFont, center, Vector2.zero, TextAnchor.MiddleCenter, white, 3);
        var retryPrompt = CreateText(gameOver.transform, "Tentar", "ENTER  TENTAR DE NOVO", 30, titleFont, center, new Vector2(0, -240), TextAnchor.MiddleCenter, white, 4);
        CreateText(gameOver.transform, "Menu", "M  voltar ao menu", 44, bodyFont, center, new Vector2(0, -310), TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.9f), 3);
        ui.gameOverPanel = gameOver;

        // ================= VITÓRIA =================
        var victory = CreatePanel(root, "Vitoria", new Color(0.02f, 0.12f, 0.08f, 0.65f));
        CreateText(victory.transform, "Titulo", "FASE CONCLUÍDA!", 64, titleFont, center, new Vector2(0, 300), TextAnchor.MiddleCenter, gold, 8);
        CreateText(victory.transform, "Subtitulo", "Pip chegou à bandeira dourada!", 50, bodyFont, center, new Vector2(0, 205), TextAnchor.MiddleCenter, cyan, 3);
        var vBox = CreateFrame(victory.transform, "Caixa Resultado", frame, center, new Vector2(0, 0), new Vector2(680, 300));
        ui.victoryInfoText = CreateText(vBox, "Info", "", 54, bodyFont, center, Vector2.zero, TextAnchor.MiddleCenter, white, 3);
        var againPrompt = CreateText(victory.transform, "Jogar", "ENTER  JOGAR DE NOVO", 30, titleFont, center, new Vector2(0, -250), TextAnchor.MiddleCenter, white, 4);
        CreateText(victory.transform, "Menu", "M  voltar ao menu", 44, bodyFont, center, new Vector2(0, -320), TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.9f), 3);
        ui.victoryPanel = victory;

        ui.pulsingTexts = new[] { startPrompt, retryPrompt, againPrompt };

        pause.SetActive(false);
        gameOver.SetActive(false);
        victory.SetActive(false);
    }

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Font font,
                           Vector2 anchor, Vector2 position, TextAnchor alignment, Color color, int shadow = 4)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(1500, size * 5);

        var text = go.GetComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.lineSpacing = 1.1f;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (shadow > 0)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0.09f, 0.08f, 0.15f, 1f);
            s.effectDistance = new Vector2(shadow, -shadow);
        }
        return text;
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Caixa com moldura pixel art (9-slice). Cada pixel do sprite vira 3 px na tela.</summary>
    static RectTransform CreateFrame(Transform parent, string name, Sprite frame, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var img = CreateImage(parent, name, frame, anchor, position, size);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 100f / PixelsPerUnit / 3f;
        return img.rectTransform;
    }

    // =====================================================================
    // Auxiliares
    // =====================================================================
    static GameObject CreateSprite(string name, Transform parent, Vector3 worldPos, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
    }

    static void AddParallaxLayer(Transform parent, Sprite sprite, string name, float y, float factor, int order, int levelWidth)
    {
        float tileWidth = sprite.bounds.size.x;
        for (float x = -12f; x < levelWidth * (1f - factor) + 40f; x += tileWidth)
        {
            var go = CreateSprite(name, parent, new Vector3(x, y, 0f), sprite, order);
            go.AddComponent<Parallax>().factor = factor;
        }
    }

    static void CreatePickup<T>(string name, Transform parent, Vector3 pos, Sprite sprite) where T : Pickup
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.35f;
        var visual = CreateSprite("Visual", go.transform, pos, sprite, 3);
        go.AddComponent<T>().visual = visual.transform;
    }

    static void CreateWall(string name, float x, int height, PhysicsMaterial2D material)
    {
        var go = new GameObject(name);
        var edge = go.AddComponent<EdgeCollider2D>();
        edge.points = new[] { new Vector2(x, -20f), new Vector2(x, height + 20f) };
        edge.sharedMaterial = material;
    }

    static void AddGlobalLight2D()
    {
        // Procura a luz 2D do URP sem depender dele: em projetos sem URP, simplesmente não cria a luz.
        Type lightType = null;
        foreach (var t in TypeCache.GetTypesDerivedFrom<Behaviour>())
        {
            if (t.FullName == "UnityEngine.Rendering.Universal.Light2D") { lightType = t; break; }
        }
        if (lightType == null) return;

        var go = new GameObject("Global Light 2D");
        var light = go.AddComponent(lightType);
        var prop = lightType.GetProperty("lightType");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(light, Enum.Parse(prop.PropertyType, "Global"));
        }
        else
        {
            var so = new SerializedObject(light);
            var typeProp = so.FindProperty("m_LightType");
            if (typeProp != null) { typeProp.intValue = 4; so.ApplyModifiedPropertiesWithoutUndo(); } // 4 = Global
        }
    }

    static PhysicsMaterial2D GetOrCreateNoFrictionMaterial()
    {
        const string path = Root + "/SemAtrito.physicsMaterial2D";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial2D("SemAtrito") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static void ConfigureSprites()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

            string file = Path.GetFileNameWithoutExtension(path);
            bool bottomPivot = file.StartsWith("bandeira") || file.StartsWith("checkpoint") || BottomPivotDecor.Contains(file);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)(bottomPivot ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            importer.SetTextureSettings(settings);
            if (file == "ui_painel") importer.spriteBorder = new Vector4(8, 8, 8, 8);

            importer.SaveAndReimport();
        }

        // Fontes pixel art: renderização nítida (sem suavização).
        foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
        {
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is TrueTypeFontImporter fontImporter
                && fontImporter.fontRenderingMode != FontRenderingMode.HintedRaster)
            {
                fontImporter.fontRenderingMode = FontRenderingMode.HintedRaster;
                fontImporter.SaveAndReimport();
            }
        }
    }

    static readonly HashSet<string> BottomPivotDecor = new HashSet<string>
        { "arvore", "arbusto", "flores", "capim", "pedra", "cogumelo", "placa" };

    static Dictionary<string, Sprite> LoadSprites()
    {
        string[] names =
        {
            "jogador_parado1", "jogador_parado2", "jogador_correr1", "jogador_correr2", "jogador_correr3",
            "jogador_correr4", "jogador_pulo", "jogador_queda",
            "inimigo1", "inimigo2", "inimigo_morto", "moeda", "coracao", "coracao_vazio",
            "grama", "grama_esq", "grama_dir", "grama_unica", "terra", "terra_esq", "terra_dir", "terra_funda",
            "plataforma", "espinho", "bandeira_final", "checkpoint_inativo", "checkpoint_ativo",
            "arvore", "arbusto", "flores", "capim", "pedra", "cogumelo", "placa",
            "ceu", "sol", "montanhas", "colinas", "nuvem", "nuvem2", "ui_painel",
        };
        var dict = new Dictionary<string, Sprite>();
        foreach (var n in names)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{n}.png");
            if (sprite == null)
            {
                Debug.LogError($"[Jogo] Sprite não encontrado: {SpriteDir}/{n}.png");
                return null;
            }
            dict[n] = sprite;
        }
        return dict;
    }
}
