using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Project-N/Inventory/Item Definition")]
public class InventoryItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
    public string Description => description;
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
    }
#endif
}
