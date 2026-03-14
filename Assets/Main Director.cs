using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Threading.Tasks;

public class MainDirector : MonoBehaviour
{
    [Header("References")]
    public RectTransform content;
    public RectTransform viewport;
    public GameObject storyLinePrefab;
    public ScrollRect scrollRect;

    [Header("First Story")]
    [TextArea(3, 10)]
    public string firstStoryText = "k";

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

    private void Start()
    {
        ConfigureContentLayout();
        StartCoroutine(BeginFirstStory());
    }

    private IEnumerator BeginFirstStory()
    {
        yield return null;
        RefreshLayout();
        SetScrollToTopImmediate();
        AddStoryLine(firstStoryText);
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

    public void AddStoryLine(string message)
    {
        message += "\n\n\n\n\n\n\n\n\n\n";
        autoScrollEnabled = true;
        skipRequested = false;
        currentLineCompletion?.TrySetResult(false);
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

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

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
        bool pressedThisFrame = false;
        bool releasedThisFrame = false;

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                pressedThisFrame = true;
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                releasedThisFrame = true;
        }

        if (Touchscreen.current != null)
        {
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                pressedThisFrame = true;
            if (Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
                releasedThisFrame = true;
        }

        if (pressedThisFrame)
        {
            autoScrollEnabled = false;

            if (scrollCoroutine != null)
            {
                StopCoroutine(scrollCoroutine);
                scrollCoroutine = null;
            }
        }

        if (!isTyping) return;

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
                float targetY = GetTargetScrollY(lineRect);

                if (scrollCoroutine != null)
                {
                    StopCoroutine(scrollCoroutine);
                }

                scrollCoroutine = StartCoroutine(SmoothScrollTo(targetY));
            }

            yield return new WaitForSeconds(interval);
        }

        yield return null;
        RefreshLayout();

        if (autoScrollEnabled)
        {
            float finalTargetY = GetTargetScrollY(lineRect);

            if (scrollCoroutine != null)
            {
                StopCoroutine(scrollCoroutine);
            }

            scrollCoroutine = StartCoroutine(SmoothScrollTo(finalTargetY));
        }

        isTyping = false;
        skipRequested = false;
        typingCoroutine = null;
        currentLineCompletion?.TrySetResult(true);
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
        Vector2 pos = content.anchoredPosition;
        pos.y = 0f;
        content.anchoredPosition = pos;
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

            Vector2 pos = content.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, t);
            content.anchoredPosition = pos;

            yield return null;
        }

        Vector2 finalPos = content.anchoredPosition;
        finalPos.y = targetY;
        content.anchoredPosition = finalPos;

        scrollCoroutine = null;
    }
}
