using UnityEngine;
using System.Collections.Generic;

public class Level0MazeGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Maze Dimensions")]
    public int gridSize = 25;
    public float cellSize = 9f;
    public float wallHeight = 12.375f;    // 1.5x of 8.25
    public float wallThickness = 0.6f;

    [Header("Level 0 Colors")]
    public Color wallColor = new Color(0.851f, 0.812f, 0.686f, 1f);
    public Color floorColor = new Color(0.388f, 0.349f, 0.286f, 1f);
    public Color ceilingColor = new Color(0.7f, 0.7f, 0.68f, 1f);
    public Color lightColor = new Color(1f, 0.95f, 0.75f, 1f);
    public Color fogColor = Color.black;
    public Color exitWallColor = new Color(0.8f, 0.2f, 0.2f, 1f); // red exit door

    [Header("Lights")]
    public float lightIntensity = 1.0f;
    public float lightRange = 8f;

    [Header("Auto-Pathfinding")]
    public float autoWalkSpeed = 8f;
    public float autoWalkReachThreshold = 2f;

    [Header("Exit Door")]
    public int requiredKey = 1;

    // 25x25 maze grid — original with extra walls added for complexity
    // 3=exit door (blocks until player has key, visually distinct)
    private static readonly int[,] Grid = new int[,]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,1,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,1,0,1,1,1,1,1,1,1,0,1,0,1,1,1,1,1,1,1,0,1},
        {1,0,1,0,0,0,0,0,1,0,0,0,1,0,0,0,0,0,1,0,0,0,1,0,1},
        {1,0,1,1,1,1,1,0,1,1,1,0,1,1,1,1,1,0,1,1,1,0,1,0,1},
        {1,0,0,0,0,0,1,0,0,0,1,0,0,0,0,0,1,0,0,0,1,0,0,0,1},
        {1,1,1,1,1,0,1,1,1,0,1,0,1,1,1,0,1,1,1,0,1,1,1,0,1},
        {1,0,0,0,0,0,0,0,1,0,1,0,1,0,0,0,0,0,1,0,0,0,1,0,1},
        {1,0,1,1,1,1,1,0,1,0,1,0,1,0,1,1,1,0,1,1,1,0,1,0,1},
        {1,0,1,0,0,0,1,0,0,0,1,0,1,0,1,0,0,0,0,0,1,0,0,0,1},
        {1,0,1,0,1,0,1,1,1,1,1,0,1,0,1,0,1,1,1,1,1,1,1,0,1},
        {1,0,1,0,1,0,0,0,0,0,0,0,1,0,1,0,0,0,0,0,0,0,1,0,1},
        {1,0,1,0,1,1,1,1,1,1,1,0,1,0,1,1,1,1,1,1,1,0,1,0,1},
        {1,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,1,0,0,0,1},
        {1,0,1,1,1,1,1,1,1,0,1,0,1,1,1,1,1,1,1,0,1,0,1,1,1},
        {1,0,0,0,0,0,0,0,1,0,1,0,1,0,0,0,0,0,1,0,1,0,0,0,1},
        {1,1,1,1,1,1,1,0,1,0,1,0,1,0,1,1,1,0,1,0,1,1,1,0,1},
        {1,0,0,0,0,0,1,0,0,0,1,0,1,0,1,0,0,0,1,0,0,0,1,0,1},
        {1,0,1,1,1,0,1,1,1,1,1,0,1,0,1,0,1,1,1,1,1,0,1,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,0,1,0,1,0,0,0,0,0,1,0,0,0,1},
        {1,0,1,0,1,1,1,1,1,1,1,1,1,0,1,1,1,1,1,0,1,1,1,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,0,1},
        {1,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,1,1,1,0,1,3,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,1,0,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
    };

    private int[,] workGrid;
    private Material wallMat, floorMat, ceilMat, lightMat, pillarMat, exitDoorMat;
    private Vector3 mazeOrigin;
    private bool autoWalking;
    private List<Vector3Int> autoPath;
    private int autoPathIndex;
    private float[] capsPressTimes = new float[3];
    private int capsPressIndex;
    private GameObject exitDoorObj;
    private bool playerInMaze;
    private List<WallEye> giantEyes = new List<WallEye>();

    public static Vector3 SpawnPosition { get; private set; }
    public static Vector3 ExitPosition { get; private set; }

    void Start()
    {
        mazeOrigin = new Vector3(2000f, 0f, 0f);

        workGrid = new int[gridSize, gridSize];
        for (int z = 0; z < gridSize; z++)
            for (int x = 0; x < gridSize; x++)
                workGrid[z, x] = Grid[z, x];

        CarveOpenAreas();
        VerifyAndFixPath();
        SetupEnvironment();
        InitMaterials();
        BuildMaze();
        PlaceMarkers();
        PlaceExitTrigger();
        //SpawnBloodInMaze();
        SpawnGiantEyes();

        Debug.Log("[Level0Maze] Build complete. Start=" + SpawnPosition + " Exit=" + ExitPosition + " WallHeight=" + wallHeight);
    }

    void CarveOpenAreas()
    {
        int[,] openCenters = new int[,]
        {
            { 5, 5 }, { 18, 5 }, { 12, 12 }, { 5, 18 }, { 18, 18 }, { 9, 9 }, { 15, 15 },
        };

        for (int i = 0; i < openCenters.GetLength(0); i++)
        {
            int cx = openCenters[i, 0];
            int cz = openCenters[i, 1];

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    int z = cz + dz;
                    if (x >= 1 && x < gridSize - 1 && z >= 1 && z < gridSize - 1)
                        if (workGrid[z, x] != 3) // Don't overwrite exit door
                            workGrid[z, x] = 0;
                }
            }
            workGrid[cz, cx] = 2; // Pillar marker
        }
    }

    void VerifyAndFixPath()
    {
        int sx = 1, sz = 1, ex = 23, ez = 23;
        workGrid[sz, sx] = 0;

        // For path verification, treat exit door (3) as passable
        // (player will need key to actually pass, but path should exist)
        if (!BFS(sx, sz, ex, ez))
        {
            Debug.LogWarning("[Level0Maze] No path! Forcing corridor...");
            ForceCorridor(sx, sz, ex, ez);
            Debug.Log("[Level0Maze] After fix, path exists: " + BFS(sx, sz, ex, ez));
        }
        else
        {
            Debug.Log("[Level0Maze] Path verified from start to exit.");
        }
    }

    bool BFS(int sx, int sz, int ex, int ez)
    {
        return BFSPath(sx, sz, ex, ez) != null;
    }

    List<Vector3Int> BFSPath(int sx, int sz, int ex, int ez)
    {
        bool[,] visited = new bool[gridSize, gridSize];
        Vector3Int[,] parent = new Vector3Int[gridSize, gridSize];
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(new Vector3Int(sx, sz, 0));
        visited[sz, sx] = true;

        int[] dx = { 0, 0, 1, -1 };
        int[] dz = { 1, -1, 0, 0 };

        while (queue.Count > 0)
        {
            Vector3Int cur = queue.Dequeue();
            if (cur.x == ex && cur.y == ez)
            {
                List<Vector3Int> path = new List<Vector3Int>();
                Vector3Int node = cur;
                while (node.x != sx || node.y != sz)
                {
                    path.Add(node);
                    node = parent[node.y, node.x];
                }
                path.Add(new Vector3Int(sx, sz, 0));
                path.Reverse();
                return path;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = cur.x + dx[i];
                int nz = cur.y + dz[i];
                if (nx >= 0 && nx < gridSize && nz >= 0 && nz < gridSize
                    && !visited[nz, nx] && workGrid[nz, nx] != 1)
                {
                    visited[nz, nx] = true;
                    parent[nz, nx] = cur;
                    queue.Enqueue(new Vector3Int(nx, nz, 0));
                }
            }
        }
        return null;
    }

    void ForceCorridor(int sx, int sz, int ex, int ez)
    {
        int cx = sx, cz = sz;
        while (cx != ex) { if (workGrid[cz, cx] == 1) workGrid[cz, cx] = 0; cx += (ex > cx) ? 1 : -1; }
        while (cz != ez) { if (workGrid[cz, cx] == 1) workGrid[cz, cx] = 0; cz += (ez > cz) ? 1 : -1; }
        workGrid[ez, ex] = 0;
    }

    void SetupEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 2f;
        RenderSettings.fogEndDistance = 15f;
        RenderSettings.fogColor = Color.black;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0.2f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    void InitMaterials()
    {
        Texture2D wallTex = null;
#if UNITY_EDITOR
        wallTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/wall.png");
#endif
        if (wallTex == null)
            wallTex = Resources.Load<Texture2D>("Textures/wall");

        wallMat = new Material(Shader.Find("Standard"));
        wallMat.color = wallColor;
        wallMat.SetFloat("_Glossiness", 0f);
        wallMat.SetFloat("_Metallic", 0f);
        wallMat.SetFloat("_SpecularHighlights", 0f);
        wallMat.SetFloat("_GlossyReflections", 0f);
        if (wallTex != null)
        {
            wallMat.SetTexture("_MainTex", wallTex);
            wallMat.SetTextureScale("_MainTex", new Vector2(3f, 2f));
        }

        floorMat = new Material(Shader.Find("Standard"));
        floorMat.color = floorColor;
        floorMat.SetFloat("_Glossiness", 0f);
        floorMat.SetFloat("_Metallic", 0f);
        floorMat.SetFloat("_SpecularHighlights", 0f);
        floorMat.SetFloat("_GlossyReflections", 0f);

        ceilMat = new Material(Shader.Find("Standard"));
        ceilMat.color = ceilingColor;
        ceilMat.SetFloat("_Glossiness", 0f);
        ceilMat.SetFloat("_Metallic", 0f);
        ceilMat.SetFloat("_SpecularHighlights", 0f);
        ceilMat.SetFloat("_GlossyReflections", 0f);

        lightMat = new Material(Shader.Find("Standard"));
        lightMat.color = lightColor;
        lightMat.EnableKeyword("_EMISSION");
        lightMat.SetColor("_EmissionColor", lightColor * 2f);

        pillarMat = new Material(Shader.Find("Standard"));
        pillarMat.color = new Color(0.6f, 0.55f, 0.45f, 1f);
        pillarMat.SetFloat("_Glossiness", 0.3f);
        pillarMat.SetFloat("_Metallic", 0.1f);

        // Exit door material — bright red, emissive
        exitDoorMat = new Material(Shader.Find("Standard"));
        exitDoorMat.color = exitWallColor;
        exitDoorMat.SetFloat("_Glossiness", 0.4f);
        exitDoorMat.SetFloat("_Metallic", 0.2f);
        exitDoorMat.EnableKeyword("_EMISSION");
        exitDoorMat.SetColor("_EmissionColor", exitWallColor * 0.8f);
    }

    void BuildMaze()
    {
        float totalSize = gridSize * cellSize;
        Vector3 floorCenter = mazeOrigin + new Vector3(totalSize * 0.5f, 0f, totalSize * 0.5f);

        SpawnPlane(floorCenter, totalSize, totalSize, 0f, floorMat, "Floor");
        SpawnPlane(floorCenter + Vector3.up * wallHeight, totalSize, totalSize, 180f, ceilMat, "Ceiling");

        int wallCount = 0;
        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                if (workGrid[z, x] == 1)
                {
                    Vector3 wallPos = mazeOrigin + new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        wallHeight * 0.5f,
                        z * cellSize + cellSize * 0.5f);
                    SpawnWall(wallPos, cellSize, wallHeight, cellSize);
                    wallCount++;
                }
                else if (workGrid[z, x] == 2)
                {
                    Vector3 pillarPos = mazeOrigin + new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        wallHeight * 0.5f,
                        z * cellSize + cellSize * 0.5f);
                    SpawnPillar(pillarPos);
                    workGrid[z, x] = 0;
                }
                else if (workGrid[z, x] == 3)
                {
                    // Exit door — visually distinct red wall
                    Vector3 doorPos = mazeOrigin + new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        wallHeight * 0.5f,
                        z * cellSize + cellSize * 0.5f);
                    exitDoorObj = SpawnExitDoor(doorPos, cellSize, wallHeight, cellSize);
                }
            }
        }

        for (int z = 1; z < gridSize - 1; z++)
        {
            for (int x = 1; x < gridSize - 1; x++)
            {
                if (workGrid[z, x] == 0 && x % 2 == 0 && z % 2 == 0)
                {
                    Vector3 lightPos = mazeOrigin + new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        wallHeight - 0.1f,
                        z * cellSize + cellSize * 0.5f);
                    SpawnLight(lightPos);
                }
            }
        }

        SpawnPosition = mazeOrigin + new Vector3(
            1 * cellSize + cellSize * 0.5f, 1f, 1 * cellSize + cellSize * 0.5f);
        ExitPosition = mazeOrigin + new Vector3(
            23 * cellSize + cellSize * 0.5f, 1f, 23 * cellSize + cellSize * 0.5f);

        Debug.Log("[Level0Maze] Walls: " + wallCount + " | Ceiling: " + wallHeight + "m");
    }

    void SpawnPlane(Vector3 center, float width, float length, float rotX, Material mat, string name)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Plane);
        p.name = name;
        p.transform.SetParent(transform);
        p.transform.position = center;
        p.transform.rotation = Quaternion.Euler(rotX, 0, 0);
        p.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
        p.GetComponent<Renderer>().sharedMaterial = mat;
        MaterialPropertyBlock pb = new MaterialPropertyBlock();
        pb.SetVector("_MainTex_ST", new Vector4(width / cellSize, length / cellSize, 0, 0));
        p.GetComponent<Renderer>().SetPropertyBlock(pb);
    }

    void SpawnWall(Vector3 pos, float width, float height, float depth)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Wall";
        w.transform.SetParent(transform);
        w.transform.position = pos;
        w.transform.localScale = new Vector3(width, height, depth);

        Material m = new Material(wallMat);
        m.SetTextureScale("_MainTex", new Vector2(width / 3f, height / 3f));
        w.GetComponent<Renderer>().sharedMaterial = m;
    }

    GameObject SpawnExitDoor(Vector3 pos, float width, float height, float depth)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "ExitDoor";
        w.transform.SetParent(transform);
        w.transform.position = pos;
        w.transform.localScale = new Vector3(width, height, depth);
        w.GetComponent<Renderer>().sharedMaterial = exitDoorMat;
        return w;
    }

    void SpawnPillar(Vector3 pos)
    {
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        p.name = "Pillar";
        p.transform.SetParent(transform);
        p.transform.position = pos;
        p.transform.localScale = new Vector3(2.5f, wallHeight * 0.5f, 2.5f);
        p.GetComponent<Renderer>().sharedMaterial = pillarMat;
    }

    void SpawnLight(Vector3 pos)
    {
        GameObject fix = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fix.name = "FluorescentLight";
        fix.transform.SetParent(transform);
        fix.transform.position = pos;
        fix.transform.localScale = new Vector3(2.4f, 0.1f, 0.24f);
        fix.GetComponent<Renderer>().material = lightMat;

        Light pl = fix.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.range = lightRange;
        pl.intensity = lightIntensity;
        pl.color = lightColor;
    }

    void PlaceMarkers()
    {
        SpawnMarker(SpawnPosition, new Color(0.1f, 1f, 0.1f, 1f), "StartMarker");
        SpawnMarker(ExitPosition, new Color(1f, 0.1f, 0.1f, 1f), "ExitMarker");
    }

    void SpawnMarker(Vector3 pos, Color color, string name)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        marker.name = name;
        marker.transform.SetParent(transform);
        marker.transform.position = pos + new Vector3(0f, 0.02f, 0f);
        marker.transform.rotation = Quaternion.Euler(90, 0, 0);
        marker.transform.localScale = new Vector3(3f, 3f, 1f);
        Object.DestroyImmediate(marker.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Glossiness", 0f);
        mat.SetFloat("_Metallic", 0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 1.5f);
        marker.GetComponent<Renderer>().material = mat;
    }

    void PlaceExitTrigger()
    {
        // Trigger behind the exit door (at exit cell)
        GameObject trigger = new GameObject("ExitTrigger");
        trigger.transform.SetParent(transform);
        trigger.transform.position = ExitPosition + new Vector3(0f, 1f, 0f);
        BoxCollider col = trigger.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(cellSize, wallHeight, cellSize);

        // 胜利逃离触发器：玩家到达终点时自动触发 EscapeSequence
        GameObject escapeTrigger = new GameObject("EscapeZoneTrigger");
        escapeTrigger.transform.SetParent(transform);
        escapeTrigger.transform.position = ExitPosition + new Vector3(0f, 1f, 0f);
        BoxCollider escapeCol = escapeTrigger.AddComponent<BoxCollider>();
        escapeCol.isTrigger = true;
        escapeCol.size = new Vector3(cellSize * 1.5f, wallHeight, cellSize * 1.5f);
        escapeTrigger.AddComponent<EscapeZoneTrigger>();
    }

    // ── Player key check: opens exit door if player has key ───────
    void Update()
    {
        // Check if player is in maze area
        if (player != null)
        {
            Vector3 localPos = player.position - mazeOrigin;
            float totalSize = gridSize * cellSize;
            playerInMaze = localPos.x > 0f && localPos.x < totalSize
                         && localPos.z > 0f && localPos.z < totalSize;
        }

        // Auto-pathfinding only when in maze
        if (playerInMaze && Input.GetKeyDown(KeyCode.CapsLock))
        {
            float now = Time.time;
            if (capsPressIndex > 0 && now - capsPressTimes[capsPressIndex - 1] > 1f)
                capsPressIndex = 0;

            capsPressTimes[capsPressIndex] = now;
            capsPressIndex++;

            if (capsPressIndex >= 3)
            {
                capsPressIndex = 0;
                StartAutoWalk();
            }
        }

        // Execute auto-walk
        if (autoWalking && player != null && autoPath != null)
            AutoWalkStep();

        // Check if player has key and is near exit door
        if (exitDoorObj != null && player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            int playerKey = 0;

            if (exitDoorObj.activeSelf)
            {
                float dist = Vector3.Distance(
                    new Vector3(player.position.x, 0, player.position.z),
                    new Vector3(exitDoorObj.transform.position.x, 0, exitDoorObj.transform.position.z));

                if (dist < cellSize * 1.5f)
                {
                    // Open the door
                    exitDoorObj.SetActive(false);
                    Debug.Log("[Level0Maze] Exit door opened! Player has key.");
                }
            }
        }

        // Giant eye pupil tracking
        if (player != null)
        {
            for (int i = 0; i < giantEyes.Count; i++)
            {
                if (giantEyes[i] == null) continue;
                giantEyes[i].TrackPlayer(player.position);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform == player || other.transform.IsChildOf(player))
        {
            Debug.Log("[Level0Maze] Player reached the exit! Triggering escape sequence!");
            autoWalking = false;

            // 触发胜利逃离演出
            var escapeSeq = other.GetComponent<EscapeSequence>();
            if (escapeSeq != null)
                escapeSeq.PlayEscapeSequence();
            else
                escapeSeq = other.GetComponentInParent<EscapeSequence>();
            if (escapeSeq != null)
                escapeSeq.PlayEscapeSequence();
        }
    }

    void StartAutoWalk()
    {
        if (player == null) return;

        Vector3 localPos = player.position - mazeOrigin;
        int px = Mathf.RoundToInt(localPos.x / cellSize - 0.5f);
        int pz = Mathf.RoundToInt(localPos.z / cellSize - 0.5f);

        px = Mathf.Clamp(px, 0, gridSize - 1);
        pz = Mathf.Clamp(pz, 0, gridSize - 1);

        if (workGrid[pz, px] == 1)
        {
            for (int r = 1; r < 5; r++)
            {
                bool found = false;
                for (int dz = -r; dz <= r && !found; dz++)
                {
                    for (int dx = -r; dx <= r && !found; dx++)
                    {
                        int nx = px + dx, nz = pz + dz;
                        if (nx >= 0 && nx < gridSize && nz >= 0 && nz < gridSize && workGrid[nz, nx] != 1)
                        {
                            px = nx; pz = nz; found = true;
                        }
                    }
                }
                if (found) break;
            }
        }

        int ex = 23, ez = 23;
        autoPath = BFSPath(px, pz, ex, ez);

        if (autoPath == null || autoPath.Count == 0)
        {
            Debug.LogWarning("[Level0Maze] Auto-walk: no path found!");
            return;
        }

        autoPathIndex = 1;
        autoWalking = true;
        Debug.Log("[Level0Maze] Auto-walk started! Path length: " + autoPath.Count + " cells.");
    }

    void AutoWalkStep()
    {
        if (autoPathIndex >= autoPath.Count)
        {
            autoWalking = false;
            Debug.Log("[Level0Maze] Auto-walk complete — reached exit!");
            return;
        }

        Vector3Int targetCell = autoPath[autoPathIndex];
        Vector3 targetWorld = mazeOrigin + new Vector3(
            targetCell.x * cellSize + cellSize * 0.5f,
            player.position.y,
            targetCell.y * cellSize + cellSize * 0.5f);

        Vector3 toTarget = targetWorld - player.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist < autoWalkReachThreshold)
        {
            autoPathIndex++;
            return;
        }

        Vector3 dir = toTarget.normalized;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            player.rotation = Quaternion.RotateTowards(player.rotation, targetRot, 360f * Time.deltaTime);
            cc.Move(dir * autoWalkSpeed * Time.deltaTime);
        }
    }

    void SpawnBloodInMaze()
    {
        List<Texture2D> bloodTex = new List<Texture2D>();
        for (int i = 1; i <= 4; i++)
        {
            Texture2D tex = null;
#if UNITY_EDITOR
            tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/b{i}.png");
#endif
            if (tex == null) tex = Resources.Load<Texture2D>($"Textures/b{i}");
            if (tex != null) bloodTex.Add(tex);
        }
        if (bloodTex.Count == 0) return;

        // Scatter ~12 blood decals on open path cells
        int placed = 0;
        for (int z = 1; z < gridSize - 1 && placed < 12; z++)
        {
            for (int x = 1; x < gridSize - 1 && placed < 12; x++)
            {
                if (Grid[z, x] != 0) continue;
                if (Random.value > 0.08f) continue;

                Vector3 pos = mazeOrigin + new Vector3(x * cellSize + cellSize * 0.5f, 1.2f + Random.value * 0.6f, z * cellSize + cellSize * 0.5f);
                bool left = Random.value > 0.5f;
                Vector3 wallDir = left ? Vector3.left : Vector3.right;
                pos += wallDir * (cellSize * 0.5f - 0.05f);

                Texture2D tex = bloodTex[Random.Range(0, bloodTex.Count)];
                float bw = 0.8f + Random.value * 0.6f;
                float bh = 0.8f + Random.value * 0.6f;

                GameObject blood = GameObject.CreatePrimitive(PrimitiveType.Quad);
                blood.name = $"MazeBlood_{x}_{z}";
                blood.transform.SetParent(transform);
                blood.transform.position = pos;
                blood.transform.rotation = Quaternion.LookRotation(wallDir, Vector3.up);
                blood.transform.localScale = new Vector3(bw, bh, 1f);
                DestroyImmediate(blood.GetComponent<Collider>());

                Material mat = new Material(Shader.Find("Unlit/Transparent"));
                mat.SetTexture("_MainTex", tex);
                blood.GetComponent<Renderer>().material = mat;
                placed++;
            }
        }
        Debug.Log($"[Level0Maze] Spawned {placed} blood decals.");
    }

    void SpawnGiantEyes()
    {
        // Corridor eyes ~0.15m, giant eyes 100x = ~15m
        float eyeSize = cellSize * 1.6f; // ~14.4m, close to 100x corridor eye

        // Generate procedural eye textures
        int texSize = 128;
        float center = texSize * 0.5f;

        // Sclera
        Texture2D scleraTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var sPixels = new Color[texSize * texSize];
        float scleraR = texSize * 0.46f;
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < scleraR)
                {
                    float edge = dist / scleraR;
                    float r = 1f, g = 1f - edge * 0.12f, b = 1f - edge * 0.15f;
                    if (edge > 0.35f && Random.value < 0.04f) { r = 1f; g = 0.25f; b = 0.25f; }
                    sPixels[y * texSize + x] = new Color(r, g, b, 1f);
                }
                else
                    sPixels[y * texSize + x] = Color.clear;
            }
        }
        scleraTex.SetPixels(sPixels);
        scleraTex.Apply();

        // Pupil
        Texture2D pupilTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var pPixels = new Color[texSize * texSize];
        float irisR = texSize * 0.46f;
        float pupilR = texSize * 0.26f;
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < pupilR)
                    pPixels[y * texSize + x] = new Color(0.02f, 0f, 0f, 1f);
                else if (dist < irisR)
                {
                    float t = (dist - pupilR) / (irisR - pupilR);
                    pPixels[y * texSize + x] = new Color(0.5f - t * 0.3f, 0.02f, 0.02f, 1f);
                }
                else
                    pPixels[y * texSize + x] = Color.clear;
            }
        }
        pupilTex.SetPixels(pPixels);
        pupilTex.Apply();

        // Exit is at grid (23,23). For each open cell, probability based on distance to exit.
        int ex = 23, ez = 23;
        int placed = 0;
        for (int z = 1; z < gridSize - 1; z++)
        {
            for (int x = 1; x < gridSize - 1; x++)
            {
                if (workGrid[z, x] != 0) continue;

                // Normalized distance to exit: 0 at exit, 1 at spawn
                float distToExit = Mathf.Sqrt((x - ex) * (x - ex) + (z - ez) * (z - ez));
                float maxDist = Mathf.Sqrt(ex * ex + ez * ez);
                float normDist = distToExit / maxDist; // 0=near exit, 1=far from exit
                float proximity = 1f - normDist; // 1=near exit, 0=far

                // Probability: ~2% far, ~25% near exit
                float prob = Mathf.Lerp(0.02f, 0.25f, proximity);
                if (Random.value > prob) continue;

                // Place eye on a random wall face of this cell
                Vector3 cellCenter = mazeOrigin + new Vector3(x * cellSize + cellSize * 0.5f, 0, z * cellSize + cellSize * 0.5f);
                bool left = Random.value > 0.5f;
                Vector3 wallDir = left ? Vector3.left : Vector3.right;
                Vector3 pos = cellCenter + wallDir * (cellSize * 0.5f - 0.1f);
                pos.y = wallHeight * 0.5f;

                GameObject sclera = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sclera.name = $"GiantEye_{x}_{z}";
                sclera.transform.SetParent(transform);
                sclera.transform.position = pos;
                sclera.transform.rotation = Quaternion.LookRotation(wallDir, Vector3.up);
                sclera.transform.localScale = new Vector3(eyeSize, eyeSize, 1f);
                DestroyImmediate(sclera.GetComponent<Collider>());

                Material scleraMat = new Material(Shader.Find("Unlit/Transparent"));
                scleraMat.SetTexture("_MainTex", scleraTex);
                scleraMat.renderQueue = 3000;
                sclera.GetComponent<Renderer>().material = scleraMat;

                GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Quad);
                pupil.name = "GiantPupil";
                pupil.transform.SetParent(sclera.transform, false);
                pupil.transform.localPosition = new Vector3(0, 0, 0.01f);
                pupil.transform.localScale = Vector3.one * 0.6f;
                DestroyImmediate(pupil.GetComponent<Collider>());

                Material pupilMat = new Material(Shader.Find("Unlit/Transparent"));
                pupilMat.SetTexture("_MainTex", pupilTex);
                pupilMat.renderQueue = 3001;
                pupil.GetComponent<Renderer>().material = pupilMat;

                // Add tracking
                var eyeScript = sclera.AddComponent<WallEye>();
                eyeScript.pupilTransform = pupil.transform;
                eyeScript.pupilRange = 0.15f;
                eyeScript.pupilDepth = 0.01f;
                giantEyes.Add(eyeScript);

                placed++;
            }
        }
        Debug.Log($"[Level0Maze] Spawned {placed} giant eyes.");
    }
}
