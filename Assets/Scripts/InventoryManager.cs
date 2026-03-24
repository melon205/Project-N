using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager instance;
    public static event Action InventoryChanged;

    [Header("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Database")]
    [SerializeField] private InventoryItemDefinition[] itemDefinitions;

    [Header("Starting Items")]
    [SerializeField] private InventoryItemStack[] startingInventory;
    [SerializeField] private InventoryItemDefinition[] startingItems;

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
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        Initialize();
        string resolvedItemId = NormalizeItemId(itemId);
        return itemQuantities.TryGetValue(resolvedItemId, out int ownedQuantity) && ownedQuantity >= quantity;
    }

    public bool AddItem(string itemId)
    {
        return AddItem(itemId, 1);
    }

    public bool AddItem(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        Initialize();
        string resolvedItemId = NormalizeItemId(itemId);
        if (string.IsNullOrWhiteSpace(resolvedItemId))
        {
            return false;
        }

        int currentQuantity = GetQuantityInternal(resolvedItemId);
        int updatedQuantity = currentQuantity + quantity;

        if (TryGetItemDefinitionInternal(resolvedItemId, out InventoryItemDefinition definition) && definition != null && definition.Stackable)
        {
            updatedQuantity = Mathf.Min(updatedQuantity, definition.MaxStack);
        }

        itemQuantities[resolvedItemId] = Mathf.Max(0, updatedQuantity);
        NotifyInventoryChanged();
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        return RemoveItem(itemId, 1);
    }

    public bool RemoveItem(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        Initialize();
        string resolvedItemId = NormalizeItemId(itemId);
        if (string.IsNullOrWhiteSpace(resolvedItemId) || itemQuantities.TryGetValue(resolvedItemId, out int currentQuantity) == false)
        {
            return false;
        }

        int updatedQuantity = currentQuantity - quantity;
        if (updatedQuantity > 0)
        {
            itemQuantities[resolvedItemId] = updatedQuantity;
        }
        else
        {
            itemQuantities.Remove(resolvedItemId);
        }

        NotifyInventoryChanged();
        return true;
    }

    public void SetOwnedItems(IEnumerable<string> itemIds)
    {
        Initialize();
        itemQuantities.Clear();

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
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        Initialize();
        string resolvedItemId = NormalizeItemId(itemId);
        return GetQuantityInternal(resolvedItemId);
    }

    public bool SetQuantity(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        Initialize();
        string resolvedItemId = NormalizeItemId(itemId);
        if (string.IsNullOrWhiteSpace(resolvedItemId))
        {
            return false;
        }

        if (quantity > 0)
        {
            if (TryGetItemDefinitionInternal(resolvedItemId, out InventoryItemDefinition definition) && definition != null && definition.Stackable)
            {
                quantity = Mathf.Min(quantity, definition.MaxStack);
            }

            itemQuantities[resolvedItemId] = quantity;
        }
        else
        {
            itemQuantities.Remove(resolvedItemId);
        }

        NotifyInventoryChanged();
        return true;
    }

    public IReadOnlyList<InventoryItemStack> GetOwnedStacks()
    {
        Initialize();

        List<InventoryItemStack> stacks = new List<InventoryItemStack>();
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

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        string resolvedItemId = NormalizeItemId(itemId);
        if (definitionsById.TryGetValue(resolvedItemId, out InventoryItemDefinition definition) && definition != null)
        {
            return definition.DisplayName;
        }

        return resolvedItemId;
    }

    public bool TryGetItemDefinition(string itemId, out InventoryItemDefinition definition)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(itemId))
        {
            definition = null;
            return false;
        }

        return TryGetItemDefinitionInternal(itemId, out definition);
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

            definitionsById[NormalizeItemId(definition.ItemId)] = definition;
        }
    }

    private void AddStartingItems()
    {
        bool changed = false;

        if (startingInventory != null)
        {
            for (int i = 0; i < startingInventory.Length; i++)
            {
                InventoryItemStack stack = startingInventory[i];
                if (stack == null || string.IsNullOrWhiteSpace(stack.ItemId))
                {
                    continue;
                }

                ApplyQuantityChange(stack.ItemId, stack.Quantity);
                changed = true;
            }
        }

        if (startingItems == null)
        {
            if (changed)
            {
                NotifyInventoryChanged();
            }
            return;
        }

        for (int i = 0; i < startingItems.Length; i++)
        {
            InventoryItemDefinition definition = startingItems[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            ApplyQuantityChange(definition.ItemId, 1);
            changed = true;
        }

        if (changed)
        {
            NotifyInventoryChanged();
        }
    }

    private string NormalizeItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return itemId.Trim();
    }

    private void ApplyQuantityChange(string itemId, int quantityDelta)
    {
        string resolvedItemId = NormalizeItemId(itemId);
        if (string.IsNullOrWhiteSpace(resolvedItemId))
        {
            return;
        }

        int currentQuantity = GetQuantityInternal(resolvedItemId);
        int updatedQuantity = currentQuantity + quantityDelta;

        if (TryGetItemDefinitionInternal(resolvedItemId, out InventoryItemDefinition definition) && definition != null && definition.Stackable && updatedQuantity > 0)
        {
            updatedQuantity = Mathf.Min(updatedQuantity, definition.MaxStack);
        }

        if (updatedQuantity > 0)
        {
            itemQuantities[resolvedItemId] = updatedQuantity;
        }
        else
        {
            itemQuantities.Remove(resolvedItemId);
        }
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
    }

    public void ClearInventory()
    {
        Initialize();
        itemQuantities.Clear();
        NotifyInventoryChanged();
    }

    public void SetInventory(IEnumerable<InventoryItemStack> stacks)
    {
        Initialize();
        itemQuantities.Clear();

        if (stacks != null)
        {
            foreach (InventoryItemStack stack in stacks)
            {
                if (stack == null || string.IsNullOrWhiteSpace(stack.ItemId))
                {
                    continue;
                }

                string resolvedItemId = NormalizeItemId(stack.ItemId);
                int quantity = Mathf.Max(1, stack.Quantity);

                if (TryGetItemDefinitionInternal(resolvedItemId, out InventoryItemDefinition definition) && definition != null && definition.Stackable)
                {
                    quantity = Mathf.Min(quantity, definition.MaxStack);
                }

                itemQuantities[resolvedItemId] = quantity;
            }
        }

        NotifyInventoryChanged();
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
