using UnityEngine;

[CreateAssetMenu(fileName = "CraftRecipe", menuName = "Project-N/Crafting/Recipe Definition")]
public class CraftRecipeDefinition : ScriptableObject
{
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [SerializeField] private InventoryItemStack[] ingredients;
    [SerializeField] private InventoryItemStack[] results;
    [SerializeField] private int sortOrder;

    [SerializeField] private string recipeId;

    public string RecipeId => string.IsNullOrWhiteSpace(recipeId) ? name : recipeId;

    public string DisplayName
    {
        get
        {
            if (results != null && results.Length > 0 && results[0] != null && results[0].ItemDefinition != null)
            {
                return results[0].ItemDefinition.DisplayName;
            }
            return RecipeId;
        }
    }

    public string Description => description;
    public InventoryItemStack[] Ingredients => ingredients ?? System.Array.Empty<InventoryItemStack>();
    public InventoryItemStack[] Results => results ?? System.Array.Empty<InventoryItemStack>();
    public int SortOrder => sortOrder;

    public bool HasIngredients => ingredients != null && ingredients.Length > 0;
    public bool HasResults => results != null && results.Length > 0;
}
