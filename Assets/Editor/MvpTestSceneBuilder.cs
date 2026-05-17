using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MvpTestSceneBuilder
{
    private const string RootName = "MVP1_TestLayout";

    [MenuItem("Metroidvania/MVP 1/Create Test Layout")]
    public static void CreateTestLayout()
    {
        EnsureLayers();

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Replace MVP Test Layout",
                "An MVP test layout already exists. Replace it?",
                "Replace",
                "Cancel");

            if (!replace)
            {
                return;
            }

            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(RootName);

        Room roomA = CreateRoom(root.transform, "Room_A", new Vector3(0f, 0f, 0f), new Vector2Int(1, 2));
        Room roomB = CreateRoom(root.transform, "Room_B", new Vector3(32f, 0f, 0f), new Vector2Int(1, 1));
        Room roomC = CreateRoom(root.transform, "Room_C", new Vector3(32f, 16f, 0f), new Vector2Int(1, 1));

        CreateSpawn(roomA.transform, "start", new Vector3(4f, 2f, 0f), GameDirection.Right);
        CreateSpawn(roomA.transform, "from_right", new Vector3(30f, 2f, 0f), GameDirection.Left);
        CreateSpawn(roomB.transform, "from_left_lower", new Vector3(34f, 2f, 0f), GameDirection.Right);
        CreateSpawn(roomC.transform, "from_left_upper", new Vector3(34f, 18f, 0f), GameDirection.Right);

        roomA.ConfigureForEditor("Room_A", new Vector2Int(1, 2), new List<RoomExit>());
        roomB.ConfigureForEditor("Room_B", new Vector2Int(1, 1), new List<RoomExit>());
        roomC.ConfigureForEditor("Room_C", new Vector2Int(1, 1), new List<RoomExit>());

        CreateRoomContents(roomA, 0f, 0f, true);
        CreateRoomContents(roomB, 32f, 0f, false);
        CreateRoomContents(roomC, 32f, 16f, false);

        GameObject player = CreatePlayer(root.transform);
        GameObject cameraRig = CreateCameraRig(root.transform);
        RoomManager roomManager = GetOrCreateRoomManager();

        SerializedObject managerObject = new SerializedObject(roomManager);
        managerObject.FindProperty("startingRoom").objectReferenceValue = roomA;
        managerObject.FindProperty("player").objectReferenceValue = player.GetComponent<MCController>();
        managerObject.FindProperty("cameraRig").objectReferenceValue = cameraRig.GetComponent<CamParent>();
        managerObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
    }

    private static RoomManager GetOrCreateRoomManager()
    {
        RoomManager existingRoomManager = Object.FindObjectOfType<RoomManager>();
        if (existingRoomManager != null)
        {
            return existingRoomManager;
        }

        GameController gameController = Object.FindObjectOfType<GameController>();
        GameObject gameControllerObject;
        if (gameController != null)
        {
            gameControllerObject = gameController.gameObject;
        }
        else
        {
            gameControllerObject = new GameObject("GameController");
            gameController = gameControllerObject.AddComponent<GameController>();
        }

        RoomManager roomManager = gameControllerObject.GetComponent<RoomManager>();
        if (roomManager == null)
        {
            roomManager = gameControllerObject.AddComponent<RoomManager>();
        }

        return roomManager;
    }

    private static Room CreateRoom(Transform parent, string roomName, Vector3 position, Vector2Int size)
    {
        GameObject roomObject = new GameObject(roomName);
        roomObject.transform.SetParent(parent);
        roomObject.transform.position = position;
        Room room = roomObject.AddComponent<Room>();
        room.ConfigureForEditor(roomName, size, new List<RoomExit>());
        return room;
    }

    private static void CreateSpawn(Transform parent, string spawnId, Vector3 position, int facingDirection)
    {
        GameObject spawnObject = new GameObject("Spawn_" + spawnId);
        spawnObject.transform.SetParent(parent);
        spawnObject.transform.position = position;
        RoomSpawnPoint spawnPoint = spawnObject.AddComponent<RoomSpawnPoint>();

        SerializedObject spawnObjectSerialized = new SerializedObject(spawnPoint);
        spawnObjectSerialized.FindProperty("spawnId").stringValue = spawnId;
        spawnObjectSerialized.FindProperty("facingDirection").intValue = GameDirection.NormalizeOrDefault(facingDirection);
        spawnObjectSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateRoomContents(Room room, float originX, float originY, bool addCheckpoint)
    {
        CreateBox(room.transform, "Ground", GameLayers.Ground, new Vector3(originX + 16f, originY + 0.5f, 0f), new Vector2(32f, 1f), false);
        CreateBox(room.transform, "Obstacle", GameLayers.Obstacle, new Vector3(originX + 12f, originY + 3f, 0f), new Vector2(2f, 4f), false);
        CreateBox(room.transform, "Hazard", GameLayers.Hazard, new Vector3(originX + 22f, originY + 1.25f, 0f), new Vector2(3f, 0.5f), true);

        GameObject platform = CreateBox(room.transform, "OneWayPlatform", GameLayers.Platform, new Vector3(originX + 18f, originY + 6f, 0f), new Vector2(6f, 0.5f), false);
        platform.AddComponent<PlatformConfig>();

        CreateBox(room.transform, "EnemyPlaceholder", GameLayers.Enemy, new Vector3(originX + 26f, originY + 2f, 0f), new Vector2(1f, 2f), true);

        if (addCheckpoint)
        {
            GameObject checkpoint = CreateBox(room.transform, "Checkpoint", GameLayers.Trigger, new Vector3(originX + 6f, originY + 2f, 0f), new Vector2(1f, 2f), true);
            checkpoint.AddComponent<Checkpoint>();
        }
    }

    private static GameObject CreatePlayer(Transform parent)
    {
        GameObject player = CreateBox(parent, "Player", GameLayers.Player, new Vector3(4f, 2f, 0f), new Vector2(1f, 2f), false);
        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        player.AddComponent<MCController>();
        player.AddComponent<PlayerRespawn>();
        return player;
    }

    private static GameObject CreateCameraRig(Transform parent)
    {
        GameObject rig = new GameObject("CameraRig");
        rig.transform.SetParent(parent);
        rig.transform.position = new Vector3(16f, 8f, -10f);
        rig.AddComponent<CamParent>();

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(rig.transform);
        cameraObject.transform.localPosition = Vector3.zero;
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7f;
        cameraObject.tag = "MainCamera";
        return rig;
    }

    private static GameObject CreateBox(Transform parent, string name, string layerName, Vector3 position, Vector2 size, bool isTrigger)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;

        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            go.layer = layer;
        }

        BoxCollider2D collider2D = go.AddComponent<BoxCollider2D>();
        collider2D.size = size;
        collider2D.isTrigger = isTrigger;
        return go;
    }

    private static void EnsureLayers()
    {
        string[] layers =
        {
            GameLayers.Player,
            GameLayers.Ground,
            GameLayers.Obstacle,
            GameLayers.Platform,
            GameLayers.Trigger,
            GameLayers.Enemy,
            GameLayers.Hazard,
            GameLayers.PlayerBullet,
            GameLayers.EnemyBullet
        };

        for (int i = 0; i < layers.Length; i++)
        {
            EnsureLayer(layers[i]);
        }
    }

    private static void EnsureLayer(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) >= 0)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        Debug.LogWarning("No empty user layer slot available for " + layerName);
    }
}
