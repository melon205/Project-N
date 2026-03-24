using System.Text;
using TMPro;
using UnityEngine;

public class InventoryItemCardView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text additionalInfoText;

    public void Bind(InventoryItemStack stack)
    {
        if (stack == null)
        {
            Clear();
            return;
        }

        InventoryItemDefinition definition = stack.ItemDefinition;
        string itemName = definition != null ? definition.DisplayName : stack.ItemId;
        string description = definition != null ? definition.Description : string.Empty;

        if (itemNameText != null)
        {
            itemNameText.text = itemName;
        }

        if (quantityText != null)
        {
            quantityText.text = $"x{stack.Quantity}";
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrWhiteSpace(description) ? "Description: -" : $"Description: {description}";
        }

        if (additionalInfoText != null)
        {
            string additionalInfo = BuildAdditionalInfo(definition);
            additionalInfoText.text = string.IsNullOrWhiteSpace(additionalInfo) ? string.Empty : additionalInfo;
            additionalInfoText.gameObject.SetActive(!string.IsNullOrWhiteSpace(additionalInfo));
        }
    }

    public void Clear()
    {
        if (itemNameText != null)
        {
            itemNameText.text = string.Empty;
        }

        if (quantityText != null)
        {
            quantityText.text = string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
        }

        if (additionalInfoText != null)
        {
            additionalInfoText.text = string.Empty;
            additionalInfoText.gameObject.SetActive(false);
        }
    }

    private static string BuildAdditionalInfo(InventoryItemDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(definition.ObtainHint))
        {
            builder.Append("Obtain: ").Append(definition.ObtainHint);
        }

        if (!string.IsNullOrWhiteSpace(definition.UsageHint))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append("Use: ").Append(definition.UsageHint);
        }

        if (!string.IsNullOrWhiteSpace(definition.CraftingHint))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append("Craft: ").Append(definition.CraftingHint);
        }

        return builder.ToString();
    }
}
