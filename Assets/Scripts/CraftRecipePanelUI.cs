using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftRecipePanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CraftingManager craftingManager;
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private CraftRecipeCardView cardPrefab;
    [SerializeField] private TMP_Text emptyStateText;

    private readonly List<CraftRecipeCardView> cardPool = new List<CraftRecipeCardView>();

    private void Awake()
    {
        ResolveManagers();
        EnsureLayout();
    }

    private void OnEnable()
    {
        CraftingManager.RecipesChanged += Refresh;
        InventoryManager.InventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        CraftingManager.RecipesChanged -= Refresh;
        InventoryManager.InventoryChanged -= Refresh;
    }

    [ContextMenu("Refresh Craft Recipe UI")]
    public void Refresh()
    {
        ResolveManagers();
        EnsureLayout();

        if (craftingManager == null || contentRoot == null)
        {
            SetEmptyState(true, "레시피 UI가 아직 설정되지 않았습니다.");
            HideAllCards();
            return;
        }

        IReadOnlyList<CraftRecipeDefinition> recipes = craftingManager.GetUnlockedRecipes();
        bool hasRecipes = recipes != null && recipes.Count > 0;

        SetEmptyState(!hasRecipes, "획득한 레시피가 없습니다.");

        if (!hasRecipes)
        {
            HideAllCards();
            return;
        }

        EnsurePoolSize(recipes.Count);
        BindVisibleCards(recipes);
    }

    private void ResolveManagers()
    {
        if (craftingManager == null)
        {
            craftingManager = CraftingManager.Instance;
        }

        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
        }
    }

    private void EnsureLayout()
    {
        if (contentRoot == null)
        {
            contentRoot = FindScrollViewContent();
        }

        if (contentRoot == null)
        {
            contentRoot = CreateContentRoot();
        }

        if (emptyStateText == null)
        {
            emptyStateText = CreateEmptyStateText();
        }
    }

    private RectTransform FindScrollViewContent()
    {
        ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content != null)
        {
            return scrollRect.content;
        }

        return null;
    }

    private RectTransform CreateContentRoot()
    {
        GameObject contentObject = new GameObject("CraftRecipeContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.layer = gameObject.layer;
        contentObject.transform.SetParent(transform, false);

        RectTransform rect = contentObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(24f, 24f);
        rect.offsetMax = new Vector2(-24f, -160f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        VerticalLayoutGroup layoutGroup = contentObject.GetComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
        layoutGroup.spacing = 16f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rect;
    }

    private TMP_Text CreateEmptyStateText()
    {
        GameObject textObject = new GameObject("CraftRecipeEmptyState", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 80f);
        rect.anchoredPosition = new Vector2(0f, -40f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = 34f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

        return text;
    }

    private void EnsurePoolSize(int requiredCount)
    {
        while (cardPool.Count < requiredCount)
        {
            cardPool.Add(CreateCardInstance());
        }
    }

    private CraftRecipeCardView CreateCardInstance()
    {
        if (cardPrefab != null)
        {
            CraftRecipeCardView card = Instantiate(cardPrefab, contentRoot);
            card.gameObject.SetActive(false);
            return card;
        }

        return CreateRuntimeCard();
    }

    private CraftRecipeCardView CreateRuntimeCard()
    {
        GameObject root = new GameObject("CraftRecipeCard", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Button), typeof(LayoutElement), typeof(CraftRecipeCardView));
        root.layer = gameObject.layer;
        root.transform.SetParent(contentRoot, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 210f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.92f);
        background.type = Image.Type.Sliced;

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 210f;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;

        RectTransform rootRect = root.transform as RectTransform;
        TextMeshProUGUI recipeName = CreateTextChild("RecipeName", rootRect, new Vector2(24f, -24f), new Vector2(-24f, -24f), 34f, FontStyles.Bold);
        TextMeshProUGUI ingredients = CreateTextChild("Ingredients", rootRect, new Vector2(24f, -78f), new Vector2(-24f, -78f), 28f, FontStyles.Normal);
        TextMeshProUGUI description = CreateTextChild("Description", rootRect, new Vector2(24f, -128f), new Vector2(-24f, -24f), 26f, FontStyles.Normal);

        CraftRecipeCardView card = root.GetComponent<CraftRecipeCardView>();
        card.Initialize(canvasGroup, button, recipeName, ingredients, description);
        card.gameObject.SetActive(false);
        return card;
    }

    private static TextMeshProUGUI CreateTextChild(string objectName, RectTransform parent, Vector2 topLeft, Vector2 bottomRight, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;

        return text;
    }

    private void BindVisibleCards(IReadOnlyList<CraftRecipeDefinition> recipes)
    {
        for (int i = 0; i < cardPool.Count; i++)
        {
            CraftRecipeCardView card = cardPool[i];
            bool shouldShow = i < recipes.Count;

            if (!shouldShow)
            {
                HideCard(card);
                continue;
            }

            CraftRecipeDefinition recipe = recipes[i];
            bool canCraft = craftingManager != null && craftingManager.CanCraft(recipe);

            card.gameObject.SetActive(true);
            card.Bind(recipe, canCraft, HandleRecipeSelected);
        }
    }

    private void HandleRecipeSelected(CraftRecipeDefinition recipe)
    {
        if (craftingManager == null || recipe == null)
        {
            return;
        }

        craftingManager.TryCraft(recipe);
        Refresh();
    }

    private void HideAllCards()
    {
        for (int i = 0; i < cardPool.Count; i++)
        {
            HideCard(cardPool[i]);
        }
    }

    private static void HideCard(CraftRecipeCardView card)
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

        emptyStateText.text = message;
        emptyStateText.gameObject.SetActive(visible);
    }
}
