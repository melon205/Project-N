using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftRecipeCardView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text recipeNameText;
    [SerializeField] private TMP_Text ingredientsText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("State")]
    [SerializeField] private float craftableAlpha = 1f;
    [SerializeField] private float unavailableAlpha = 0.45f;

    public void Initialize(CanvasGroup group, Button clickButton, TMP_Text recipeName, TMP_Text ingredients, TMP_Text description)
    {
        canvasGroup = group;
        button = clickButton;
        recipeNameText = recipeName;
        ingredientsText = ingredients;
        descriptionText = description;
        EnsureCanvasGroup();
        EnsureButton();
    }

    public void Bind(CraftRecipeDefinition recipe, bool canCraft, Action<CraftRecipeDefinition> onSelected)
    {
        if (recipe == null)
        {
            Clear();
            return;
        }

        SetText(recipeNameText, recipe.DisplayName);
        SetText(ingredientsText, BuildIngredientsText(recipe));
        SetText(descriptionText, string.IsNullOrWhiteSpace(recipe.Description) ? "설명 없음" : recipe.Description);
        ApplyVisualState(canCraft);
        ConfigureButton(recipe, canCraft, onSelected);
    }

    public void Clear()
    {
        SetText(recipeNameText, string.Empty);
        SetText(ingredientsText, string.Empty);
        SetText(descriptionText, string.Empty);
        ApplyVisualState(true);

        Button resolvedButton = EnsureButton();
        if (resolvedButton != null)
        {
            resolvedButton.onClick.RemoveAllListeners();
            resolvedButton.interactable = false;
        }
    }

    private void ApplyVisualState(bool canCraft)
    {
        CanvasGroup resolvedCanvasGroup = EnsureCanvasGroup();
        if (resolvedCanvasGroup == null)
        {
            return;
        }

        resolvedCanvasGroup.alpha = canCraft ? craftableAlpha : unavailableAlpha;
        resolvedCanvasGroup.interactable = canCraft;
        resolvedCanvasGroup.blocksRaycasts = true;
    }

    private void ConfigureButton(CraftRecipeDefinition recipe, bool canCraft, Action<CraftRecipeDefinition> onSelected)
    {
        Button resolvedButton = EnsureButton();
        if (resolvedButton == null)
        {
            return;
        }

        resolvedButton.onClick.RemoveAllListeners();
        resolvedButton.interactable = canCraft && recipe != null && onSelected != null;

        if (resolvedButton.interactable)
        {
            resolvedButton.onClick.AddListener(() => onSelected(recipe));
        }
    }

    private Button EnsureButton()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }

        if (button.targetGraphic == null)
        {
            button.targetGraphic = GetComponent<Graphic>();
        }

        return button;
    }

    private CanvasGroup EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private static string BuildIngredientsText(CraftRecipeDefinition recipe)
    {
        InventoryItemStack[] ingredients = recipe.Ingredients;
        if (ingredients == null || ingredients.Length == 0)
        {
            return "필요 재료 없음";
        }

        StringBuilder builder = new StringBuilder("필요 재료: ");
        bool hasIngredient = false;

        for (int i = 0; i < ingredients.Length; i++)
        {
            InventoryItemStack ingredient = ingredients[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.ItemId))
            {
                continue;
            }

            if (hasIngredient)
            {
                builder.Append(", ");
            }

            builder.Append(InventoryManager.GetDisplayNameOrId(ingredient.ItemId))
                .Append(" x")
                .Append(ingredient.Quantity);

            hasIngredient = true;
        }

        return hasIngredient ? builder.ToString() : "필요 재료 없음";
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
        {
            return;
        }

        target.text = value;
    }
}
