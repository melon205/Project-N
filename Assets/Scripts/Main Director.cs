using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Threading.Tasks;
using Yarn.Unity;

public class MainDirector : MonoBehaviour
{
    private const string StoryLineBottomPadding = "\n\n\n\n\n\n\n\n\n\n";

    [Header("References")]
    public RectTransform content;
    public RectTransform viewport;
    public GameObject storyLinePrefab;
    public ScrollRect scrollRect;
    public DialogueRunner dialogueRunner;

    [Header("Yarn")]
    public bool startDialogueOnStart = true;

    [Header("Scroll")]
    public float scrollDuration = 0.45f;
    public float topPadding = 30f;

    [Header("Typewriter")]
    public float charactersPerSecond = 50f;

    [Header("Skip")]
    public float doubleClickTime = 0.3f;

    private Coroutine typingCoroutine;
    private Coroutine scrollCoroutine;

    private bool isTyping = false;
    private bool skipRequested = false;
    private bool autoScrollEnabled = true;

    private float lastClickTime = -1f;
    private TaskCompletionSource<bool> currentLineCompletion;
    private TMP_Text currentTypingLineText;
    private RectTransform currentTypingLineRect;

    private void Start()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onNodeStart.AddListener(OnNodeStart);
        }
        ConfigureContentLayout();
        StartCoroutine(BeginStartup());
    }

    private void OnNodeStart(string nodeName)
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdateLastYarnNode(nodeName);
        }
    }

    private IEnumerator BeginStartup()
    {
        yield return null;
        RefreshLayout();
        SetScrollToTopImmediate();

        if (startDialogueOnStart && dialogueRunner != null)
        {
            SaveData data = null;
            if (SaveManager.Instance != null)
            {
                data = SaveManager.Instance.LoadGame();
            }

            if (data != null && !string.IsNullOrWhiteSpace(data.lastYarnNode))
            {
                StartDialogueFromNode(data.lastYarnNode);
            }
        }
    }

    public void StartInitialDialogue()
    {
        if (SaveManager.Instance == null)
        {
            return;
        }

        SaveData data = SaveManager.Instance.LoadGame();
        if (data != null && !string.IsNullOrWhiteSpace(data.lastYarnNode))
        {
            StartDialogueFromNode(data.lastYarnNode);
        }
    }

    public void StartDialogueFromNode(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            Debug.LogWarning("Cannot start dialogue with an empty node name.", this);
            return;
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("MainDirector requires a DialogueRunner reference to start Yarn dialogue.", this);
            return;
        }

        if (dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning("Dialogue is already running.", this);
            return;
        }

        dialogueRunner.StartDialogue(nodeName).Forget();
    }

    private void ConfigureContentLayout()
    {
        VerticalLayoutGroup layoutGroup = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
        if (layoutGroup == null)
        {
            return;
        }

        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandHeight = false;
    }

    public void ClearStory()
    {
        if (content != null)
        {
            foreach (Transform child in content)
            {
                Destroy(child.gameObject);
            }
        }

        StopTypingCoroutine();
        StopScrollCoroutine();
        isTyping = false;
        skipRequested = false;
        currentTypingLineText = null;
        currentTypingLineRect = null;
        currentLineCompletion = null;

        SetScrollToTopImmediate();
    }

    public void AddStoryLine(string message)
    {
        if (isTyping)
        {
            CompleteCurrentLineImmediately();
        }

        if (content == null || storyLinePrefab == null)
        {
            Debug.LogError("MainDirector requires both content and storyLinePrefab references.", this);
            currentLineCompletion?.TrySetResult(false);
            return;
        }

        message += StoryLineBottomPadding;
        autoScrollEnabled = true;
        skipRequested = false;
        currentLineCompletion = new TaskCompletionSource<bool>();

        GameObject lineObj = Instantiate(storyLinePrefab, content);
        TMP_Text lineText = lineObj.GetComponent<TMP_Text>();
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();

        if (lineText == null)
        {
            Debug.LogError("storyLinePrefab is missing TMP_Text.");
            currentLineCompletion.TrySetResult(false);
            return;
        }

        PrepareLineLayout(lineRect);
        lineText.text = message;
        lineText.maxVisibleCharacters = 0;
        lineText.ForceMeshUpdate();
        UpdateLineHeight(lineText, lineRect);
        RefreshLayout();

        if (!autoScrollEnabled)
        {
            SetScrollToTopImmediate();
        }

        StopTypingCoroutine();
        typingCoroutine = StartCoroutine(TypeLine(lineText, lineRect));
    }

    public Task WaitForLineCompleteAsync()
    {
        if (currentLineCompletion == null)
        {
            return Task.CompletedTask;
        }

        return currentLineCompletion.Task;
    }

    public void RequestSkipCurrentLine()
    {
        if (!isTyping)
        {
            return;
        }

        skipRequested = true;
    }

    private void Update()
    {
        GetPointerState(out bool pressedThisFrame, out bool releasedThisFrame);

        if (pressedThisFrame)
        {
            autoScrollEnabled = false;
            StopScrollCoroutine();
        }

        if (!isTyping)
        {
            return;
        }

        if (releasedThisFrame)
        {
            float now = Time.time;

            if (now - lastClickTime <= doubleClickTime)
            {
                skipRequested = true;
                lastClickTime = -1f;
            }
            else
            {
                lastClickTime = now;
            }
        }
    }

    private IEnumerator TypeLine(TMP_Text lineText, RectTransform lineRect)
    {
        isTyping = true;
        currentTypingLineText = lineText;
        currentTypingLineRect = lineRect;

        lineText.ForceMeshUpdate();
        int totalChars = lineText.textInfo.characterCount;
        float interval = 1f / Mathf.Max(1f, charactersPerSecond);

        for (int i = 0; i <= totalChars; i++)
        {
            if (skipRequested)
            {
                lineText.maxVisibleCharacters = totalChars;
                break;
            }

            lineText.maxVisibleCharacters = i;

            RefreshLayout();

            if (autoScrollEnabled)
            {
                StartScrollTo(lineRect);
            }

            yield return new WaitForSeconds(interval);
        }

        yield return null;
        RefreshLayout();

        if (autoScrollEnabled)
        {
            StartScrollTo(lineRect);
        }

        FinishCurrentLineTyping();
    }

    private void CompleteCurrentLineImmediately()
    {
        StopTypingCoroutine();

        if (currentTypingLineText != null)
        {
            currentTypingLineText.ForceMeshUpdate();
            currentTypingLineText.maxVisibleCharacters = currentTypingLineText.textInfo.characterCount;
            RefreshLayout();

            if (autoScrollEnabled && currentTypingLineRect != null)
            {
                StartScrollTo(currentTypingLineRect);
            }
        }

        FinishCurrentLineTyping();
    }

    private void PrepareLineLayout(RectTransform lineRect)
    {
        if (lineRect == null)
        {
            return;
        }

        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.offsetMin = new Vector2(0f, lineRect.offsetMin.y);
        lineRect.offsetMax = new Vector2(0f, lineRect.offsetMax.y);

        LayoutElement layoutElement = lineRect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = lineRect.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.flexibleHeight = 0f;
    }

    private void UpdateLineHeight(TMP_Text lineText, RectTransform lineRect)
    {
        if (lineText == null || lineRect == null)
        {
            return;
        }

        LayoutElement layoutElement = lineRect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = lineRect.gameObject.AddComponent<LayoutElement>();
        }

        float availableWidth = lineRect.rect.width;
        if (content != null)
        {
            float paddingWidth = 0f;
            VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null)
            {
                paddingWidth = layoutGroup.padding.left + layoutGroup.padding.right;
            }

            availableWidth = Mathf.Max(0f, content.rect.width - paddingWidth);
        }

        float preferredHeight = lineText.GetPreferredValues(lineText.text, availableWidth, 0f).y;
        layoutElement.preferredHeight = Mathf.Ceil(preferredHeight);
    }

    private void RefreshLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        if (viewport != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
        }
    }

    private void SetScrollToTopImmediate()
    {
        if (content == null || scrollRect == null)
        {
            return;
        }

        scrollRect.StopMovement();
        SetContentY(0f);
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private float GetTargetScrollY(RectTransform target)
    {
        float targetY = Mathf.Abs(target.anchoredPosition.y) - topPadding;

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);

        return Mathf.Clamp(targetY, 0f, maxScroll);
    }

    private IEnumerator SmoothScrollTo(float targetY)
    {
        float startY = content.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < scrollDuration)
        {
            if (!autoScrollEnabled)
            {
                scrollCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / scrollDuration;
            t = 1f - Mathf.Pow(1f - t, 3f);

            SetContentY(Mathf.Lerp(startY, targetY, t));

            yield return null;
        }

        SetContentY(targetY);
        scrollCoroutine = null;
    }

    private void GetPointerState(out bool pressedThisFrame, out bool releasedThisFrame)
    {
        pressedThisFrame = false;
        releasedThisFrame = false;

        if (Mouse.current != null)
        {
            pressedThisFrame |= Mouse.current.leftButton.wasPressedThisFrame;
            releasedThisFrame |= Mouse.current.leftButton.wasReleasedThisFrame;
        }

        if (Touchscreen.current != null)
        {
            pressedThisFrame |= Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            releasedThisFrame |= Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        }
    }

    private void StartScrollTo(RectTransform lineRect)
    {
        if (lineRect == null)
        {
            return;
        }

        StopScrollCoroutine();
        scrollCoroutine = StartCoroutine(SmoothScrollTo(GetTargetScrollY(lineRect)));
    }

    private void StopTypingCoroutine()
    {
        if (typingCoroutine == null)
        {
            return;
        }

        StopCoroutine(typingCoroutine);
        typingCoroutine = null;
    }

    private void StopScrollCoroutine()
    {
        if (scrollCoroutine == null)
        {
            return;
        }

        StopCoroutine(scrollCoroutine);
        scrollCoroutine = null;
    }

    private void FinishCurrentLineTyping()
    {
        isTyping = false;
        skipRequested = false;
        typingCoroutine = null;
        currentTypingLineText = null;
        currentTypingLineRect = null;
        currentLineCompletion?.TrySetResult(true);
    }

    private void SetContentY(float y)
    {
        if (content == null)
        {
            return;
        }

        Vector2 position = content.anchoredPosition;
        position.y = y;
        content.anchoredPosition = position;
    }
}
