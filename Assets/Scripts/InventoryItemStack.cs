using UnityEngine;

[System.Serializable]
public class InventoryItemStack
{
    [SerializeField] private InventoryItemDefinition itemDefinition;
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
