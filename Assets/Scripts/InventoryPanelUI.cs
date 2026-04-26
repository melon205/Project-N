using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private InventoryItemCardView cardPrefab;
    [SerializeField] private TMP_Text emptyStateText;

    private readonly List<InventoryItemCardView> cardPool = new List<InventoryItemCardView>();

    private void Awake()
    {
        ResolveManager();
    }

    private void OnEnable()
    {
        InventoryManager.InventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        InventoryManager.InventoryChanged -= Refresh;
    }

    [ContextMenu("Refresh Inventory UI")]
    public void Refresh()
    {
        ResolveManager();

        if (inventoryManager == null || contentRoot == null || cardPrefab == null)
        {
            SetEmptyState(true, "Inventory UI is not configured.");
            HideAllCards();
            return;
        }

        IReadOnlyList<InventoryItemStack> stacks = inventoryManager.GetOwnedStacks();
        bool hasItems = stacks != null && stacks.Count > 0;

        SetEmptyState(!hasItems, "No items in inventory.");

        if (!hasItems)
        {
            HideAllCards();
            return;
        }

        EnsurePoolSize(stacks.Count);
        BindVisibleCards(stacks);
    }

    private void EnsurePoolSize(int requiredCount)
    {
        while (cardPool.Count < requiredCount)
        {
            InventoryItemCardView card = Instantiate(cardPrefab, contentRoot);
            card.gameObject.SetActive(false);
            cardPool.Add(card);
        }
    }

    private void ResolveManager()
    {
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
        }
    }

    private void BindVisibleCards(IReadOnlyList<InventoryItemStack> stacks)
    {
        for (int i = 0; i < cardPool.Count; i++)
        {
            InventoryItemCardView card = cardPool[i];
            bool shouldShow = i < stacks.Count;

            if (!shouldShow)
            {
                HideCard(card);
                continue;
            }

            card.gameObject.SetActive(true);
            card.Bind(stacks[i]);
        }
    }

    private void HideAllCards()
    {
        for (int i = 0; i < cardPool.Count; i++)
        {
            HideCard(cardPool[i]);
        }
    }

    private static void HideCard(InventoryItemCardView card)
    {
        if (card == null)
        {
            return;
        }

        card.Clear();
        card.gameObject.SetActive(false);
    }

    private void SetEmptyState(bool visible, string message)
    {
        if (emptyStateText == null)
        {
            return;
        }

        emptyStateText.gameObject.SetActive(visible);
        emptyStateText.text = message;
    }
}
