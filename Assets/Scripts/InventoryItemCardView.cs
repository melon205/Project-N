using System.Text;
using TMPro;
using UnityEngine;

public class InventoryItemCardView : MonoBehaviour
{
    private const string EmptyDescriptionText = "Description: -";

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
        string additionalInfo = BuildAdditionalInfo(definition);

        SetText(itemNameText, itemName);
        SetText(quantityText, $"x{stack.Quantity}");
        SetText(descriptionText, string.IsNullOrWhiteSpace(description) ? EmptyDescriptionText : $"Description: {description}");
        SetOptionalText(additionalInfoText, additionalInfo);
    }

    public void Clear()
    {
        SetText(itemNameText, string.Empty);
        SetText(quantityText, string.Empty);
        SetText(descriptionText, string.Empty);
        SetOptionalText(additionalInfoText, string.Empty);
    }

    private static string BuildAdditionalInfo(InventoryItemDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        AppendLabeledLine(builder, "Obtain", definition.ObtainHint);
        AppendLabeledLine(builder, "Use", definition.UsageHint);
        AppendLabeledLine(builder, "Craft", definition.CraftingHint);

        return builder.ToString();
    }

    private static void AppendLabeledLine(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(label).Append(": ").Append(value);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
        {
            return;
        }

        target.text = value;
    }

    private static void SetOptionalText(TMP_Text target, string value)
    {
        if (target == null)
        {
            return;
        }

        bool hasValue = !string.IsNullOrWhiteSpace(value);
        target.text = hasValue ? value : string.Empty;
        target.gameObject.SetActive(hasValue);
    }
}
