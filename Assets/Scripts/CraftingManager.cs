using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class CraftingManager : MonoBehaviour
{
    private static CraftingManager instance;
    public static event Action RecipesChanged;

    [Header("Database")]
    [SerializeField] private CraftRecipeDefinition[] recipeDefinitions;



    private readonly Dictionary<string, CraftRecipeDefinition> definitionsById = new Dictionary<string, CraftRecipeDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unlockedRecipeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool initialized;

    public static CraftingManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CraftingManager>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
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

    public bool HasRecipe(string recipeId)
    {
        Initialize();
        return TryResolveRecipeId(recipeId, out string resolvedRecipeId) && unlockedRecipeIds.Contains(resolvedRecipeId);
    }

    public bool UnlockRecipe(string recipeId)
    {
        Initialize();
        if (!TryResolveRecipeId(recipeId, out string resolvedRecipeId))
        {
            return false;
        }

        if (!definitionsById.ContainsKey(resolvedRecipeId))
        {
            return false;
        }

        bool changed = unlockedRecipeIds.Add(resolvedRecipeId);
        if (changed)
        {
            RecipesChanged?.Invoke();
            if (SaveManager.Instance != null && Application.isPlaying)
            {
                SaveManager.Instance.SaveGame();
            }
        }

        return changed;
    }

    public bool UnlockRecipe(CraftRecipeDefinition recipe)
    {
        return recipe != null && UnlockRecipe(recipe.RecipeId);
    }

    public bool RemoveRecipe(string recipeId)
    {
        Initialize();
        if (!TryResolveRecipeId(recipeId, out string resolvedRecipeId))
        {
            return false;
        }

        bool changed = unlockedRecipeIds.Remove(resolvedRecipeId);
        if (changed)
        {
            RecipesChanged?.Invoke();
            if (SaveManager.Instance != null && Application.isPlaying)
            {
                SaveManager.Instance.SaveGame();
            }
        }

        return changed;
    }

    public IReadOnlyList<CraftRecipeDefinition> GetUnlockedRecipes()
    {
        Initialize();

        List<CraftRecipeDefinition> recipes = new List<CraftRecipeDefinition>(unlockedRecipeIds.Count);
        foreach (string recipeId in unlockedRecipeIds)
        {
            if (definitionsById.TryGetValue(recipeId, out CraftRecipeDefinition definition) && definition != null)
            {
                recipes.Add(definition);
            }
        }

        recipes.Sort(CompareRecipes);
        return recipes;
    }

    public bool CanCraft(CraftRecipeDefinition recipe)
    {
        if (recipe == null || InventoryManager.Instance == null)
        {
            return false;
        }

        InventoryItemStack[] ingredients = recipe.Ingredients;
        if (ingredients == null || ingredients.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < ingredients.Length; i++)
        {
            InventoryItemStack ingredient = ingredients[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.ItemId))
            {
                continue;
            }

            if (!InventoryManager.Instance.HasItem(ingredient.ItemId, ingredient.Quantity))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryCraft(CraftRecipeDefinition recipe)
    {
        if (recipe == null || !HasRecipe(recipe.RecipeId) || !CanCraft(recipe) || InventoryManager.Instance == null)
        {
            return false;
        }

        InventoryItemStack[] ingredients = recipe.Ingredients;
        for (int i = 0; i < ingredients.Length; i++)
        {
            InventoryItemStack ingredient = ingredients[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.ItemId))
            {
                continue;
            }

            InventoryManager.Instance.RemoveItem(ingredient.ItemId, ingredient.Quantity);
        }

        InventoryItemStack[] results = recipe.Results;
        for (int i = 0; i < results.Length; i++)
        {
            InventoryItemStack result = results[i];
            if (result == null || string.IsNullOrWhiteSpace(result.ItemId))
            {
                continue;
            }

            InventoryManager.Instance.AddItem(result.ItemId, result.Quantity);
        }

        return true;
    }

    public bool TryGetRecipe(string recipeId, out CraftRecipeDefinition recipe)
    {
        Initialize();
        if (!TryResolveRecipeId(recipeId, out string resolvedRecipeId))
        {
            recipe = null;
            return false;
        }

        return definitionsById.TryGetValue(resolvedRecipeId, out recipe) && recipe != null;
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        RebuildDefinitionLookup();
        initialized = true;
    }

    private void RebuildDefinitionLookup()
    {
        definitionsById.Clear();

        if (recipeDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < recipeDefinitions.Length; i++)
        {
            CraftRecipeDefinition recipe = recipeDefinitions[i];
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.RecipeId))
            {
                continue;
            }

            definitionsById[NormalizeRecipeId(recipe.RecipeId)] = recipe;
        }
    }

    public List<string> GetSaveData()
    {
        Initialize();
        return new List<string>(unlockedRecipeIds);
    }

    public void LoadSaveData(List<string> data)
    {
        Initialize();
        unlockedRecipeIds.Clear();

        if (data != null)
        {
            foreach (string recipeId in data)
            {
                if (!string.IsNullOrWhiteSpace(recipeId))
                {
                    unlockedRecipeIds.Add(NormalizeRecipeId(recipeId));
                }
            }
        }

        RecipesChanged?.Invoke();
    }

    private static int CompareRecipes(CraftRecipeDefinition left, CraftRecipeDefinition right)
    {
        int sortComparison = left.SortOrder.CompareTo(right.SortOrder);
        if (sortComparison != 0)
        {
            return sortComparison;
        }

        return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveRecipeId(string recipeId, out string resolvedRecipeId)
    {
        resolvedRecipeId = NormalizeRecipeId(recipeId);
        return !string.IsNullOrWhiteSpace(resolvedRecipeId);
    }

    private static string NormalizeRecipeId(string recipeId)
    {
        return string.IsNullOrWhiteSpace(recipeId) ? string.Empty : recipeId.Trim();
    }
}

public static class CraftingYarnBridge
{
    [YarnFunction("has_recipe")]
    public static bool HasRecipe(string recipeId)
    {
        return CraftingManager.Instance != null && CraftingManager.Instance.HasRecipe(recipeId);
    }

    [YarnCommand("give_recipe")]
    public static void GiveRecipe(string recipeId)
    {
        if (CraftingManager.Instance == null)
        {
            Debug.LogWarning($"Cannot give recipe '{recipeId}' because no CraftingManager exists in the scene.");
            return;
        }

        CraftingManager.Instance.UnlockRecipe(recipeId);
    }

    [YarnCommand("remove_recipe")]
    public static void RemoveRecipe(string recipeId)
    {
        if (CraftingManager.Instance == null)
        {
            Debug.LogWarning($"Cannot remove recipe '{recipeId}' because no CraftingManager exists in the scene.");
            return;
        }

        CraftingManager.Instance.RemoveRecipe(recipeId);
    }
}
