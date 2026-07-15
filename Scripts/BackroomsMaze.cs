using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;

public class BackroomsMaze : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Dimensions")]
    public float corridorWidth = 3f;
    public float corridorHeight = 3f;
    public float roomCeilingHeight = 9f;
    public float wallThickness = 0.05f;

    [System.Serializable]
    public class Segment
    {
        public string name;
        public float width;
        public float length;
        public float turnAfter;
    }

    [Header("Segments (spawn → exit)")]
    public Segment[] segments = new Segment[]
    {
        new Segment { name = "EntryCorridor",  width = 3f,   length = 100f,  turnAfter = 90f },
        new Segment { name = "Connector",     width = 3f,   length = 2f,    turnAfter = 0f },
        new Segment { name = "OpenRoom",      width = 60f, length = 60f,  turnAfter = 0f },
        new Segment { name = "LongCorridor2", width = 3f,   length = 80f,   turnAfter = 90f },
        new Segment { name = "Corridor3",     width = 3f,   length = 25f,   turnAfter = 90f },
        new Segment { name = "Corridor4",    width = 3f,   length = 50f,   turnAfter = -90f },
        new Segment { name = "FinalRoom",    width = 70f,  length = 10f,   turnAfter = 0f },
    };

    [Header("Colors")]
    public Color wallColor = new Color(0.14f, 0.14f, 0.14f, 1f);
    public Color floorColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    public Color ceilingColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    public Color lightColor = new Color(1f, 0.05f, 0.05f, 1f);
    public Color fogColor = new Color(0.12f, 0.02f, 0.02f, 1f);

    [Header("Bookshelves")]
    public float bookshelfHeight = 2.2f;
    public float bookshelfDepth = 0.5f;
    public float bookshelfSegmentLength = 4f;
    public float bookshelfGapBetweenSegments = 1.2f;
    public float aisleWidth = 2f;
    public float wallMargin = 4f;
    public float quadrantGapWidth = 6f;
    public int shelvesPerQuadrantSide = 4;

    [Header("Center Podium")]
    public float podiumRadius = 2.5f;
    public float podiumHeight = 0.7f;

    [Header("Tapes")]
    public int tapeCount = 20;
    [System.NonSerialized] public Color tapeColor = Color.black;

    private Material wallMat, corridorWallMat, floorMat, ceilMat, corridorCeilMat, lightMat, doorMat;
    private Vector3 mazeEntryPos;
    private Quaternion mazeEntryRot;
    private Texture2D wallTex, floorTex, ceilTex, doorTex;
    private bool fadeActive;

    // Scary painting system
    private Dictionary<int, List<Texture2D>> paintingTextures = new Dictionary<int, List<Texture2D>>();
    private List<Texture2D> bloodTextures = new List<Texture2D>();
    private List<GameObject> allPaintings = new List<GameObject>();
    private List<GameObject> allGlows = new List<GameObject>();
    private List<float> paintingZs = new List<float>();
    private List<SegmentInfo> paintingSegs = new List<SegmentInfo>();
    private bool paintingsReady;

    // Wall eye system
    private List<WallEye> allEyes = new List<WallEye>();
    private Texture2D eyeScleraTex;
    private Texture2D eyePupilTex;

    private struct SegmentInfo
    {
        public Vector3 pos;
        public Quaternion rot;
        public float width;
        public float length;
        public bool isRoom;
    }
    private List<SegmentInfo> segmentInfos = new List<SegmentInfo>();

    void Start()
    {
        SetupEnvironment();
        InitMaterials();
        LoadPaintingTextures();
        BuildLayout();
        SpawnBatteries();
        SpawnCheckpoints();
        PositionPlayer();
        SetupMonsterIntegration();
        InitPaintings();
        InitEyes();
    }

    void SetupEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.04f;
        RenderSettings.fogColor = Color.black;
        RenderSettings.ambientLight = new Color(0.06f, 0.005f, 0.005f, 1f);
        RenderSettings.ambientIntensity = 0.5f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    void InitMaterials()
    {
        Color baseRed = new Color(0.2f, 0.02f, 0.02f, 1f);

        // Load external wall.png for walls and ceiling
        Texture2D externalWall = Resources.Load<Texture2D>("Textures/wall");
        if (externalWall == null)
        {
            #if UNITY_EDITOR
            externalWall = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/wall.png");
            #endif
        }

        // Blend textures: 70% for room, 10% for corridor (darker, more red)
        Texture2D blendedWall = null;
        Texture2D blendedCeil = null;
        Texture2D corridorBlendedWall = null;
        Texture2D corridorBlendedCeil = null;
        if (externalWall != null)
        {
            blendedWall = BlendTexture(externalWall, baseRed, 0.7f);
            blendedCeil = blendedWall;
            corridorBlendedWall = BlendTexture(externalWall, baseRed, 0.1f);
            corridorBlendedCeil = corridorBlendedWall;
        }

        floorTex = TextureGen.MarbleFloorTexture();
        doorTex = TextureGen.DoorTexture();

        wallMat = new Material(Shader.Find("Standard"));
        wallMat.color = Color.white;
        wallMat.SetFloat("_Glossiness", 0f);
        wallMat.SetFloat("_Metallic", 0f);
        if (blendedWall != null)
        {
            wallMat.SetTexture("_MainTex", blendedWall);
            wallMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }
        else
        {
            wallTex = TextureGen.WallTexture();
            wallMat.SetTexture("_MainTex", wallTex);
            wallMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }

        floorMat = new Material(Shader.Find("Standard"));
        floorMat.color = Color.white;
        floorMat.SetFloat("_Glossiness", 0f);
        floorMat.SetFloat("_Metallic", 0f);
        floorMat.SetTexture("_MainTex", floorTex);
        floorMat.SetTextureScale("_MainTex", new Vector2(8f, 8f));

        ceilMat = new Material(Shader.Find("Standard"));
        ceilMat.color = Color.white;
        ceilMat.SetFloat("_Glossiness", 0f);
        ceilMat.SetFloat("_Metallic", 0f);
        if (blendedCeil != null)
        {
            ceilMat.SetTexture("_MainTex", blendedCeil);
            ceilMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }
        else
        {
            ceilTex = TextureGen.CeilingTexture();
            ceilMat.SetTexture("_MainTex", ceilTex);
            ceilMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }

        lightMat = NewMat(lightColor);

        // Corridor wall material: 10% texture, 90% dark red
        corridorWallMat = new Material(Shader.Find("Standard"));
        corridorWallMat.color = Color.white;
        corridorWallMat.SetFloat("_Glossiness", 0f);
        corridorWallMat.SetFloat("_Metallic", 0f);
        if (corridorBlendedWall != null)
        {
            corridorWallMat.SetTexture("_MainTex", corridorBlendedWall);
            corridorWallMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }
        else
        {
            corridorWallMat.SetTexture("_MainTex", wallTex);
            corridorWallMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }

        // Corridor ceiling material: 10% texture
        corridorCeilMat = new Material(Shader.Find("Standard"));
        corridorCeilMat.color = Color.white;
        corridorCeilMat.SetFloat("_Glossiness", 0f);
        corridorCeilMat.SetFloat("_Metallic", 0f);
        if (corridorBlendedCeil != null)
        {
            corridorCeilMat.SetTexture("_MainTex", corridorBlendedCeil);
            corridorCeilMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }
        else
        {
            corridorCeilMat.SetTexture("_MainTex", ceilTex);
            corridorCeilMat.SetTextureScale("_MainTex", new Vector2(6f, 6f));
        }

        doorMat = new Material(Shader.Find("Standard"));
        doorMat.color = Color.white;
        doorMat.SetTexture("_MainTex", doorTex);
    }

    Material NewMat(Color c)
    {
        return new Material(Shader.Find("Standard")) { color = c };
    }

    Texture2D BlendTexture(Texture2D src, Color baseColor, float texOpacity)
    {
        // Create a readable copy and blend: result = texOpacity * src + (1-texOpacity) * baseColor
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
        copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        copy.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = copy.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(
                pixels[i].r * texOpacity + baseColor.r * (1f - texOpacity),
                pixels[i].g * texOpacity + baseColor.g * (1f - texOpacity),
                pixels[i].b * texOpacity + baseColor.b * (1f - texOpacity),
                1f
            );
        }
        copy.SetPixels(pixels);
        copy.Apply(true);
        copy.filterMode = FilterMode.Bilinear;
        return copy;
    }

    void BuildLayout()
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        float h = corridorHeight;
        float wallH = h + 0.15f;
        float wallY = wallH * 0.5f;
        float lightSpacing = 6f;

        // No back wall here — T-junction opens left and right

        for (int i = 0; i < segments.Length; i++)
        {
            Segment seg = segments[i];
            float w = seg.width;
            float l = seg.length;
            bool isRoom = w > corridorWidth + 5f;

            segmentInfos.Add(new SegmentInfo { pos = pos, rot = rot, width = w, length = l, isRoom = isRoom });

            float curH = isRoom ? roomCeilingHeight : corridorHeight;
            float curWallH = curH + 0.15f;
            float curWallY = curWallH * 0.5f;
            Material activeWallMat = isRoom ? wallMat : corridorWallMat;
            Material activeCeilMat = isRoom ? ceilMat : corridorCeilMat;

            // Floor
            SpawnFloor(pos, rot, w, l);

            // Ceiling (use room height for rooms)
            SpawnCeiling(pos, rot, w, l, curH, activeCeilMat);

            // Left wall — for EntryCorridor (i==0), offset to leave opening for side corridors
            float wallStartOffset = (i == 0) ? corridorWidth * 0.5f : 0f;
            float leftWallLen = l + wallThickness - wallStartOffset;
            Vector3 leftPos = pos + rot * Vector3.left * (w * 0.5f) + rot * Vector3.forward * (wallStartOffset + leftWallLen * 0.5f);
            SpawnSideWall(leftPos, rot, wallThickness, curWallH, leftWallLen, curWallY, activeWallMat);

            // Right wall
            Vector3 rightPos = pos + rot * Vector3.right * (w * 0.5f) + rot * Vector3.forward * (wallStartOffset + leftWallLen * 0.5f);
            SpawnSideWall(rightPos, rot, wallThickness, curWallH, leftWallLen, curWallY, activeWallMat);

            // Lights along the ceiling
            int lightCount = Mathf.Max(1, Mathf.FloorToInt(l / lightSpacing));
            float entryLightIntensity = seg.name == "EntryCorridor" ? 1.5f : 4f;
            float entryLightRange = seg.name == "EntryCorridor" ? corridorWidth * 2f : corridorWidth * 4f;
            for (int li = 0; li < lightCount; li++)
            {
                float t = (li + 0.5f) / lightCount;
                Vector3 lightPos = pos + rot * Vector3.forward * (t * l) + rot * Vector3.up * (curH - 0.03f);
                if (isRoom)
                    SpawnRoomLight(lightPos, rot, w);
                else
                    SpawnCorridorLight(lightPos, rot, w, entryLightIntensity, entryLightRange);
            }

            // Barriers in corridor segments
            SpawnBarriers(pos, rot, w, l, curWallH);

            // Bookshelves and room features only in the large open room
            if (seg.name == "OpenRoom")
            {
                SpawnBookshelves(pos, rot, w, l, curH);
                SpawnTapes();
                SpawnCenterPodium(pos, rot, w, l);
                SpawnCRTTelevision(pos, rot, w, l);
                SpawnFlashlights(pos, rot, w, l);
                SpawnRoomLightsAndBells(pos, rot, w, l, curH);
            }

            // Advance position to end of segment
            Vector3 endPos = pos + rot * Vector3.forward * l;

            // Transition walls between different-width segments
            if (i < segments.Length - 1)
            {
                float nextW = segments[i + 1].width;
                float nextH = segments[i + 1].width > corridorWidth + 5f ? roomCeilingHeight : corridorHeight;
                float transWallH = Mathf.Max(curWallH, nextH + 0.15f);
                float transWallY = transWallH * 0.5f;
                Material transMat = (segments[i + 1].width > corridorWidth + 5f || isRoom) ? wallMat : corridorWallMat;
                float diff = nextW - w;
                if (Mathf.Abs(diff) > 0.01f)
                {
                    float sideLen = Mathf.Abs(diff) * 0.5f + wallThickness;
                    if (diff > 0)
                    {
                        Vector3 gapPos = endPos + rot * Vector3.left * (w * 0.5f + sideLen * 0.5f);
                        SpawnWall(gapPos, rot, new Vector3(sideLen, transWallH, wallThickness), transMat, transWallY);
                        Vector3 gapPosR = endPos + rot * Vector3.right * (w * 0.5f + sideLen * 0.5f);
                        SpawnWall(gapPosR, rot, new Vector3(sideLen, transWallH, wallThickness), transMat, transWallY);
                    }
                    else
                    {
                        Vector3 gapPos = endPos + rot * Vector3.left * (nextW * 0.5f + sideLen * 0.5f);
                        SpawnWall(gapPos, rot, new Vector3(sideLen, transWallH, wallThickness), transMat, transWallY);
                        Vector3 gapPosR = endPos + rot * Vector3.right * (nextW * 0.5f + sideLen * 0.5f);
                        SpawnWall(gapPosR, rot, new Vector3(sideLen, transWallH, wallThickness), transMat, transWallY);
                    }
                }
            }

            pos = endPos;
            rot *= Quaternion.Euler(0, seg.turnAfter, 0);

            // Trigger at the turn after LongCorridor2 — fades to black and teleports to Level0 maze
            if (seg.name == "LongCorridor1")
            {
                GameObject turnTrigger = new GameObject("TurnTrigger");
                turnTrigger.transform.SetParent(transform);
                turnTrigger.transform.position = endPos;
                turnTrigger.transform.rotation = rot;
                BoxCollider tc = turnTrigger.AddComponent<BoxCollider>();
                tc.isTrigger = true;
                tc.size = new Vector3(corridorWidth, corridorHeight, 2f);
                turnTrigger.AddComponent<TeleportTrigger>();
            }

            // Spawn progress door between OpenRoom and next segment
            if (seg.name == "OpenRoom")
            {
                // endPos is at the room exit, corridor connects at center (3m wide)
                // Door spans the corridor width, positioned at endPos
                float doorW = corridorWidth;
                float doorH = corridorHeight;
                float doorT = 0.15f;

                GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                door.name = "ProgressDoor";
                door.transform.SetParent(transform);
                door.transform.position = endPos + rot * Vector3.up * (doorH * 0.5f);
                door.transform.rotation = rot;
                door.transform.localScale = new Vector3(doorW, doorH, doorT);

                Material doorMat = new Material(Shader.Find("Standard"));
                doorMat.color = new Color(0.3f, 0.15f, 0.05f, 1f);
                doorMat.SetFloat("_Glossiness", 0.2f);
                doorMat.SetFloat("_Metallic", 0.1f);
                door.GetComponent<Renderer>().material = doorMat;

                door.AddComponent<ProgressDoor>();

                // Store the maze entry position for SetupMonsterIntegration
                mazeEntryPos = endPos;
                mazeEntryRot = rot;
            }
        }

        // End-cap wall at the very end
        SpawnWall(
            pos + rot * Vector3.back * (wallThickness * 0.5f),
            rot,
            new Vector3(segments[segments.Length - 1].width + wallThickness, wallH, wallThickness),
            wallMat,
            wallY
        );

        // 在 FinalRoom 末端放置逃离触发器，与 EscapeSequence 联动
        GameObject escapeTrigger = new GameObject("EscapeZoneTrigger");
        escapeTrigger.transform.SetParent(transform);
        escapeTrigger.transform.position = pos + rot * Vector3.back * 3f;
        BoxCollider escapeCol = escapeTrigger.AddComponent<BoxCollider>();
        escapeCol.isTrigger = true;
        escapeCol.size = new Vector3(segments[segments.Length - 1].width, roomCeilingHeight, 6f);
        escapeTrigger.AddComponent<EscapeZoneTrigger>();

        // Side corridors branching left and right from spawn point (T-junction)
        SpawnSideCorridor(Vector3.zero, Quaternion.Euler(0, -90, 0));
        SpawnSideCorridor(Vector3.zero, Quaternion.Euler(0, 90, 0));

        // Spawn room behind the T-junction (where player starts)
        SpawnSpawnRoom();

        // Set up monster system after all geometry is generated
        SetupMonsterIntegration();
    }

    void SpawnCheckpoints()
    {
        // Ensure CheckpointManager exists
        if (CheckpointManager.Instance == null)
        {
            var cpm = new GameObject("CheckpointManager");
            cpm.AddComponent<CheckpointManager>();
        }

        // Ensure GameManager exists and has player reference
        if (GameManager.Instance == null)
        {
            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
        }
        if (GameManager.Instance != null && player != null)
            GameManager.Instance.SetPlayer(player);

        // If no save exists, register the spawn room as the default checkpoint
        if (!CheckpointManager.Instance.HasSave())
        {
            Vector3 spawnPos = new Vector3(0f, 1f, -(corridorWidth * 0.5f + 1.5f));
            CheckpointManager.Instance.SaveCheckpoint(spawnPos, 0f);
            CheckpointManager.Instance.SaveGameState(0, false, false);
        }

        // Checkpoint 1: Spawn room (maze entrance)
        SpawnCheckpoint(
            new Vector3(0f, 1f, -(corridorWidth * 0.5f + 1.5f)),
            "SpawnRoom");

        // Checkpoint 2: Just entered the OpenRoom (after EntryCorridor + Connector)
        // EntryCorridor ends at (0,0,100) then turns +90° (facing +X)
        // Connector is 2m along +X → (2,0,100)
        // OpenRoom starts at (2,0,100) facing +X, place checkpoint a few meters in
        SpawnCheckpoint(
            new Vector3(5f, 1f, 100f),
            "OpenRoomEntry");

        // Checkpoint 3: Just entered LongCorridor2 (after OpenRoom, past ProgressDoor)
        // OpenRoom ends at (62,0,100), ProgressDoor is there
        // LongCorridor2 starts at (62,0,100) facing +X, place checkpoint a few meters in
        SpawnCheckpoint(
            new Vector3(65f, 1f, 100f),
            "LongCorridor2Entry");
    }

    void SpawnCheckpoint(Vector3 pos, string name)
    {
        GameObject cp = new GameObject($"Checkpoint_{name}");
        cp.transform.SetParent(transform);
        cp.transform.position = pos;
        BoxCollider col = cp.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(corridorWidth, corridorHeight, 2f);
        CheckpointTrigger ct = cp.AddComponent<CheckpointTrigger>();
        // Set checkpoint name via reflection-friendly public field
        var checkpointNameField = typeof(CheckpointTrigger).GetField("checkpointName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (checkpointNameField != null)
            checkpointNameField.SetValue(ct, name);
    }

    void SpawnSpawnRoom()
    {
        // 3m x 3m room behind the T-junction, height = corridorHeight - 0.5
        float w = 3f;
        float l = 3f;
        float h = corridorHeight - 0.5f; // 2.5m
        float wallH = h + 0.15f;
        float wallY = wallH * 0.5f;
        Quaternion roomRot = Quaternion.identity;
        // Room spans z = -(corridorWidth/2 + l) to -(corridorWidth/2)
        // SpawnFloor/Ceiling treat pos as the near edge and extend forward by l
        Vector3 floorStart = new Vector3(0, 0, -(corridorWidth * 0.5f + l));
        Vector3 roomCenter = new Vector3(0, 0, -(corridorWidth * 0.5f + l * 0.5f));

        // Floor
        SpawnFloor(floorStart, roomRot, w, l);

        // Ceiling
        SpawnCeiling(floorStart, roomRot, w, l, h, corridorCeilMat);

        // Back wall (far end, -Z side) — end wall: wide in X, thin in Z
        Vector3 backPos = floorStart + roomRot * Vector3.back * (wallThickness * 0.5f);
        SpawnWall(backPos, roomRot, new Vector3(w + wallThickness, wallH, wallThickness), corridorWallMat, wallY);

        // Left wall — full length
        Vector3 leftWallPos = floorStart + roomRot * Vector3.left * (w * 0.5f) + roomRot * Vector3.forward * (l * 0.5f);
        SpawnSideWall(leftWallPos, roomRot, wallThickness, wallH, l + wallThickness, wallY, corridorWallMat);

        // Right wall — full length
        Vector3 rightWallPos = floorStart + roomRot * Vector3.right * (w * 0.5f) + roomRot * Vector3.forward * (l * 0.5f);
        SpawnSideWall(rightWallPos, roomRot, wallThickness, wallH, l + wallThickness, wallY, corridorWallMat);

        // A dim light in the room
        Vector3 lightPos = roomCenter + roomRot * Vector3.up * (h - 0.03f);
        SpawnCorridorLight(lightPos, roomRot, w, 1.5f, corridorWidth * 2f);
    }

    void SpawnSideCorridor(Vector3 startPos, Quaternion corridorRot)
    {
        float w = corridorWidth;
        float l = 30f;
        float h = corridorHeight;
        float wallH = h + 0.15f;
        float wallY = wallH * 0.5f;
        // Offset side walls from junction center to leave opening for the T-junction
        float wallStart = w * 0.5f;
        float wallLen = l - wallStart + wallThickness;

        // Floor
        SpawnFloor(startPos, corridorRot, w, l);

        // Ceiling
        SpawnCeiling(startPos, corridorRot, w, l, h, corridorCeilMat);

        // Left wall — starts at wallStart from junction, not from center
        Vector3 leftPos = startPos + corridorRot * Vector3.left * (w * 0.5f) + corridorRot * Vector3.forward * (wallStart + wallLen * 0.5f);
        SpawnSideWall(leftPos, corridorRot, wallThickness, wallH, wallLen, wallY, corridorWallMat);

        // Right wall
        Vector3 rightPos = startPos + corridorRot * Vector3.right * (w * 0.5f) + corridorRot * Vector3.forward * (wallStart + wallLen * 0.5f);
        SpawnSideWall(rightPos, corridorRot, wallThickness, wallH, wallLen, wallY, corridorWallMat);

        // Dim lights (same as EntryCorridor)
        int lightCount = Mathf.Max(1, Mathf.FloorToInt(l / 6f));
        for (int li = 0; li < lightCount; li++)
        {
            float t = (li + 0.5f) / lightCount;
            Vector3 lightPos = startPos + corridorRot * Vector3.forward * (t * l) + corridorRot * Vector3.up * (h - 0.03f);
            SpawnCorridorLight(lightPos, corridorRot, w, 1.5f, corridorWidth * 2f);
        }

        // End-cap wall at far end
        Vector3 endPos = startPos + corridorRot * Vector3.forward * l;
        SpawnWall(
            endPos + corridorRot * Vector3.back * (wallThickness * 0.5f),
            corridorRot,
            new Vector3(w + wallThickness, wallH, wallThickness),
            corridorWallMat,
            wallY
        );
    }

    void SpawnFloor(Vector3 pos, Quaternion rot, float w, float l)
    {
        int tilesX = Mathf.Max(1, Mathf.CeilToInt(w / 100f));
        int tilesZ = Mathf.Max(1, Mathf.CeilToInt(l / 100f));
        float tileW = w / tilesX;
        float tileL = l / tilesZ;

        for (int tx = 0; tx < tilesX; tx++)
        {
            for (int tz = 0; tz < tilesZ; tz++)
            {
                Vector3 localOffset = rot * (Vector3.right * ((tx + 0.5f) * tileW - w * 0.5f) + Vector3.forward * ((tz + 0.5f) * tileL));
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Plane);
                p.name = "Floor";
                p.transform.SetParent(transform);
                p.transform.localScale = new Vector3(tileW / 10f, 1, tileL / 10f);
                p.transform.position = pos + localOffset;
                p.transform.rotation = rot;
                p.GetComponent<Renderer>().sharedMaterial = floorMat;
                // Per-tile texture tiling based on actual tile size
                MaterialPropertyBlock fpb = new MaterialPropertyBlock();
                fpb.SetVector("_MainTex_ST", new Vector4(tileW / textureTileSize, tileL / textureTileSize, 0, 0));
                p.GetComponent<Renderer>().SetPropertyBlock(fpb);
                Object.DestroyImmediate(p.GetComponent<MeshCollider>());
                BoxCollider bc = p.AddComponent<BoxCollider>();
                // Extend collider slightly beyond tile edges to eliminate seams between segments
                bc.size = new Vector3(10.5f, 1f, 10.5f);
                bc.center = new Vector3(0, -0.25f, 0);
            }
        }
    }

    void SpawnCeiling(Vector3 pos, Quaternion rot, float w, float l, float h, Material mat)
    {
        int tilesX = Mathf.Max(1, Mathf.CeilToInt(w / 100f));
        int tilesZ = Mathf.Max(1, Mathf.CeilToInt(l / 100f));
        float tileW = w / tilesX;
        float tileL = l / tilesZ;

        for (int tx = 0; tx < tilesX; tx++)
        {
            for (int tz = 0; tz < tilesZ; tz++)
            {
                Vector3 localOffset = rot * (Vector3.right * ((tx + 0.5f) * tileW - w * 0.5f) + Vector3.forward * ((tz + 0.5f) * tileL));
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Plane);
                p.name = "Ceiling";
                p.transform.SetParent(transform);
                p.transform.localScale = new Vector3(tileW / 10f, 1, tileL / 10f);
                p.transform.position = pos + localOffset + rot * Vector3.up * h;
                p.transform.rotation = rot * Quaternion.Euler(180, 0, 0);
                p.GetComponent<Renderer>().sharedMaterial = mat;
                MaterialPropertyBlock cpb = new MaterialPropertyBlock();
                cpb.SetVector("_MainTex_ST", new Vector4(tileW / textureTileSize, tileL / textureTileSize, 0, 0));
                p.GetComponent<Renderer>().SetPropertyBlock(cpb);
            }
        }
    }

    void SpawnSideWall(Vector3 pos, Quaternion rot, float thickness, float height, float length, float yOffset, Material sourceMat)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.transform.SetParent(transform);
        w.transform.position = new Vector3(pos.x, yOffset, pos.z);
        w.transform.rotation = rot;
        w.transform.localScale = new Vector3(thickness, height, length);

        Material m = new Material(sourceMat);
        m.SetTextureScale("_MainTex", new Vector2(length / textureTileSize, height / textureTileSize));
        w.GetComponent<Renderer>().sharedMaterial = m;
    }

    // Desired texture tile size in meters (one texture repeat covers this area)
    public float textureTileSize = 1f;

    void SpawnWall(Vector3 pos, Quaternion rot, Vector3 scale, Material mat, float yOffset)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.transform.SetParent(transform);
        w.transform.position = new Vector3(pos.x, yOffset, pos.z);
        w.transform.rotation = rot;
        w.transform.localScale = scale;

        // Per-wall texture tiling based on actual visible face dimensions
        // Cube local scale: X=thickness, Y=height, Z=length
        // Visible face is Y×Z (for side walls) or X×Y (for end caps)
        // Use the two largest dimensions as the visible face
        float dimA = scale.x;
        float dimB = scale.y;
        float dimC = scale.z;
        // Pick the two largest dims as the visible face
        float faceU, faceV;
        if (dimA <= dimB && dimA <= dimC) { faceU = dimB; faceV = dimC; }
        else if (dimB <= dimA && dimB <= dimC) { faceU = dimA; faceV = dimC; }
        else { faceU = dimA; faceV = dimB; }

        w.GetComponent<Renderer>().sharedMaterial = mat;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetVector("_MainTex_ST", new Vector4(faceU / textureTileSize, faceV / textureTileSize, 0, 0));
        w.GetComponent<Renderer>().SetPropertyBlock(mpb);
    }


    void SpawnCorridorLight(Vector3 pos, Quaternion rot, float segWidth, float intensity = 4f, float range = -1f)
    {
        float fixtureSize = Mathf.Min(segWidth * 0.3f, 1.5f);
        GameObject fix = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fix.transform.SetParent(transform);
        fix.transform.position = pos;
        fix.transform.rotation = rot;
        fix.transform.localScale = new Vector3(fixtureSize, 0.05f, fixtureSize);
        fix.GetComponent<Renderer>().material = lightMat;

        Light pl = fix.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.range = range > 0 ? range : corridorWidth * 4f;
        pl.intensity = intensity;
        pl.color = new Color(1f, 0.02f, 0.02f, 1f);
    }

    void SpawnRoomLight(Vector3 pos, Quaternion rot, float segWidth)
    {
        float fixtureSize = Mathf.Min(segWidth * 0.2f, 1.5f);
        GameObject fix = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fix.transform.SetParent(transform);
        fix.transform.position = pos;
        fix.transform.rotation = rot;
        fix.transform.localScale = new Vector3(fixtureSize, 0.05f, fixtureSize);
        fix.GetComponent<Renderer>().material = lightMat;

        Light pl = fix.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.range = corridorWidth * 3f;
        pl.intensity = 2f;
        pl.color = lightColor;
    }

    void SpawnBarriers(Vector3 segStart, Quaternion rot, float w, float l, float wallH)
    {
        if (w > corridorWidth + 0.5f || l < 12f) return;

        float barrierW = corridorWidth * 0.5f;
        float barrierH = wallH - 1f;
        float barrierThick = wallThickness;
        float margin = 4f;

        // Group-based: within each group, alternate sides (L-R-L or R-L-R)
        float z = margin;
        bool leftSide = Random.value > 0.5f;

        while (z < l - margin)
        {
            int groupSize = Random.Range(2, 5);
            float groupSpacing = 1.5f;

            for (int gi = 0; gi < groupSize && z < l - margin; gi++)
            {
                SpawnSingleBarrier(segStart, rot, w, wallH, barrierW, barrierH, barrierThick, z, leftSide);
                leftSide = !leftSide;
                z += groupSpacing;
            }

            // Gap between groups
            z += Random.Range(8f, 14f);
        }
    }

    void SpawnSingleBarrier(Vector3 segStart, Quaternion rot, float w, float wallH,
        float barrierW, float barrierH, float barrierThick, float z, bool leftSide)
    {
        // Pivot at the wall edge, at distance z along corridor
        Vector3 sideDir = leftSide ? Vector3.left : Vector3.right;
        Vector3 pivotWorldPos = segStart
            + rot * (sideDir * (w * 0.5f))
            + rot * (Vector3.forward * z)
            + rot * (Vector3.up * (barrierH * 0.5f));

        GameObject pivot = new GameObject("Barrier");
        pivot.transform.SetParent(transform);
        pivot.transform.position = pivotWorldPos;
        // Hidden: flush against wall (same rotation as corridor)
        pivot.transform.rotation = rot;

        // Panel as child: extends along Z from pivot (flush with wall)
        // Scale: thin in X, height in Y, length in Z
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "BarrierPanel";
        panel.transform.SetParent(pivot.transform);
        panel.transform.localScale = new Vector3(barrierThick, barrierH, barrierW);
        // Offset so one edge is at pivot (wall), extends inward along Z
        // Offset into wall so panel is fully hidden inside the wall
        float wallOffset = leftSide ? -wallThickness * 0.5f : wallThickness * 0.5f;
        panel.transform.localPosition = new Vector3(wallOffset, 0, barrierW * 0.5f);
        panel.GetComponent<Renderer>().material = doorMat;

        BarrierDoor door = pivot.AddComponent<BarrierDoor>();
        door.player = player;
        // Left wall: rotate +90° to swing into corridor (+X)
        // Right wall: rotate -90° to swing into corridor (-X)
        door.deployAngle = leftSide ? 90f : -90f;
    }

    void SpawnBookshelves(Vector3 roomPos, Quaternion roomRot, float roomW, float roomL, float ceilingH)
    {
        // Room center
        Vector3 roomCenter = roomPos + roomRot * (Vector3.forward * (roomL * 0.5f));

        // Usable area inside room after margin from walls
        float usableW = roomW - wallMargin * 2f;
        float usableL = roomL - wallMargin * 2f;

        // Each quadrant size
        float quadW = (usableW - quadrantGapWidth) * 0.5f;
        float quadL = (usableL - quadrantGapWidth) * 0.5f;

        float shelfY = bookshelfHeight * 0.5f;
        float shelfUnitLen = bookshelfSegmentLength + bookshelfGapBetweenSegments;

        for (int qx = 0; qx < 2; qx++)
        {
            for (int qz = 0; qz < 2; qz++)
            {
                // Quadrant center relative to room center
                float offsetX = (qx - 0.5f) * (quadW + quadrantGapWidth);
                float offsetZ = (qz - 0.5f) * (quadL + quadrantGapWidth);

                Vector3 quadCenter = roomCenter
                    + roomRot * (Vector3.right * offsetX)
                    + roomRot * (Vector3.forward * offsetZ);

                // Layout: rows along X, shelves within each row along Z
                // Each row is a line of shelves along Z (corridor direction)
                // Rows are stacked along X with aisle between them
                int rowCount = shelvesPerQuadrantSide;

                // How many shelf units fit along Z within this quadrant
                int unitsPerRow = Mathf.FloorToInt((quadL - bookshelfDepth) / shelfUnitLen);
                if (unitsPerRow < 1) unitsPerRow = 1;

                float rowPhysicalLen = unitsPerRow * bookshelfSegmentLength + (unitsPerRow - 1) * bookshelfGapBetweenSegments;
                float rowStartZ = -rowPhysicalLen * 0.5f;

                // Rows along X: depth of each row + aisle between
                float totalRowsDepth = rowCount * bookshelfDepth + (rowCount - 1) * aisleWidth;
                float rowStartX = -totalRowsDepth * 0.5f;

                for (int r = 0; r < rowCount; r++)
                {
                    float rowX = rowStartX + r * (bookshelfDepth + aisleWidth) + bookshelfDepth * 0.5f;

                    for (int u = 0; u < unitsPerRow; u++)
                    {
                        float unitZ = rowStartZ + u * shelfUnitLen + bookshelfSegmentLength * 0.5f;

                        Vector3 shelfPos = quadCenter
                            + roomRot * (Vector3.right * rowX)
                            + roomRot * (Vector3.forward * unitZ)
                            + roomRot * (Vector3.up * shelfY);

                        GameObject shelf = new GameObject($"Shelf_Q{qx}{qz}_R{r}_U{u}");
                        shelf.transform.SetParent(transform);
                        shelf.transform.position = shelfPos;
                        shelf.transform.rotation = roomRot;

                        MeshFilter smf = shelf.AddComponent<MeshFilter>();
                        MeshRenderer smr = shelf.AddComponent<MeshRenderer>();
                        Mesh shelfMesh = CreateBookshelfMesh(bookshelfDepth, bookshelfHeight, bookshelfSegmentLength);
                        smf.sharedMesh = shelfMesh;

                        Material m = new Material(wallMat);
                        m.SetTextureScale("_MainTex", new Vector2(bookshelfSegmentLength / textureTileSize, bookshelfHeight / textureTileSize));
                        smr.sharedMaterial = m;

                        // Add collider so player can't walk through
                        BoxCollider bc = shelf.AddComponent<BoxCollider>();
                        bc.size = new Vector3(bookshelfDepth, bookshelfHeight, bookshelfSegmentLength);
                    }
                }
            }
        }
    }

    void SpawnTapes()
    {
        // Collect all bookshelf transforms
        List<Transform> shelves = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Shelf_"))
                shelves.Add(child);
        }

        if (shelves.Count == 0) return;

        // Load play.jpg for tape top face
        Texture2D playTex = null;
#if UNITY_EDITOR
        playTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/play.jpg");
#endif
        if (playTex == null)
            playTex = Resources.Load<Texture2D>("Textures/play");

        // Top face material (play.jpg)
        Material topMat = new Material(Shader.Find("Standard"));
        topMat.color = Color.white;
        topMat.SetFloat("_Glossiness", 0.2f);
        topMat.SetFloat("_Metallic", 0f);
        if (playTex != null)
            topMat.SetTexture("_MainTex", playTex);

        // Body material (pure black)
        Material bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = Color.black;
        bodyMat.SetFloat("_Glossiness", 0.3f);
        bodyMat.SetFloat("_Metallic", 0f);

        // Tape dimensions: flat cuboid (X=depth, Y=height, Z=width in shelf local space)
        float tapeW = 0.24f;
        float tapeH = 0.07f;
        float tapeL = 0.36f;

        // Board thickness must match CreateBookshelfMesh
        float t = 0.04f;

        float interiorDepth = bookshelfDepth - t * 2f;
        float interiorWidth = bookshelfSegmentLength - t * 2f;

        // Top surface Y of the board below each of the 3 layers
        float[] layerBottomY = new float[3];
        layerBottomY[0] = -bookshelfHeight * 0.5f + t;
        layerBottomY[1] = -bookshelfHeight * 0.5f + bookshelfHeight / 3f + t * 0.5f;
        layerBottomY[2] = -bookshelfHeight * 0.5f + 2f * bookshelfHeight / 3f + t * 0.5f;

        float tapeHalfH = tapeH * 0.5f;
        float maxDim = Mathf.Max(tapeW, tapeL);
        float margin = maxDim * 0.5f;

        // Build the tape mesh once (2 submeshes: top face, other faces)
        Mesh tapeMesh = CreateTapeMesh(tapeW, tapeH, tapeL);

        for (int i = 0; i < tapeCount; i++)
        {
            Transform shelf = shelves[Random.Range(0, shelves.Count)];
            int layer = Random.Range(0, 3);

            float localX = Random.Range(-interiorDepth * 0.5f + margin, interiorDepth * 0.5f - margin);
            float localZ = Random.Range(-interiorWidth * 0.5f + margin, interiorWidth * 0.5f - margin);
            float localY = layerBottomY[layer] + tapeHalfH;

            Vector3 worldPos = shelf.position + shelf.rotation * new Vector3(localX, localY, localZ);

            GameObject tape = new GameObject($"Tape_{i}");
            tape.transform.SetParent(transform);
            tape.transform.position = worldPos;
            tape.transform.rotation = shelf.rotation * Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            MeshFilter mf = tape.AddComponent<MeshFilter>();
            mf.sharedMesh = tapeMesh;

            MeshRenderer mr = tape.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[] { topMat, bodyMat };

            // Add collider for the tape
            BoxCollider bc = tape.AddComponent<BoxCollider>();
            bc.size = new Vector3(tapeW, tapeH, tapeL);

            TapeInteract ti = tape.AddComponent<TapeInteract>();
        }
    }

    Mesh CreateTapeMesh(float w, float h, float l)
    {
        // Local space: X=depth(w), Y=height(h), Z=width(l)
        // 24 vertices (4 per face) for correct per-face UV mapping
        // Submesh 0: +Y (top) face with play.jpg
        // Submesh 1: remaining 5 faces with black material
        float hx = w * 0.5f, hy = h * 0.5f, hz = l * 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> topTris = new List<int>();
        List<int> bodyTris = new List<int>();

        // Helper to push a quad (4 verts + uvs) and return first index
        int AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                    Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int start = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);
            return start;
        }

        // Submesh 0: Top face (+Y) — texture maps perfectly to face, rotated 90°
        // UV rotation 90° clockwise: (u,v) -> (v, 1-u)
        // Corners: (-hx,+hy,-hz), (+hx,+hy,-hz), (+hx,+hy,+hz), (-hx,+hy,+hz)
        int topStart = AddQuad(
            new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz),
            new Vector3(hx, hy, hz),   new Vector3(-hx, hy, hz),
            new Vector2(0, 1), new Vector2(0, 0),
            new Vector2(1, 0), new Vector2(1, 1)
        );
        topTris.Add(topStart + 0); topTris.Add(topStart + 2); topTris.Add(topStart + 1);
        topTris.Add(topStart + 0); topTris.Add(topStart + 3); topTris.Add(topStart + 2);

        // Submesh 1: Other 5 faces (black, UV doesn't matter)
        // -Z face
        int fZ = AddQuad(
            new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz),
            new Vector3(hx, hy, -hz),   new Vector3(-hx, hy, -hz),
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
        );
        bodyTris.Add(fZ + 0); bodyTris.Add(fZ + 2); bodyTris.Add(fZ + 1);
        bodyTris.Add(fZ + 0); bodyTris.Add(fZ + 3); bodyTris.Add(fZ + 2);

        // +Z face
        int bZ = AddQuad(
            new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz),
            new Vector3(hx, hy, hz),   new Vector3(-hx, hy, hz),
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
        );
        bodyTris.Add(bZ + 0); bodyTris.Add(bZ + 1); bodyTris.Add(bZ + 2);
        bodyTris.Add(bZ + 0); bodyTris.Add(bZ + 2); bodyTris.Add(bZ + 3);

        // -X face
        int lX = AddQuad(
            new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy, hz),
            new Vector3(-hx, hy, hz),   new Vector3(-hx, hy, -hz),
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
        );
        bodyTris.Add(lX + 0); bodyTris.Add(lX + 2); bodyTris.Add(lX + 1);
        bodyTris.Add(lX + 0); bodyTris.Add(lX + 3); bodyTris.Add(lX + 2);

        // +X face
        int rX = AddQuad(
            new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz),
            new Vector3(hx, hy, hz),   new Vector3(hx, hy, -hz),
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
        );
        bodyTris.Add(rX + 0); bodyTris.Add(rX + 1); bodyTris.Add(rX + 2);
        bodyTris.Add(rX + 0); bodyTris.Add(rX + 2); bodyTris.Add(rX + 3);

        // -Y face (bottom)
        int bot = AddQuad(
            new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz),
            new Vector3(hx, -hy, hz),   new Vector3(-hx, -hy, hz),
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
        );
        bodyTris.Add(bot + 0); bodyTris.Add(bot + 2); bodyTris.Add(bot + 1);
        bodyTris.Add(bot + 0); bodyTris.Add(bot + 3); bodyTris.Add(bot + 2);

        Mesh mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTris.ToArray(), 0);
        mesh.SetTriangles(bodyTris.ToArray(), 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void SpawnRoomLightsAndBells(Vector3 roomPos, Quaternion roomRot, float roomW, float roomL, float ceilingH)
    {
        Color coldYellow = new Color(1f, 0.85f, 0.4f, 1f);
        Vector3 roomCenter = roomPos + roomRot * (Vector3.forward * (roomL * 0.5f));

        // Evenly spaced cold yellow ceiling lights in a grid
        int gridX = Mathf.Max(2, Mathf.FloorToInt(roomW / 15f));
        int gridZ = Mathf.Max(2, Mathf.FloorToInt(roomL / 15f));
        List<Light> roomLights = new List<Light>();

        for (int gx = 0; gx < gridX; gx++)
        {
            for (int gz = 0; gz < gridZ; gz++)
            {
                float tx = (gx + 0.5f) / gridX;
                float tz = (gz + 0.5f) / gridZ;
                Vector3 localOffset = roomRot * (Vector3.right * (tx * roomW - roomW * 0.5f) + Vector3.forward * (tz * roomL));
                Vector3 lightPos = roomPos + localOffset + roomRot * Vector3.up * (ceilingH - 0.3f);

                GameObject fix = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fix.name = "RoomLight";
                fix.transform.SetParent(transform);
                fix.transform.position = lightPos;
                fix.transform.rotation = roomRot;
                fix.transform.localScale = new Vector3(0.6f, 0.08f, 0.6f);
                fix.GetComponent<Renderer>().material.color = coldYellow;

                Light pl = fix.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.range = 25f;
                pl.intensity = 3f;
                pl.color = coldYellow;
                roomLights.Add(pl);
            }
        }

        // RoomLightController on this object
        RoomLightController rlc = gameObject.AddComponent<RoomLightController>();
        rlc.player = player;
        rlc.roomCenter = roomCenter;
        rlc.roomRadius = Mathf.Min(roomW, roomL) * 0.5f;
        rlc.lights = roomLights;

        // Bells are placed manually in the scene editor, just assign player reference
        BellInteract[] bells = FindObjectsByType<BellInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in bells)
        {
            b.player = player;
        }
    }

    void SpawnCenterPodium(Vector3 roomPos, Quaternion roomRot, float roomW, float roomL)
    {
        // Room center
        Vector3 podiumPos = roomPos + roomRot * (Vector3.forward * (roomL * 0.5f));
        podiumPos.y = podiumHeight * 0.5f;

        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "CenterPodium";
        cyl.transform.SetParent(transform);
        cyl.transform.position = podiumPos;
        cyl.transform.rotation = roomRot;
        cyl.transform.localScale = new Vector3(podiumRadius * 2f, podiumHeight * 0.5f, podiumRadius * 2f);

        Material podMat = new Material(Shader.Find("Standard"));
        podMat.color = wallColor;
        podMat.SetFloat("_Glossiness", 0f);
        podMat.SetFloat("_Metallic", 0f);
        cyl.GetComponent<Renderer>().sharedMaterial = podMat;
    }

    void SpawnCRTTelevision(Vector3 roomPos, Quaternion roomRot, float roomW, float roomL)
    {
        // Place on top of center podium
        Vector3 center = roomPos + roomRot * (Vector3.forward * (roomL * 0.5f));
        float podiumTop = podiumHeight;

        // TV dimensions
        float bodyW = 1.2f;
        float bodyH = 1.0f;
        float bodyD = 0.9f;

        // Matte black plastic shell
        Material shellMat = new Material(Shader.Find("Standard"));
        shellMat.color = Color.black;
        shellMat.SetFloat("_Glossiness", 0.15f);
        shellMat.SetFloat("_Metallic", 0f);

        // Dark gray screen glass
        Material screenMat = new Material(Shader.Find("Standard"));
        screenMat.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        screenMat.SetFloat("_Glossiness", 0.9f);
        screenMat.SetFloat("_Metallic", 0.2f);

        // Power cord material (slightly lighter dark)
        Material cordMat = new Material(Shader.Find("Standard"));
        cordMat.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        cordMat.SetFloat("_Glossiness", 0.3f);
        cordMat.SetFloat("_Metallic", 0f);

        GameObject tv = new GameObject("CRTTelevision");
        tv.transform.SetParent(transform);
        tv.transform.position = center + roomRot * Vector3.up * podiumTop;
        tv.transform.rotation = roomRot;

        // Main body (slightly beveled cube)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "TV_Body";
        body.transform.SetParent(tv.transform);
        body.transform.localPosition = new Vector3(0, bodyH * 0.5f, 0);
        body.transform.localScale = new Vector3(bodyW, bodyH, bodyD);
        body.GetComponent<Renderer>().material = shellMat;

        // Front bezel frame (raised rim around screen)
        float bezelW = 0.95f;
        float bezelH = 0.75f;
        float bezelT = 0.04f;
        // Top bezel
        GameObject bzTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bzTop.name = "Bezel_Top";
        bzTop.transform.SetParent(tv.transform);
        bzTop.transform.localPosition = new Vector3(0, bodyH * 0.5f - (bezelH + bezelT) * 0.5f, bodyD * 0.5f + bezelT * 0.5f);
        bzTop.transform.localScale = new Vector3(bezelW, bezelT, bezelT);
        bzTop.GetComponent<Renderer>().material = shellMat;
        // Bottom bezel
        GameObject bzBot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bzBot.name = "Bezel_Bot";
        bzBot.transform.SetParent(tv.transform);
        bzBot.transform.localPosition = new Vector3(0, bodyH * 0.5f - (bezelH + bezelT) * 0.5f - bezelH, bodyD * 0.5f + bezelT * 0.5f);
        bzBot.transform.localScale = new Vector3(bezelW, bezelT, bezelT);
        bzBot.GetComponent<Renderer>().material = shellMat;
        // Left bezel
        GameObject bzLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bzLeft.name = "Bezel_Left";
        bzLeft.transform.SetParent(tv.transform);
        bzLeft.transform.localPosition = new Vector3(-(bezelW + bezelT) * 0.5f, bodyH * 0.5f - bezelH * 0.5f, bodyD * 0.5f + bezelT * 0.5f);
        bzLeft.transform.localScale = new Vector3(bezelT, bezelH, bezelT);
        bzLeft.GetComponent<Renderer>().material = shellMat;
        // Right bezel
        GameObject bzRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bzRight.name = "Bezel_Right";
        bzRight.transform.SetParent(tv.transform);
        bzRight.transform.localPosition = new Vector3((bezelW + bezelT) * 0.5f, bodyH * 0.5f - bezelH * 0.5f, bodyD * 0.5f + bezelT * 0.5f);
        bzRight.transform.localScale = new Vector3(bezelT, bezelH, bezelT);
        bzRight.GetComponent<Renderer>().material = shellMat;

        // Screen (dark gray glass, slightly inset)
        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "TV_Screen";
        screen.transform.SetParent(tv.transform);
        screen.transform.localPosition = new Vector3(0, bodyH * 0.5f - bezelH * 0.5f, bodyD * 0.5f + 0.005f);
        screen.transform.localScale = new Vector3(bezelW - bezelT * 2f, bezelH - bezelT * 2f, 0.01f);
        screen.GetComponent<Renderer>().material = screenMat;

        // Two control knobs on the front-right side
        for (int k = 0; k < 2; k++)
        {
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = $"Knob_{k}";
            knob.transform.SetParent(tv.transform);
            knob.transform.localPosition = new Vector3(bodyW * 0.5f - 0.12f, bodyH * 0.5f - 0.15f - k * 0.12f, bodyD * 0.5f + 0.02f);
            knob.transform.localRotation = Quaternion.Euler(90, 0, 0);
            knob.transform.localScale = new Vector3(0.03f, 0.02f, 0.03f);
            knob.GetComponent<Renderer>().material = shellMat;
        }

        // Speaker grille slits on front-left
        for (int s = 0; s < 5; s++)
        {
            GameObject slit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slit.name = $"SpeakerSlit_{s}";
            slit.transform.SetParent(tv.transform);
            slit.transform.localPosition = new Vector3(-bodyW * 0.5f + 0.15f, bodyH * 0.5f - 0.15f - s * 0.06f, bodyD * 0.5f + 0.005f);
            slit.transform.localScale = new Vector3(0.2f, 0.02f, 0.01f);
            slit.GetComponent<Renderer>().material = shellMat;
        }

        // Detached power cord at the back (curved cylinder)
        GameObject cord = new GameObject("PowerCord");
        cord.transform.SetParent(tv.transform);
        cord.transform.localPosition = new Vector3(0, bodyH * 0.4f, -bodyD * 0.5f - 0.02f);

        // Cord segments to simulate a dangling cable
        Vector3[] cordPts = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(0.05f, -0.1f, -0.08f),
            new Vector3(-0.02f, -0.25f, -0.12f),
            new Vector3(0.08f, -0.42f, -0.1f),
            new Vector3(-0.05f, -0.58f, -0.04f),
            new Vector3(0, -0.7f, 0.05f),
        };

        for (int c = 0; c < cordPts.Length - 1; c++)
        {
            Vector3 p1 = cordPts[c];
            Vector3 p2 = cordPts[c + 1];
            Vector3 mid = (p1 + p2) * 0.5f;
            float len = Vector3.Distance(p1, p2);

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = $"CordSeg_{c}";
            seg.transform.SetParent(cord.transform);
            seg.transform.localPosition = mid;
            seg.transform.localRotation = Quaternion.LookRotation(p2 - p1) * Quaternion.Euler(90, 0, 0);
            seg.transform.localScale = new Vector3(0.015f, len * 0.5f, 0.015f);
            seg.GetComponent<Renderer>().material = cordMat;
        }

        // Attach TVInteract
        TVInteract tvi = tv.AddComponent<TVInteract>();
    }

    void SpawnFlashlights(Vector3 roomPos, Quaternion roomRot, float roomW, float roomL)
    {
        Vector3 center = roomPos + roomRot * (Vector3.forward * (roomL * 0.5f));
        float podiumTop = podiumHeight;

        // Materials
        Material matBlack = new Material(Shader.Find("Standard"));
        matBlack.color = new Color(0.04f, 0.04f, 0.04f, 1f);
        matBlack.SetFloat("_Glossiness", 0.2f);
        matBlack.SetFloat("_Metallic", 0.3f);

        Material matDarkGrey = new Material(Shader.Find("Standard"));
        matDarkGrey.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        matDarkGrey.SetFloat("_Glossiness", 0.4f);
        matDarkGrey.SetFloat("_Metallic", 0.2f);

        Material matLightGrey = new Material(Shader.Find("Standard"));
        matLightGrey.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        matLightGrey.SetFloat("_Glossiness", 0.5f);
        matLightGrey.SetFloat("_Metallic", 0.1f);

        Material matReflector = new Material(Shader.Find("Standard"));
        matReflector.color = new Color(0.9f, 0.88f, 0.8f, 1f);
        matReflector.SetFloat("_Glossiness", 0.85f);
        matReflector.SetFloat("_Metallic", 0.3f);

        Material matGlass = new Material(Shader.Find("Standard"));
        matGlass.color = new Color(0.85f, 0.85f, 0.82f, 0.4f);
        matGlass.SetFloat("_Glossiness", 0.95f);
        matGlass.SetFloat("_Metallic", 0.5f);
        matGlass.SetOverrideTag("RenderType", "Transparent");
        matGlass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matGlass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        matGlass.SetInt("_ZWrite", 0);

        // Place 3 flashlights around podium edge, avoiding TV in center
        float placeR = podiumRadius * 0.6f;
        int count = 3;
        for (int i = 0; i < count; i++)
        {
            float angle = (i * 360f / count + 30f) * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(Mathf.Cos(angle) * placeR, 0, Mathf.Sin(angle) * placeR);
            Vector3 worldPos = center + roomRot * (localPos + Vector3.up * podiumTop);

            GameObject flashlight = new GameObject($"Flashlight_{i}");
            flashlight.transform.SetParent(transform);
            flashlight.transform.position = worldPos;
            flashlight.transform.rotation = roomRot * Quaternion.Euler(90f, 0f, 0f);
            // Lay flat on podium: rotate so barrel points along radial outward
            flashlight.transform.rotation = roomRot * Quaternion.LookRotation(new Vector3(localPos.x, 0, localPos.z), Vector3.up);

            // Short thick cylinder (head) + long thick cylinder (body)
            float headLen = 0.12f;
            float headR = 0.08f;
            float bodyLen = 0.5f;
            float bodyR = 0.06f;

            // Short thick head cylinder (front, matte black)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            head.transform.SetParent(flashlight.transform);
            head.transform.localPosition = new Vector3(0, 0, headLen * 0.5f);
            head.transform.localRotation = Quaternion.Euler(90, 0, 0);
            head.transform.localScale = new Vector3(headR * 2f, headLen * 0.5f, headR * 2f);
            head.GetComponent<Renderer>().material = matBlack;

            // Long thick body cylinder (behind head, dark grey)
            // Body's top face (z=0) connects to head's bottom face (z=0)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(flashlight.transform);
            body.transform.localPosition = new Vector3(0, 0, -bodyLen * 0.5f);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            body.transform.localScale = new Vector3(bodyR * 2f, bodyLen * 0.5f, bodyR * 2f);
            body.GetComponent<Renderer>().material = matDarkGrey;

            // Add pickup interaction
            flashlight.AddComponent<FlashlightPickup>();

            // Add collider for pickup detection
            BoxCollider bc = flashlight.AddComponent<BoxCollider>();
            bc.size = new Vector3(headR * 2.5f, headR * 2.5f, headLen + bodyLen);
        }

        // Attach FlashlightController to player camera (disabled until pickup)
        if (player != null)
        {
            Camera cam = player.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                GameObject lightObj = new GameObject("FlashlightLight");
                lightObj.transform.SetParent(cam.transform);
                lightObj.transform.localPosition = Vector3.zero;
                FlashlightController fc = lightObj.AddComponent<FlashlightController>();
                fc.enabled = false;
            }

            if (player.GetComponent<BatteryHUD>() == null)
                player.gameObject.AddComponent<BatteryHUD>();
            if (player.GetComponent<Inventory>() == null)
                player.gameObject.AddComponent<Inventory>();
        }
    }

    void SpawnBatteries()
    {
        int batteryCount = 10;

        // Battery body material (emissive green)
        Material bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = new Color(0.15f, 0.6f, 0.15f, 1f);
        bodyMat.SetFloat("_Glossiness", 0.4f);
        bodyMat.SetFloat("_Metallic", 0.1f);
        bodyMat.EnableKeyword("_EMISSION");
        bodyMat.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.1f, 1f));

        // Cap material (metallic)
        Material capMat = new Material(Shader.Find("Standard"));
        capMat.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        capMat.SetFloat("_Glossiness", 0.8f);
        capMat.SetFloat("_Metallic", 0.9f);

        float bodyR = 0.04f;
        float bodyH = 0.14f;
        float capR = 0.025f;
        float capH = 0.02f;

        // Batteries can appear in any segment — the player may revisit corridors
        if (segmentInfos.Count == 0) return;

        for (int i = 0; i < batteryCount; i++)
        {
            SegmentInfo seg = segmentInfos[Random.Range(0, segmentInfos.Count)];

            Vector3 localPos;
            if (seg.isRoom)
            {
                float halfW = seg.width * 0.4f;
                localPos = new Vector3(
                    Random.Range(-halfW, halfW),
                    0f,
                    seg.length * 0.5f + Random.Range(-seg.length * 0.35f, seg.length * 0.35f));
            }
            else
            {
                float sideOffset = Random.Range(-seg.width * 0.3f, seg.width * 0.3f);
                float t = Random.Range(0.1f, 0.9f);
                localPos = new Vector3(sideOffset, 0f, t * seg.length);
            }

            Vector3 worldPos = seg.pos + seg.rot * localPos + Vector3.up * (bodyH * 0.5f);

            GameObject battery = new GameObject($"Battery_{i}");
            battery.transform.SetParent(transform);
            battery.transform.position = worldPos;

            // Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(battery.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(bodyR * 2f, bodyH * 0.5f, bodyR * 2f);
            body.GetComponent<Renderer>().material = bodyMat;
            Destroy(body.GetComponent<Collider>());

            // Cap (positive terminal)
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Cap";
            cap.transform.SetParent(battery.transform);
            cap.transform.localPosition = new Vector3(0, bodyH * 0.5f + capH * 0.5f, 0);
            cap.transform.localScale = new Vector3(capR * 2f, capH * 0.5f, capR * 2f);
            cap.GetComponent<Renderer>().material = capMat;
            Destroy(cap.GetComponent<Collider>());

            // Collider for pickup detection (only on parent)
            BoxCollider bc = battery.AddComponent<BoxCollider>();
            bc.size = new Vector3(bodyR * 3f, bodyH + capH + 0.1f, bodyR * 3f);
            bc.center = new Vector3(0, capH * 0.5f, 0);

            // Small light to make visible in the dark
            Light bl = battery.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.range = 2.5f;
            bl.intensity = 0.6f;
            bl.color = new Color(0.3f, 0.8f, 0.3f, 1f);

            battery.AddComponent<BatteryPickup>();
        }
    }

    void SpawnBell(Vector3 pos, Quaternion rot, float radius, Material mat, string name)
    {
        GameObject bell = new GameObject(name);
        bell.transform.SetParent(transform);
        bell.transform.position = pos;
        bell.transform.rotation = rot;

        MeshFilter mf = bell.AddComponent<MeshFilter>();
        MeshRenderer mr = bell.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        Mesh hemisphere = CreateHemisphere(radius, 16);
        mf.sharedMesh = hemisphere;

        SphereCollider sc = bell.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 2f;

        BellInteract bi = bell.AddComponent<BellInteract>();
        bi.player = player;
    }

    Mesh CreateHemisphere(float radius, int segments)
    {
        Mesh mesh = new Mesh();
        int vertCount = (segments + 1) * (segments / 2 + 1);
        Vector3[] verts = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] tris = new int[segments * (segments / 2) * 6];
        int vi = 0, ti = 0;

        for (int lat = 0; lat <= segments / 2; lat++)
        {
            float phi = lat * Mathf.PI * 0.5f / (segments / 2);
            for (int lon = 0; lon <= segments; lon++)
            {
                float theta = lon * 2f * Mathf.PI / segments;
                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                verts[vi] = new Vector3(x, y, z) * radius;
                uvs[vi] = new Vector2((float)lon / segments, (float)lat / (segments / 2));
                vi++;
            }
        }

        for (int lat = 0; lat < segments / 2; lat++)
        {
            for (int lon = 0; lon < segments; lon++)
            {
                int cur = lat * (segments + 1) + lon;
                int next = cur + segments + 1;
                tris[ti++] = cur;
                tris[ti++] = next;
                tris[ti++] = cur + 1;
                tris[ti++] = cur + 1;
                tris[ti++] = next;
                tris[ti++] = next + 1;
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Mesh CreateBookshelfMesh(float depth, float height, float width)
    {
        // Board thickness
        float t = 0.04f;
        // Local space: X=depth, Y=height, Z=width
        // Origin at center of bookshelf

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        // Helper: add a box (center + size) to the mesh
        void AddBox(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            int start = verts.Count;
            float hx = sx * 0.5f, hy = sy * 0.5f, hz = sz * 0.5f;
            // 8 corners
            verts.Add(new Vector3(cx - hx, cy - hy, cz - hz));
            verts.Add(new Vector3(cx + hx, cy - hy, cz - hz));
            verts.Add(new Vector3(cx + hx, cy + hy, cz - hz));
            verts.Add(new Vector3(cx - hx, cy + hy, cz - hz));
            verts.Add(new Vector3(cx - hx, cy - hy, cz + hz));
            verts.Add(new Vector3(cx + hx, cy - hy, cz + hz));
            verts.Add(new Vector3(cx + hx, cy + hy, cz + hz));
            verts.Add(new Vector3(cx - hx, cy + hy, cz + hz));
            // 12 triangles (6 faces)
            // -Z face
            tris.Add(start+0); tris.Add(start+2); tris.Add(start+1);
            tris.Add(start+0); tris.Add(start+3); tris.Add(start+2);
            // +Z face
            tris.Add(start+4); tris.Add(start+5); tris.Add(start+6);
            tris.Add(start+4); tris.Add(start+6); tris.Add(start+7);
            // -X face
            tris.Add(start+0); tris.Add(start+4); tris.Add(start+7);
            tris.Add(start+0); tris.Add(start+7); tris.Add(start+3);
            // +X face
            tris.Add(start+1); tris.Add(start+6); tris.Add(start+5);
            tris.Add(start+1); tris.Add(start+2); tris.Add(start+6);
            // -Y face
            tris.Add(start+0); tris.Add(start+1); tris.Add(start+5);
            tris.Add(start+0); tris.Add(start+5); tris.Add(start+4);
            // +Y face
            tris.Add(start+3); tris.Add(start+7); tris.Add(start+6);
            tris.Add(start+3); tris.Add(start+6); tris.Add(start+2);
        }

        // Bottom board
        AddBox(0, -height * 0.5f + t * 0.5f, 0, depth, t, width);
        // Top board
        AddBox(0, height * 0.5f - t * 0.5f, 0, depth, t, width);
        // Left side panel (full height)
        AddBox(0, 0, -width * 0.5f + t * 0.5f, depth, height, t);
        // Right side panel (full height)
        AddBox(0, 0, width * 0.5f - t * 0.5f, depth, height, t);
        // Two middle shelves (between side panels, 3 equal layers)
        float shelfWidth = width - t * 2f;
        for (int i = 1; i <= 2; i++)
        {
            float y = -height * 0.5f + height * i / 3f;
            AddBox(0, y, 0, depth, t, shelfWidth);
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void LoadPaintingTextures()
    {
        for (int tier = 1; tier <= 3; tier++)
        {
            paintingTextures[tier] = new List<Texture2D>();
            for (int idx = 1; idx <= 3; idx++)
            {
                string name = $"{tier}-{idx}";
                Texture2D tex = null;
#if UNITY_EDITOR
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{name}.png");
#endif
                if (tex == null) tex = Resources.Load<Texture2D>($"Textures/{name}");
                if (tex != null) paintingTextures[tier].Add(tex);
            }
        }

        // Load blood textures b1-b4
        for (int i = 1; i <= 4; i++)
        {
            Texture2D tex = null;
#if UNITY_EDITOR
            tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/b{i}.png");
#endif
            if (tex == null) tex = Resources.Load<Texture2D>($"Textures/b{i}");
            if (tex != null) bloodTextures.Add(tex);
        }
    }

    void GenerateEyeTextures()
    {
        int size = 64;
        float center = size * 0.5f;

        // Sclera: white circle with faint red veins at edges
        eyeScleraTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var scleraPixels = new Color[size * size];
        float scleraR = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < scleraR)
                {
                    float edge = dist / scleraR;
                    float r = 1f;
                    float g = 1f - edge * 0.12f;
                    float b = 1f - edge * 0.15f;
                    // Random bloodshot veins near edges
                    if (edge > 0.35f && Random.value < 0.04f)
                    {
                        r = 1f; g = 0.25f; b = 0.25f;
                    }
                    scleraPixels[y * size + x] = new Color(r, g, b, 1f);
                }
                else
                {
                    scleraPixels[y * size + x] = Color.clear;
                }
            }
        }
        eyeScleraTex.SetPixels(scleraPixels);
        eyeScleraTex.Apply();

        // Pupil: dark iris + black pupil
        eyePupilTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pupilPixels = new Color[size * size];
        float irisR = size * 0.46f;
        float pupilR = size * 0.26f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < pupilR)
                {
                    pupilPixels[y * size + x] = new Color(0.02f, 0f, 0f, 1f);
                }
                else if (dist < irisR)
                {
                    float t = (dist - pupilR) / (irisR - pupilR);
                    // Dark red iris fading to darker red
                    float r = 0.5f - t * 0.3f;
                    float g = 0.02f;
                    float b = 0.02f;
                    pupilPixels[y * size + x] = new Color(r, g, b, 1f);
                }
                else
                {
                    pupilPixels[y * size + x] = Color.clear;
                }
            }
        }
        eyePupilTex.SetPixels(pupilPixels);
        eyePupilTex.Apply();
    }

    void InitEyes()
    {
        GenerateEyeTextures();

        // Target corridor segments: 0 (EntryCorridor), 3+ (after big room)
        for (int i = 0; i < segmentInfos.Count; i++)
        {
            var seg = segmentInfos[i];
            if (seg.isRoom) continue;
            if (i < 3) continue; // skip first corridor, connector, and room

            // Segment 3+: big room at start → denser toward end (further from room)
            bool denserTowardEnd = true;

            // Cluster-based spawning: tight clusters, either adjacent or far apart
            float z = 2f;
            while (z < seg.length - 0.5f)
            {
                float progress = z / seg.length;
                if (!denserTowardEnd) progress = 1f - progress;

                int clusterCount = Random.Range(20, 41);
                bool bothSides = Random.value < 0.5f;
                float clusterSpread = Random.Range(1f, 1.5f);
                float clusterCenterZ = z + clusterSpread * 0.5f;
                bool clusterSide = Random.value > 0.5f;

                for (int c = 0; c < clusterCount; c++)
                {
                    float cz = clusterCenterZ + (Random.value - 0.5f) * clusterSpread;
                    if (cz < 2f || cz >= seg.length - 0.5f) continue;

                    float py;
                    float roll = Random.value;
                    if (roll < 0.40f)
                        py = Random.Range(0.1f, 0.7f);
                    else if (roll < 0.80f)
                        py = Random.Range(2.0f, 2.9f);
                    else
                        py = Random.Range(0.8f, 2.0f);

                    CreateEye(seg, cz, clusterSide, py);
                    if (bothSides)
                        CreateEye(seg, cz, !clusterSide, py);
                }

                // Gap follows painting logic: Lerp(8, 1.5, progress) + adjacent-or-wide rule
                float baseInterval = Mathf.Lerp(8f, 1.5f, progress);
                float gap = Random.value < 0.5f ? Random.Range(0f, 0.5f) : baseInterval;
                z += clusterSpread + gap;
            }
        }

        Debug.Log($"[WallEyes] Pre-spawned {allEyes.Count} eyes (hidden).");
    }

    void CreateEye(SegmentInfo seg, float z, bool leftSide, float py)
    {
        float eyeSize = Random.Range(0.10f, 0.18f);
        float pupilRange = 0.22f;

        Vector3 wallDir = leftSide ? Vector3.left : Vector3.right;
        Vector3 pos = seg.pos
            + seg.rot * (Vector3.forward * z)
            + seg.rot * (wallDir * (seg.width * 0.5f - 0.05f))
            + seg.rot * (Vector3.up * py);

        // Sclera quad
        GameObject sclera = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sclera.name = $"EyeSclera_{z:F1}_{(leftSide ? "L" : "R")}";
        sclera.transform.SetParent(transform);
        sclera.transform.position = pos;
        sclera.transform.rotation = seg.rot * Quaternion.LookRotation(wallDir, Vector3.up);
        sclera.transform.localScale = new Vector3(eyeSize, eyeSize, 1f);
        DestroyImmediate(sclera.GetComponent<Collider>());

        Material scleraMat = new Material(Shader.Find("Unlit/Transparent"));
        scleraMat.SetTexture("_MainTex", eyeScleraTex);
        scleraMat.renderQueue = 3000;
        sclera.GetComponent<Renderer>().material = scleraMat;

        // Pupil quad (child, slightly in FRONT of sclera toward camera)
        GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pupil.name = "Pupil";
        pupil.transform.SetParent(sclera.transform, false);
        pupil.transform.localPosition = new Vector3(0, 0, 0.01f);
        pupil.transform.localScale = Vector3.one * 0.6f;
        DestroyImmediate(pupil.GetComponent<Collider>());

        Material pupilMat = new Material(Shader.Find("Unlit/Transparent"));
        pupilMat.SetTexture("_MainTex", eyePupilTex);
        pupilMat.renderQueue = 3001;
        pupil.GetComponent<Renderer>().material = pupilMat;

        sclera.SetActive(false);

        var eyeScript = sclera.AddComponent<WallEye>();
        eyeScript.pupilTransform = pupil.transform;
        eyeScript.pupilRange = pupilRange;
        eyeScript.pupilDepth = 0.01f;
        allEyes.Add(eyeScript);
    }

    void InitPaintings()
    {
        if (segmentInfos.Count < 4) return;

        PreSpawnPaintings(segmentInfos[0], 1, 2, true);
        PreSpawnPaintings(segmentInfos[3], 2, 3, false);

        // Blood in corridors (same reveal logic, sparser)
        PreSpawnBlood(segmentInfos[0]);
        PreSpawnBlood(segmentInfos[3]);

        // Blood in big room (sparse)
        if (segmentInfos.Count > 2)
            PreSpawnBloodInRoom(segmentInfos[2]);

        Debug.Log($"[Paintings] Pre-spawned {allPaintings.Count} items (hidden).");
    }

    void PreSpawnBloodInRoom(SegmentInfo seg)
    {
        if (bloodTextures.Count == 0 || !seg.isRoom) return;
        int count = Random.Range(5, 9);
        for (int i = 0; i < count; i++)
        {
            float z = Random.Range(4f, seg.length - 4f);
            bool left = Random.value > 0.5f;
            Texture2D tex = bloodTextures[Random.Range(0, bloodTextures.Count)];
            CreateDecal(seg, z, tex, left, true);
        }
    }

    void PreSpawnBlood(SegmentInfo seg)
    {
        if (bloodTextures.Count == 0) return;
        float z = 5f;
        while (z < seg.length - 1f)
        {
            float progress = z / seg.length;
            Texture2D tex = bloodTextures[Random.Range(0, bloodTextures.Count)];
            bool left = Random.value > 0.5f;
            CreateDecal(seg, z, tex, left, true);

            float interval = Mathf.Lerp(12f, 4f, progress);
            z += interval;
        }
    }

    void PreSpawnPaintings(SegmentInfo seg, int minTier, int maxTier, bool denserTowardRoom)
    {
        float z = 2f;
        int currentMin = minTier;  // tracks the highest tier seen so far, never goes down
        while (z < seg.length - 0.5f)
        {
            float progress = z / seg.length;

            // Tier: mostly minTier at start, gradually shift to maxTier
            int tier;
            if (Random.value < (1f - progress * 0.8f))
                tier = currentMin;  // use current minimum (could have been raised)
            else
                tier = maxTier;

            // Once maxTier appears, raise the floor so no lower tier follows
            if (tier >= maxTier)
                currentMin = maxTier;

            if (!paintingTextures.ContainsKey(tier) || paintingTextures[tier].Count == 0) { z += 3f; continue; }

            Texture2D tex = paintingTextures[tier][Random.Range(0, paintingTextures[tier].Count)];
            if (tex == null) { z += 3f; continue; }

            // Cluster: sometimes spawn 2-3 paintings close together
            int clusterCount = Random.value < 0.35f ? Random.Range(2, 4) : 1;

            for (int c = 0; c < clusterCount; c++)
            {
                float cz = z + c * 0.6f;
                if (cz >= seg.length - 0.5f) break;

                bool left = Random.value > 0.5f;
                CreatePainting(seg, cz, tier, tex, left);

                // Sometimes also opposite side in cluster
                if (clusterCount > 1 && Random.value < 0.4f)
                    CreatePainting(seg, cz, tier, tex, !left);
            }

            // Sparser intervals: 8m at start → 1.5m at end
            float interval = Mathf.Lerp(8f, 1.5f, progress);
            z += interval;
        }
    }

    void CreatePainting(SegmentInfo seg, float z, int tier, Texture2D tex, bool leftSide)
    {
        CreateDecal(seg, z, tex, leftSide, false);
    }

    void CreateDecal(SegmentInfo seg, float z, Texture2D tex, bool leftSide, bool isBlood)
    {
        float pw, ph;
        if (isBlood)
        {
            pw = 0.8f + Random.value * 0.6f;
            ph = 0.8f + Random.value * 0.6f;
        }
        else
        {
            pw = 0.6f + Random.value * 0.3f;
            ph = 0.7f + Random.value * 0.4f;
        }
        float py = 0.8f + (Mathf.Sin(z * 0.7f) * 0.5f + 0.5f) * 1.0f;

        Vector3 wallDir = leftSide ? Vector3.left : Vector3.right;
        Vector3 pos = seg.pos
            + seg.rot * (Vector3.forward * z)
            + seg.rot * (wallDir * (seg.width * 0.5f - 0.05f))
            + seg.rot * (Vector3.up * py);

        GameObject painting = GameObject.CreatePrimitive(PrimitiveType.Quad);
        painting.name = $"Painting_{z:F1}_{(leftSide ? "L" : "R")}";
        painting.transform.SetParent(transform);
        painting.transform.position = pos;
        painting.transform.rotation = seg.rot * Quaternion.LookRotation(wallDir, Vector3.up);
        painting.transform.localScale = new Vector3(pw, ph, 1f);
        DestroyImmediate(painting.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Unlit/Transparent"));
        mat.SetTexture("_MainTex", tex);
        painting.GetComponent<Renderer>().material = mat;

        // Red glow panel behind decal (same for paintings and blood)
        float glowX = seg.width * 0.5f - 0.01f;
        Vector3 glowPos = seg.pos
            + seg.rot * (Vector3.forward * z)
            + seg.rot * (wallDir * glowX)
            + seg.rot * (Vector3.up * py);

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = $"Glow_{z:F1}_{(leftSide ? "L" : "R")}";
        glow.transform.SetParent(transform);
        glow.transform.position = glowPos;
        glow.transform.rotation = seg.rot * Quaternion.LookRotation(wallDir, Vector3.up);
        glow.transform.localScale = new Vector3(pw * 0.9f, ph * 0.9f, 1f);
        DestroyImmediate(glow.GetComponent<Collider>());

        Material glowMat = new Material(Shader.Find("Unlit/Color"));
        glowMat.color = new Color(1f, 0.05f, 0.05f, 1f);
        glow.GetComponent<Renderer>().material = glowMat;

        // Hidden until player approaches
        painting.SetActive(false);
        glow.SetActive(false);

        allPaintings.Add(painting);
        allGlows.Add(glow);
    }

    AudioClip GenerateStaticNoise()
    {
        // Short 0.3 second burst instead of 1 second loop
        int sampleRate = 22050;
        int samples = Mathf.RoundToInt(sampleRate * 0.3f);
        AudioClip clip = AudioClip.Create("StaticBurst", samples, 1, sampleRate, false);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float env = Mathf.Exp(-i * 0.0001f);
            data[i] = (Random.value * 2f - 1f) * 0.3f * env;
        }
        clip.SetData(data, 0);
        return clip;
    }

    void Update()
    {
        if (player == null) return;

        for (int i = 0; i < allPaintings.Count; i++)
        {
            if (allPaintings[i] == null) continue;
            if (allPaintings[i].activeSelf) continue;

            float dist = Vector3.Distance(player.position, allPaintings[i].transform.position);
            if (dist < 5f)
            {
                allPaintings[i].SetActive(true);
                allGlows[i].SetActive(true);

                // One-shot沙沙 sound: play at painting position, no loop
                AudioSource.PlayClipAtPoint(GenerateStaticNoise(), allPaintings[i].transform.position, 0.3f);
            }
        }

        // Eye reveal + pupil tracking
        for (int i = 0; i < allEyes.Count; i++)
        {
            if (allEyes[i] == null) continue;
            var eyeGO = allEyes[i].gameObject;
            float dist = Vector3.Distance(player.position, eyeGO.transform.position);

            if (!eyeGO.activeSelf && dist < 6f)
                eyeGO.SetActive(true);

            if (eyeGO.activeSelf)
                allEyes[i].TrackPlayer(player.position);
        }
    }

    public void HandleTeleport(Collider other)
    {
        if (fadeActive) return;
        if (other.transform == player || other.transform.IsChildOf(player))
            StartCoroutine(FadeAndTeleport());
    }

    System.Collections.IEnumerator FadeAndTeleport()
    {
        fadeActive = true;
        float duration = 8f;
        float elapsed = 0f;
        float startFogEnd = RenderSettings.fogEndDistance;
        float startAmbient = RenderSettings.ambientIntensity;
        Color startAmbientColor = RenderSettings.ambientLight;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            RenderSettings.fog = true;
            RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, 0.3f, t);
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbient, 0f, t);
            RenderSettings.ambientLight = Color.Lerp(startAmbientColor, Color.black, t);
            yield return null;
        }

        Vector3 destination = Level0MazeGenerator.SpawnPosition;
        if (destination == Vector3.zero) destination = new Vector3(2003f, 1f, 2003f);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = destination;
        player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        if (cc != null) cc.enabled = true;

        yield return new WaitForSecondsRealtime(0.5f);

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            RenderSettings.fogEndDistance = Mathf.Lerp(0.3f, 25f, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(0f, 5f, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(0f, 0.6f, t);
            RenderSettings.ambientLight = Color.Lerp(Color.black, Color.black, t);
            yield return null;
        }

        RenderSettings.fog = true;
        RenderSettings.fogStartDistance = 5f;
        RenderSettings.fogEndDistance = 25f;
        RenderSettings.fogColor = Color.black;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0.6f;
        fadeActive = false;
    }

    void PositionPlayer()
    {
        if (player == null) return;

        // Start inside the spawn room (center of the 3x3 room behind T-junction)
        player.position = new Vector3(0, 1f, -(corridorWidth * 0.5f + 1.5f));
        player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    }

    void SetupMonsterIntegration()
    {
        // 1. Bake NavMesh — covers all walkable floors, walls are obstacles
        var navSurface = gameObject.GetComponent<NavMeshSurface>();
        if (navSurface == null)
            navSurface = gameObject.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.Children;
        navSurface.BuildNavMesh();

        // 2. Create MonsterSpawner with spawn points at corridor start (behind player)
        float cw = corridorWidth;
        var spawnerObj = new GameObject("MonsterSpawner");
        var leftSpawn = new GameObject("SpawnPoint_Left");
        leftSpawn.transform.SetParent(spawnerObj.transform, false);
        leftSpawn.transform.position = new Vector3(-15f, 0, 0f);
        var rightSpawn = new GameObject("SpawnPoint_Right");
        rightSpawn.transform.SetParent(spawnerObj.transform, false);
        rightSpawn.transform.position = new Vector3(15f, 0, 0f);
        spawnerObj.AddComponent<MonsterSpawner>();

        // 3. Create ExitTrigger at T-junction — fires when player enters corridor
        var triggerObj = new GameObject("ExitTrigger");
        triggerObj.transform.SetParent(transform, false);
        triggerObj.transform.position = new Vector3(0, 1.5f, 0);
        var triggerCol = triggerObj.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(cw, 3f, cw);
        triggerObj.AddComponent<ExitTrigger>();

        // 4. Create maze entry trigger — when player enters maze, monsters stop chasing
        var mazeEntryObj = new GameObject("MazeEntryTrigger");
        mazeEntryObj.transform.SetParent(transform, false);
        mazeEntryObj.transform.position = mazeEntryPos + mazeEntryRot * Vector3.forward * 2f;
        mazeEntryObj.transform.rotation = mazeEntryRot;
        var mazeEntryCol = mazeEntryObj.AddComponent<BoxCollider>();
        mazeEntryCol.isTrigger = true;
        mazeEntryCol.size = new Vector3(cw, 3f, 2f);
        mazeEntryObj.AddComponent<MazeEntryTrigger>();
    }
}

public class MazeEntryTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Deactivate all monsters
            var monsters = FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                m.isActive = false;
            }
            Debug.Log("[MazeEntryTrigger] 玩家进入迷宫，怪物停止追击。");
        }
    }
}

public class WallEye : MonoBehaviour
{
    public Transform pupilTransform;
    public float pupilRange = 0.04f;
    public float pupilDepth = -0.01f;
    public float trackLerpSpeed = 8f;

    private Vector3 pupilLocalTarget;

    public void TrackPlayer(Vector3 playerPos)
    {
        if (pupilTransform == null) return;

        Vector3 toPlayer = playerPos - transform.position;
        Vector3 localDir = transform.InverseTransformDirection(toPlayer);

        // Local X = along corridor, Local Y = up/down, Local Z = into room
        Vector2 dir = new Vector2(localDir.x, localDir.y);
        float distFactor = Mathf.Clamp01(toPlayer.magnitude / 20f);
        // Pupil moves more when player is closer
        float range = pupilRange * (1f - distFactor * 0.3f);

        if (dir.sqrMagnitude > 0.0001f)
            dir.Normalize();
        else
            dir = Vector2.zero;

        pupilLocalTarget = new Vector3(dir.x * range, dir.y * range, pupilDepth);

        pupilTransform.localPosition = Vector3.Lerp(
            pupilTransform.localPosition,
            pupilLocalTarget,
            Time.deltaTime * trackLerpSpeed);
    }
}
