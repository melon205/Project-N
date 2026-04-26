using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Yarn.Unity;

public class MainDirectorPresenter : DialoguePresenterBase
{
    private static MainDirectorPresenter instance;

    [SerializeField] private MainDirector mainDirector;
    [SerializeField] private bool includeCharacterName = true;
    [Header("Choice UI")]
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private bool showUnavailableOptionsAsDisabled = true;
    [SerializeField] private string hideUnavailableOptionTag = "hide_if_unavailable";
    [SerializeField] private string requiredItemTagPrefix = "requires_item:";
    [SerializeField] private Color disabledOptionColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color ownedRequiredItemColor = new Color(0.3f, 0.85f, 0.3f);
    [SerializeField] private Color missingRequiredItemColor = new Color(0.9f, 0.25f, 0.25f);
    [SerializeField] private string lockedOptionPrefix = "\U0001F512 ";
    [Header("Unavailable Feedback")]
    [SerializeField] private TMP_Text floatingMessageText;
    [SerializeField] private CanvasGroup floatingMessageCanvasGroup;
    [SerializeField] private float floatingMessageDuration = 1.25f;
    [SerializeField] private float floatingMessageFadeDuration = 0.2f;
    [Header("Inventory Toast")]
    [SerializeField] private Color inventoryGainToastColor = new Color(0.3f, 0.85f, 0.3f);
    [SerializeField] private Color inventoryUseToastColor = new Color(0.9f, 0.25f, 0.25f);
    [SerializeField] private string missingItemMessageFormat = "{0}이(가) 필요합니다.";

    private TMP_Text[] choiceLabels;
    private UnityAction[] choiceButtonActions;
    private DialogueOption[] currentOptions;
    private YarnTaskCompletionSource<DialogueOption> currentOptionSelection;
    private readonly StringBuilder pendingStoryText = new StringBuilder();
    private Coroutine floatingMessageCoroutine;
    private Color defaultToastTextColor = Color.white;

    private readonly struct ToastMessage
    {
        public readonly string text;
        public readonly Color color;
        public ToastMessage(string t, Color c)
        {
            text = t;
            color = c;
        }
    }
    private readonly Queue<ToastMessage> toastQueue = new Queue<ToastMessage>();

    private void Awake()
    {
        instance = this;

        if (mainDirector == null)
        {
            mainDirector = FindAnyObjectByType<MainDirector>();
        }

        if (floatingMessageText != null)
        {
            defaultToastTextColor = floatingMessageText.color;
        }

        CacheChoiceLabels();
        RegisterChoiceButtonCallbacks();
        HideAllChoiceButtons();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        UnregisterChoiceButtonCallbacks();
    }

    public static void ShowInventoryToast(string itemDisplayName, int quantity, bool gained)
    {
        if (instance == null || string.IsNullOrWhiteSpace(itemDisplayName) || quantity <= 0)
        {
            return;
        }

        Color toastColor = gained ? instance.inventoryGainToastColor : instance.inventoryUseToastColor;
        string prefix = gained ? "+" : "-";
        instance.ShowToastMessage($"{prefix} {itemDisplayName} {quantity}개", toastColor);
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        pendingStoryText.Clear();
        HideAllChoiceButtons();
        HideFloatingMessageImmediate();
        return YarnTask.CompletedTask;
    }

    public override async YarnTask OnDialogueCompleteAsync()
    {
        await FlushPendingStoryTextAsync();
        currentOptionSelection?.TrySetResult(null);
        currentOptionSelection = null;
        currentOptions = null;
        HideAllChoiceButtons();
        HideFloatingMessageImmediate();
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (mainDirector == null)
        {
            Debug.LogError("MainDirectorPresenter requires a MainDirector reference.", this);
            return YarnTask.CompletedTask;
        }

        string text = line.TextWithoutCharacterName.Text;

        if (includeCharacterName && !string.IsNullOrWhiteSpace(line.CharacterName))
        {
            text = $"{line.CharacterName}: {text}";
        }

        AppendPendingStoryText(text);
        return YarnTask.CompletedTask;
    }

    public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
    {
        await FlushPendingStoryTextAsync(cancellationToken);

        if (choiceButtons == null || choiceButtons.Length == 0)
        {
            Debug.LogError("MainDirectorPresenter requires choice buttons to present dialogue options.", this);
            return await DialogueRunner.NoOptionSelected;
        }

        int visibleOptionCount = 0;
        foreach (DialogueOption option in dialogueOptions)
        {
            if (ShouldDisplayOption(option))
            {
                visibleOptionCount++;
            }
        }

        if (visibleOptionCount == 0)
        {
            HideAllChoiceButtons();
            return await DialogueRunner.NoOptionSelected;
        }

        if (visibleOptionCount > choiceButtons.Length)
        {
            Debug.LogError($"MainDirectorPresenter has {choiceButtons.Length} choice buttons, but {visibleOptionCount} visible dialogue options were requested.", this);
            HideAllChoiceButtons();
            return await DialogueRunner.NoOptionSelected;
        }

        currentOptions = new DialogueOption[choiceButtons.Length];
        currentOptionSelection = new YarnTaskCompletionSource<DialogueOption>();

        int visibleIndex = 0;
        for (int i = 0; i < dialogueOptions.Length; i++)
        {
            DialogueOption option = dialogueOptions[i];
            if (!ShouldDisplayOption(option))
            {
                continue;
            }

            SetChoiceButton(visibleIndex, option);
            visibleIndex++;
        }

        for (int i = visibleIndex; i < choiceButtons.Length; i++)
        {
            HideChoiceButton(i);
        }

        using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.NextContentToken);

        async YarnTask CancelSelectionWhenDialogueCancelled()
        {
            await YarnTask.WaitUntilCanceled(cancellationSource.Token);

            if (cancellationToken.IsNextContentRequested)
            {
                currentOptionSelection?.TrySetResult(null);
            }
        }

        CancelSelectionWhenDialogueCancelled().Forget();

        DialogueOption selectedOption = await currentOptionSelection.Task;
        cancellationSource.Cancel();

        currentOptionSelection = null;
        currentOptions = null;
        HideAllChoiceButtons();

        if (cancellationToken.NextContentToken.IsCancellationRequested)
        {
            return await DialogueRunner.NoOptionSelected;
        }

        return selectedOption;
    }

    private void CacheChoiceLabels()
    {
        if (choiceButtons == null)
        {
            choiceLabels = System.Array.Empty<TMP_Text>();
            choiceButtonActions = System.Array.Empty<UnityAction>();
            return;
        }

        choiceLabels = new TMP_Text[choiceButtons.Length];
        choiceButtonActions = new UnityAction[choiceButtons.Length];
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
            {
                continue;
            }

            choiceLabels[i] = button.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void RegisterChoiceButtonCallbacks()
    {
        if (choiceButtons == null)
        {
            return;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
            {
                continue;
            }

            int index = i;
            choiceButtonActions[i] = () => OnChoiceButtonClicked(index);
            button.onClick.AddListener(choiceButtonActions[i]);
        }
    }

    private void UnregisterChoiceButtonCallbacks()
    {
        if (choiceButtons == null)
        {
            return;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null || choiceButtonActions == null || i >= choiceButtonActions.Length || choiceButtonActions[i] == null)
            {
                continue;
            }

            button.onClick.RemoveListener(choiceButtonActions[i]);
        }
    }

    private void SetChoiceButton(int index, DialogueOption option)
    {
        Button button = choiceButtons[index];
        if (button == null)
        {
            Debug.LogError($"Choice button at index {index} is not assigned.", this);
            return;
        }

        currentOptions[index] = option;
        button.gameObject.SetActive(true);
        button.interactable = true;

        if (choiceLabels != null && index < choiceLabels.Length && choiceLabels[index] != null)
        {
            choiceLabels[index].text = BuildChoiceLabel(option, option.IsAvailable);
        }
    }

    private void HideChoiceButton(int index)
    {
        if (choiceButtons == null || index < 0 || index >= choiceButtons.Length)
        {
            return;
        }

        Button button = choiceButtons[index];
        if (button == null)
        {
            return;
        }

        button.interactable = false;
        button.gameObject.SetActive(false);

        if (currentOptions != null && index < currentOptions.Length)
        {
            currentOptions[index] = null;
        }
    }

    private void HideAllChoiceButtons()
    {
        if (choiceButtons == null)
        {
            return;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            HideChoiceButton(i);
        }
    }

    private void OnChoiceButtonClicked(int index)
    {
        if (currentOptionSelection == null || currentOptions == null)
        {
            return;
        }

        if (index < 0 || index >= currentOptions.Length)
        {
            return;
        }

        DialogueOption selectedOption = currentOptions[index];
        if (selectedOption == null)
        {
            return;
        }

        if (!selectedOption.IsAvailable)
        {
            ShowUnavailableOptionMessage(selectedOption);
            return;
        }

        currentOptionSelection.TrySetResult(selectedOption);
    }

    private bool ShouldDisplayOption(DialogueOption option)
    {
        if (option.IsAvailable)
        {
            return true;
        }

        if (HasMetadataTag(option, hideUnavailableOptionTag))
        {
            return false;
        }

        return showUnavailableOptionsAsDisabled;
    }

    private static bool HasMetadataTag(DialogueOption option, string tag)
    {
        if (option?.Line?.Metadata == null || string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        for (int i = 0; i < option.Line.Metadata.Length; i++)
        {
            if (string.Equals(option.Line.Metadata[i], tag, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string BuildChoiceLabel(DialogueOption option, bool interactable)
    {
        string optionText = option.Line.Text.Text;
        if (!interactable)
        {
            optionText = $"<color=#{ColorUtility.ToHtmlStringRGB(disabledOptionColor)}>{lockedOptionPrefix}{optionText}</color>";
        }

        string requiredItemId = GetMetadataValue(option, requiredItemTagPrefix);
        if (string.IsNullOrWhiteSpace(requiredItemId))
        {
            return optionText;
        }

        string itemDisplayName = InventoryManager.GetDisplayNameOrId(requiredItemId);
        bool hasRequiredItem = InventoryManager.Instance != null && InventoryManager.Instance.HasItem(requiredItemId);
        Color itemColor = hasRequiredItem ? ownedRequiredItemColor : missingRequiredItemColor;
        string itemLabel = $" <color=#{ColorUtility.ToHtmlStringRGB(itemColor)}>[{itemDisplayName}]</color>";
        return optionText + itemLabel;
    }

    private static string GetMetadataValue(DialogueOption option, string prefix)
    {
        if (option?.Line?.Metadata == null || string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        for (int i = 0; i < option.Line.Metadata.Length; i++)
        {
            string metadata = option.Line.Metadata[i];
            if (metadata.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return metadata.Substring(prefix.Length).Trim();
            }
        }

        return string.Empty;
    }

    private void ShowUnavailableOptionMessage(DialogueOption option)
    {
        string requiredItemId = GetMetadataValue(option, requiredItemTagPrefix);
        string message = string.IsNullOrWhiteSpace(requiredItemId)
            ? option.Line.Text.Text
            : string.Format(missingItemMessageFormat, InventoryManager.GetDisplayNameOrId(requiredItemId));

        ShowToastMessage(message, defaultToastTextColor);
    }

    private void ShowToastMessage(string message, Color textColor)
    {
        if (floatingMessageText == null)
        {
            return;
        }

        toastQueue.Enqueue(new ToastMessage(message, textColor));

        if (floatingMessageCoroutine == null)
        {
            floatingMessageCoroutine = StartCoroutine(ProcessToastQueue());
        }
    }

    private IEnumerator ProcessToastQueue()
    {
        while (toastQueue.Count > 0)
        {
            ToastMessage msg = toastQueue.Dequeue();
            floatingMessageText.text = msg.text;
            floatingMessageText.color = msg.color;

            if (floatingMessageCanvasGroup == null)
            {
                floatingMessageText.gameObject.SetActive(true);
                yield return new WaitForSeconds(floatingMessageDuration);
                floatingMessageText.gameObject.SetActive(false);
            }
            else
            {
                floatingMessageText.gameObject.SetActive(true);
                floatingMessageCanvasGroup.gameObject.SetActive(true);
                yield return FadeFloatingMessage(0f, 1f, floatingMessageFadeDuration);
                yield return new WaitForSeconds(floatingMessageDuration);
                yield return FadeFloatingMessage(1f, 0f, floatingMessageFadeDuration);
                floatingMessageText.gameObject.SetActive(false);
                floatingMessageCanvasGroup.gameObject.SetActive(false);
            }

            if (toastQueue.Count > 0)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

        floatingMessageCoroutine = null;
    }

    private IEnumerator FadeFloatingMessage(float from, float to, float duration)
    {
        if (floatingMessageCanvasGroup == null || duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        floatingMessageCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            floatingMessageCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        floatingMessageCanvasGroup.alpha = to;
    }

    private void HideFloatingMessageImmediate()
    {
        if (floatingMessageCoroutine != null)
        {
            StopCoroutine(floatingMessageCoroutine);
            floatingMessageCoroutine = null;
        }

        toastQueue.Clear();

        if (floatingMessageText != null)
        {
            floatingMessageText.gameObject.SetActive(false);
        }

        if (floatingMessageCanvasGroup != null)
        {
            floatingMessageCanvasGroup.alpha = 0f;
            floatingMessageCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void AppendPendingStoryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (pendingStoryText.Length > 0)
        {
            pendingStoryText.Append('\n');
        }

        pendingStoryText.Append(text);
    }

    private async YarnTask FlushPendingStoryTextAsync(LineCancellationToken cancellationToken)
    {
        if (pendingStoryText.Length == 0)
        {
            return;
        }

        if (mainDirector == null)
        {
            pendingStoryText.Clear();
            return;
        }

        string textToDisplay = pendingStoryText.ToString();
        pendingStoryText.Clear();

        mainDirector.AddStoryLine(textToDisplay);

        Task lineTask = mainDirector.WaitForLineCompleteAsync();
        while (!lineTask.IsCompleted)
        {
            if (cancellationToken.IsHurryUpRequested || cancellationToken.IsNextContentRequested)
            {
                mainDirector.RequestSkipCurrentLine();
            }

            await Task.Yield();
        }

        await lineTask;
    }

    private async YarnTask FlushPendingStoryTextAsync()
    {
        if (pendingStoryText.Length == 0)
        {
            return;
        }

        if (mainDirector == null)
        {
            pendingStoryText.Clear();
            return;
        }

        string textToDisplay = pendingStoryText.ToString();
        pendingStoryText.Clear();

        mainDirector.AddStoryLine(textToDisplay);
        await mainDirector.WaitForLineCompleteAsync();
    }
}
