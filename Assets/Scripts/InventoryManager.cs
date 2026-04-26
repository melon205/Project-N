using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yarn.Unity;

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager instance;
    public static event Action InventoryChanged;

    [Header("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Database")]
    [SerializeField] private InventoryItemDefinition[] itemDefinitions;

    private readonly Dictionary<string, InventoryItemDefinition> definitionsById = new Dictionary<string, InventoryItemDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> itemQuantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
        return HasItem(itemId, 1);
    }

    public bool HasItem(string itemId, int quantity)
    {
        if (quantity <= 0 || !TryResolveItemId(itemId, out string resolvedItemId))
        {
            return false;
        }

        Initialize();
        return itemQuantities.TryGetValue(resolvedItemId, out int ownedQuantity) && ownedQuantity >= quantity;
    }

    public bool AddItem(string itemId)
    {
        return AddItem(itemId, 1);
    }

    public bool AddItem(string itemId, int quantity)
    {
        if (quantity <= 0)
        {
            return false;
        }

        Initialize();
        if (!TryResolveItemId(itemId, out string resolvedItemId))
        {
            return false;
        }

        int currentQuantity = GetQuantityInternal(resolvedItemId);
        int updatedQuantity = ClampQuantityForDefinition(resolvedItemId, currentQuantity + quantity);
        int addedQuantity = Mathf.Max(0, updatedQuantity - currentQuantity);
        SetResolvedQuantity(resolvedItemId, updatedQuantity);
        NotifyInventoryChanged();

        if (addedQuantity > 0)
        {
            ShowInventoryToast(resolvedItemId, addedQuantity, true);
        }

        return true;
    }

    public bool RemoveItem(string itemId)
    {
        return RemoveItem(itemId, 1);
    }

    public bool RemoveItem(string itemId, int quantity)
    {
        if (quantity <= 0)
        {
            return false;
        }

        Initialize();
        if (!TryResolveItemId(itemId, out string resolvedItemId) || !itemQuantities.TryGetValue(resolvedItemId, out int currentQuantity))
        {
            return false;
        }

        int removedQuantity = Mathf.Min(currentQuantity, quantity);
        SetResolvedQuantity(resolvedItemId, currentQuantity - quantity);
        NotifyInventoryChanged();

        if (removedQuantity > 0)
        {
            ShowInventoryToast(resolvedItemId, removedQuantity, false);
        }

        return true;
    }

    public void SetOwnedItems(IEnumerable<string> itemIds)
    {
        Initialize();
        ClearQuantities();

        if (itemIds == null)
        {
            NotifyInventoryChanged();
            return;
        }

        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                ApplyQuantityChange(itemId, 1);
            }
        }

        NotifyInventoryChanged();
    }

    public string[] GetOwnedItemIds()
    {
        Initialize();
        return itemQuantities
            .Where(entry => entry.Value > 0)
            .Select(entry => entry.Key)
            .ToArray();
    }

    public int GetQuantity(string itemId)
    {
        if (!TryResolveItemId(itemId, out string resolvedItemId))
        {
            return 0;
        }

        Initialize();
        return GetQuantityInternal(resolvedItemId);
    }

    public bool SetQuantity(string itemId, int quantity)
    {
        Initialize();
        if (!TryResolveItemId(itemId, out string resolvedItemId))
        {
            return false;
        }

        SetResolvedQuantity(resolvedItemId, ClampQuantityForDefinition(resolvedItemId, quantity));
        NotifyInventoryChanged();
        return true;
    }

    public IReadOnlyList<InventoryItemStack> GetOwnedStacks()
    {
        Initialize();

        List<InventoryItemStack> stacks = new List<InventoryItemStack>(itemQuantities.Count);
        foreach (KeyValuePair<string, int> entry in itemQuantities)
        {
            if (entry.Value <= 0)
            {
                continue;
            }

            if (TryGetItemDefinitionInternal(entry.Key, out InventoryItemDefinition definition) && definition != null)
            {
                stacks.Add(new InventoryItemStack(definition, entry.Value));
            }
            else
            {
                stacks.Add(new InventoryItemStack(entry.Key, entry.Value));
            }
        }

        stacks.Sort(CompareStacks);
        return stacks;
    }

    public string GetItemDisplayName(string itemId)
    {
        Initialize();

        if (!TryResolveItemId(itemId, out string resolvedItemId))
        {
            return string.Empty;
        }

        if (definitionsById.TryGetValue(resolvedItemId, out InventoryItemDefinition definition) && definition != null)
        {
            return definition.DisplayName;
        }

        return resolvedItemId;
    }

    public bool TryGetItemDefinition(string itemId, out InventoryItemDefinition definition)
    {
        Initialize();

        if (!TryResolveItemId(itemId, out string resolvedItemId))
        {
            definition = null;
            return false;
        }

        return TryGetItemDefinitionInternal(resolvedItemId, out definition);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        RebuildDefinitionLookup();
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

            definitionsById[NormalizeItemId(definition.ItemId)] = definition;
        }
    }

    public List<ItemSaveData> GetSaveData()
    {
        Initialize();
        List<ItemSaveData> data = new List<ItemSaveData>();
        foreach (var entry in itemQuantities)
        {
            if (entry.Value > 0)
            {
                data.Add(new ItemSaveData(entry.Key, entry.Value));
            }
        }
        return data;
    }

    public void LoadSaveData(List<ItemSaveData> data)
    {
        Initialize();
        ClearQuantities();

        if (data != null)
        {
            foreach (var item in data)
            {
                if (!string.IsNullOrWhiteSpace(item.itemId))
                {
                    SetResolvedQuantity(NormalizeItemId(item.itemId), item.quantity);
                }
            }
        }

        NotifyInventoryChanged();
    }

    private string NormalizeItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return itemId.Trim();
    }

    private bool TryResolveItemId(string itemId, out string resolvedItemId)
    {
        resolvedItemId = NormalizeItemId(itemId);
        return !string.IsNullOrWhiteSpace(resolvedItemId);
    }

    private int ClampQuantityForDefinition(string resolvedItemId, int quantity)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        if (TryGetItemDefinitionInternal(resolvedItemId, out InventoryItemDefinition definition) && definition != null && definition.Stackable)
        {
            return Mathf.Min(quantity, definition.MaxStack);
        }

        return quantity;
    }

    private void SetResolvedQuantity(string resolvedItemId, int quantity)
    {
        if (quantity > 0)
        {
            itemQuantities[resolvedItemId] = quantity;
        }
        else
        {
            itemQuantities.Remove(resolvedItemId);
        }
    }

    private bool ApplyQuantityChange(string itemId, int quantityDelta)
    {
        if (!TryResolveItemId(itemId, out string resolvedItemId))
        {
            return false;
        }

        int currentQuantity = GetQuantityInternal(resolvedItemId);
        int updatedQuantity = ClampQuantityForDefinition(resolvedItemId, currentQuantity + quantityDelta);
        if (updatedQuantity == currentQuantity)
        {
            return false;
        }

        SetResolvedQuantity(resolvedItemId, updatedQuantity);
        return true;
    }

    private static int CompareStacks(InventoryItemStack left, InventoryItemStack right)
    {
        InventoryItemDefinition leftDefinition = left?.ItemDefinition;
        InventoryItemDefinition rightDefinition = right?.ItemDefinition;

        int leftSort = leftDefinition != null ? leftDefinition.SortOrder : int.MaxValue;
        int rightSort = rightDefinition != null ? rightDefinition.SortOrder : int.MaxValue;
        int sortComparison = leftSort.CompareTo(rightSort);
        if (sortComparison != 0)
        {
            return sortComparison;
        }

        string leftName = leftDefinition != null ? leftDefinition.DisplayName : left?.ItemId;
        string rightName = rightDefinition != null ? rightDefinition.DisplayName : right?.ItemId;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke();
        if (SaveManager.Instance != null && Application.isPlaying)
        {
            SaveManager.Instance.SaveGame();
        }
    }

    public void ClearInventory()
    {
        Initialize();
        ClearQuantities();
        NotifyInventoryChanged();
    }

    public void SetInventory(IEnumerable<InventoryItemStack> stacks)
    {
        Initialize();
        ClearQuantities();

        if (stacks != null)
        {
            foreach (InventoryItemStack stack in stacks)
            {
                if (stack == null || !TryResolveItemId(stack.ItemId, out string resolvedItemId))
                {
                    continue;
                }

                SetResolvedQuantity(resolvedItemId, ClampQuantityForDefinition(resolvedItemId, stack.Quantity));
            }
        }

        NotifyInventoryChanged();
    }

    private void ClearQuantities()
    {
        itemQuantities.Clear();
    }

    private void ShowInventoryToast(string resolvedItemId, int quantity, bool gained)
    {
        MainDirectorPresenter.ShowInventoryToast(GetItemDisplayName(resolvedItemId), quantity, gained);
    }

    private int GetQuantityInternal(string itemId)
    {
        return itemQuantities.TryGetValue(itemId, out int quantity) ? quantity : 0;
    }

    private bool TryGetItemDefinitionInternal(string itemId, out InventoryItemDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            definition = null;
            return false;
        }

        string resolvedItemId = NormalizeItemId(itemId);
        return definitionsById.TryGetValue(resolvedItemId, out definition) && definition != null;
    }
}

[System.Serializable]
public class InventoryItemStack
{
    [SerializeField] private InventoryItemDefinition itemDefinition;
    [HideInInspector]
    [SerializeField] private string itemId;
    [Min(1)]
    [SerializeField] private int quantity = 1;

    public InventoryItemDefinition ItemDefinition => itemDefinition;
    public string ItemId => itemDefinition != null ? itemDefinition.ItemId : itemId;
    public int Quantity => Mathf.Max(1, quantity);

    public InventoryItemStack(string resolvedItemId, int amount)
    {
        itemId = resolvedItemId;
        quantity = Mathf.Max(1, amount);
    }

    public InventoryItemStack(InventoryItemDefinition definition, int amount)
    {
        itemDefinition = definition;
        itemId = definition != null ? definition.ItemId : string.Empty;
        quantity = Mathf.Max(1, amount);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (itemDefinition != null)
        {
            itemId = itemDefinition.ItemId;
        }

        quantity = Mathf.Max(1, quantity);
    }
#endif
}

public static class InventoryYarnBridge
{
    private static bool TryGetManager(string action, string itemId, out InventoryManager manager)
    {
        manager = InventoryManager.Instance;
        if (manager != null)
        {
            return true;
        }

        Debug.LogWarning($"Cannot {action} item '{itemId}' because no InventoryManager exists in the scene.");
        return false;
    }

    [YarnFunction("has_item")]
    public static bool HasItem(string itemId)
    {
        return InventoryManager.Instance != null && InventoryManager.Instance.HasItem(itemId);
    }

    [YarnFunction("item_count")]
    public static float ItemCount(string itemId)
    {
        return InventoryManager.Instance != null ? InventoryManager.Instance.GetQuantity(itemId) : 0f;
    }

    [YarnCommand("give_item")]
    public static void GiveItem(string itemId, int quantity = 1)
    {
        if (!TryGetManager("give", itemId, out InventoryManager manager))
        {
            return;
        }

        manager.AddItem(itemId, quantity);
    }

    [YarnCommand("remove_item")]
    public static void RemoveItem(string itemId, int quantity = 1)
    {
        if (!TryGetManager("remove", itemId, out InventoryManager manager))
        {
            return;
        }

        manager.RemoveItem(itemId, quantity);
    }

    [YarnCommand("use_item")]
    public static void UseItem(string itemId, int quantity = 1)
    {
        if (!TryGetManager("use", itemId, out InventoryManager manager))
        {
            return;
        }

        if (!manager.RemoveItem(itemId, quantity))
        {
            Debug.LogWarning($"Cannot use item '{itemId}' because it is not in the inventory.");
        }
    }
}
