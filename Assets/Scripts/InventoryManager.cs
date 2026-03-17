using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager instance;

    [Header("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Database")]
    [SerializeField] private InventoryItemDefinition[] itemDefinitions;

    [Header("Starting Items")]
    [SerializeField] private InventoryItemDefinition[] startingItems;

    private readonly Dictionary<string, InventoryItemDefinition> definitionsById = new Dictionary<string, InventoryItemDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ownedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool initialized;

    public static InventoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<InventoryManager>();
            }

            return instance;
        }
    }

    public static string GetDisplayNameOrId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        InventoryManager manager = Instance;
        if (manager == null)
        {
            return itemId;
        }

        return manager.GetItemDisplayName(itemId);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        Initialize();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        RebuildDefinitionLookup();
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        Initialize();
        return ownedItemIds.Contains(itemId);
    }

    public bool AddItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        Initialize();
        return ownedItemIds.Add(itemId);
    }

    public bool RemoveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        Initialize();
        return ownedItemIds.Remove(itemId);
    }

    public void SetOwnedItems(IEnumerable<string> itemIds)
    {
        Initialize();
        ownedItemIds.Clear();

        if (itemIds == null)
        {
            return;
        }

        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                ownedItemIds.Add(itemId);
            }
        }
    }

    public string[] GetOwnedItemIds()
    {
        Initialize();
        string[] results = new string[ownedItemIds.Count];
        ownedItemIds.CopyTo(results);
        return results;
    }

    public string GetItemDisplayName(string itemId)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        if (definitionsById.TryGetValue(itemId, out InventoryItemDefinition definition) && definition != null)
        {
            return definition.DisplayName;
        }

        return itemId;
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        RebuildDefinitionLookup();
        AddStartingItems();
        initialized = true;
    }

    private void RebuildDefinitionLookup()
    {
        definitionsById.Clear();

        if (itemDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < itemDefinitions.Length; i++)
        {
            InventoryItemDefinition definition = itemDefinitions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            definitionsById[definition.ItemId] = definition;
        }
    }

    private void AddStartingItems()
    {
        if (startingItems == null)
        {
            return;
        }

        for (int i = 0; i < startingItems.Length; i++)
        {
            InventoryItemDefinition definition = startingItems[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            ownedItemIds.Add(definition.ItemId);
        }
    }
}
