using UnityEngine;
using Yarn.Unity;

public static class InventoryYarnBridge
{
    [YarnFunction("has_item")]
    public static bool HasItem(string itemId)
    {
        return InventoryManager.Instance != null && InventoryManager.Instance.HasItem(itemId);
    }

    [YarnCommand("give_item")]
    public static void GiveItem(string itemId)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning($"Cannot give item '{itemId}' because no InventoryManager exists in the scene.");
            return;
        }

        InventoryManager.Instance.AddItem(itemId);
    }

    [YarnCommand("remove_item")]
    public static void RemoveItem(string itemId)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning($"Cannot remove item '{itemId}' because no InventoryManager exists in the scene.");
            return;
        }

        InventoryManager.Instance.RemoveItem(itemId);
    }
}
