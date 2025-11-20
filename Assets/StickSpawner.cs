using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class StickSpawner : NetworkBehaviour
{
    [Header("Stick Prefab")]
    public GameObject stickPrefab;

    [Header("Spawn Settings")]
    public int maxStickCount = 10;
    public float spawnInterval = 5f;
    public float spawnRadius = 50f;
    public Vector3 spawnCenter = Vector3.zero;
    public LayerMask groundLayer;

    private List<GameObject> activeSticks = new List<GameObject>();
    private float spawnTimer = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only spawn sticks on the server
        if (IsServer)
        {
            // Spawn initial sticks
            SpawnInitialSticks();
        }
    }

    void Update()
    {
        // Only run on server
        if (!IsServer)
            return;

        // Clean up null references (destroyed sticks)
        activeSticks.RemoveAll(stick => stick == null);

        // Update spawn timer
        spawnTimer += Time.deltaTime;

        // Check if we should spawn a new stick
        if (spawnTimer >= spawnInterval && activeSticks.Count < maxStickCount)
        {
            SpawnSingleStick();
            spawnTimer = 0f;
        }
    }

    void SpawnInitialSticks()
    {
        if (stickPrefab == null)
        {
            Debug.LogError("[StickSpawner] Stick prefab not assigned!");
            return;
        }

        // Check if stick prefab has NetworkObject component
        NetworkObject prefabNetObj = stickPrefab.GetComponent<NetworkObject>();
        if (prefabNetObj == null)
        {
            Debug.LogError("[StickSpawner] Stick prefab must have a NetworkObject component!");
            return;
        }

        for (int i = 0; i < maxStickCount; i++)
        {
            SpawnSingleStick();
        }

        Debug.Log($"[StickSpawner] Server spawned {maxStickCount} initial sticks");
    }

    void SpawnSingleStick()
    {
        if (stickPrefab == null)
        {
            Debug.LogError("[StickSpawner] Stick prefab not assigned!");
            return;
        }

        Vector3 spawnPosition = FindRandomGroundPosition();

        if (spawnPosition != Vector3.zero)
        {
            // Instantiate and spawn as network object
            GameObject stickObj = Instantiate(stickPrefab, spawnPosition, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            NetworkObject netObj = stickObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                activeSticks.Add(stickObj);
                Debug.Log($"[StickSpawner] Spawned stick. Active count: {activeSticks.Count}/{maxStickCount}");
            }
        }
        else
        {
            Debug.LogWarning("[StickSpawner] Could not spawn stick - no valid position found");
        }
    }

    Vector3 FindRandomGroundPosition()
    {
        // Try multiple times to find a valid spawn position
        for (int attempt = 0; attempt < 30; attempt++)
        {
            // Random position in a circle around spawn center
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = spawnCenter + new Vector3(randomCircle.x, 100f, randomCircle.y);

            // Raycast down to find ground
            RaycastHit hit;
            if (Physics.Raycast(randomPos, Vector3.down, out hit, 200f, groundLayer))
            {
                // Add a small offset so stick sits on top of ground
                return hit.point + Vector3.up * 0.1f;
            }
        }

        Debug.LogWarning("[StickSpawner] Could not find valid spawn position");
        return Vector3.zero;
    }
}
