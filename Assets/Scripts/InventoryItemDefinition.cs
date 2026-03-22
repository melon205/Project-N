using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Project-N/Inventory/Item Definition")]
public class InventoryItemDefinition : ScriptableObject
{
    public enum ItemCategory
    {
        KeyItem,
        Material,
        Consumable,
        Equipment,
        Quest,
        Crafted,
        Misc
    }

    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private ItemCategory category = ItemCategory.Misc;
    [SerializeField] private bool stackable = true;
    [SerializeField] private int maxStack = 99;
    [SerializeField] private int sortOrder;
    [SerializeField] private string[] craftingTags;
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [TextArea(1, 3)]
    [SerializeField] private string obtainHint;
    [TextArea(1, 3)]
    [SerializeField] private string usageHint;
    [TextArea(1, 3)]
    [SerializeField] private string craftingHint;
    [SerializeField] private Sprite icon;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
    public ItemCategory Category => category;
    public bool Stackable => stackable;
    public int MaxStack => Mathf.Max(1, maxStack);
    public int SortOrder => sortOrder;
    public string[] CraftingTags => craftingTags ?? System.Array.Empty<string>();
    public string Description => description;
    public string ObtainHint => obtainHint;
    public string UsageHint => usageHint;
    public string CraftingHint => craftingHint;
    public Sprite Icon => icon;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = name;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = itemId;
        }

        maxStack = Mathf.Max(1, maxStack);
    }
#endif
}
