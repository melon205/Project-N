using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class BottomBarModeController : MonoBehaviour
{
    public enum InitialState
    {
        Hidden,
        Inventory,
        Craft
    }

    public enum PanelMode
    {
        Inventory,
        Craft
    }

    public enum HiddenDirection
    {
        Bottom,
        Left,
        Right
    }

    [System.Serializable]
    public class PanelBinding
    {
        public PanelMode mode;
        public Button button;
        public RectTransform panel;
        [HideInInspector] public CanvasGroup canvasGroup;
        [HideInInspector] public Vector2 shownPosition;
        [HideInInspector] public UnityAction clickAction;
    }

    [Header("Panels")]
    [SerializeField] private PanelBinding[] panels;
    [SerializeField] private InitialState initialState = InitialState.Hidden;

    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.28f;
    [SerializeField] private float hiddenMargin = 40f;
    [SerializeField] private AnimationCurve animationCurve = null;

    [Header("Button State")]
    [SerializeField] private Color activeButtonColor = Color.white;
    [SerializeField] private Color inactiveButtonColor = new Color(1f, 1f, 1f, 0.55f);

    private Sequence transitionSequence;
    private PanelMode? currentMode;
    private bool initialized;

    private void Awake()
    {
        if (animationCurve == null || animationCurve.length == 0)
        {
            animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        KillTransition();

        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            PanelBinding binding = panels[i];
            if (binding?.button == null)
            {
                continue;
            }

            if (binding.clickAction != null)
            {
                binding.button.onClick.RemoveListener(binding.clickAction);
            }
        }
    }

    public void SwitchToInventory()
    {
        SwitchTo(PanelMode.Inventory);
    }

    public void SwitchToCraft()
    {
        SwitchTo(PanelMode.Craft);
    }

    public void SwitchTo(PanelMode mode)
    {
        Initialize();

        if (currentMode.HasValue && currentMode.Value == mode)
        {
            HideCurrentPanel();
            return;
        }

        KillTransition();
        AnimateSwitch(mode);
    }

    public void HideCurrentPanel()
    {
        Initialize();

        if (!currentMode.HasValue)
        {
            return;
        }

        KillTransition();
        AnimateHide(currentMode.Value);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        if (panels == null || panels.Length == 0)
        {
            initialized = true;
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            PanelBinding binding = panels[i];
            if (binding == null || binding.panel == null)
            {
                continue;
            }

            binding.canvasGroup = binding.panel.GetComponent<CanvasGroup>();
            if (binding.canvasGroup == null)
            {
                binding.canvasGroup = binding.panel.gameObject.AddComponent<CanvasGroup>();
            }

            binding.shownPosition = binding.panel.anchoredPosition;
            if (binding.button != null)
            {
                PanelMode mode = binding.mode;
                binding.clickAction = () => SwitchTo(mode);
                binding.button.onClick.AddListener(binding.clickAction);
            }
        }

        currentMode = GetInitialMode();

        for (int i = 0; i < panels.Length; i++)
        {
            PanelBinding binding = panels[i];
            if (binding == null || binding.panel == null || binding.canvasGroup == null)
            {
                continue;
            }

            bool isDefault = currentMode.HasValue && binding.mode == currentMode.Value;
            binding.panel.gameObject.SetActive(true);
            binding.panel.anchoredPosition = isDefault ? binding.shownPosition : GetBottomHiddenPosition(binding.panel, binding.shownPosition);
            binding.canvasGroup.alpha = isDefault ? 1f : 0f;
            binding.canvasGroup.interactable = isDefault;
            binding.canvasGroup.blocksRaycasts = isDefault;

            if (!isDefault)
            {
                binding.panel.gameObject.SetActive(false);
            }
        }

        UpdateButtonVisuals(currentMode);
        initialized = true;
    }

    private void AnimateSwitch(PanelMode nextMode)
    {
        PanelBinding currentBinding = currentMode.HasValue ? GetBinding(currentMode.Value) : null;
        PanelBinding nextBinding = GetBinding(nextMode);

        if (nextBinding == null || nextBinding.panel == null || nextBinding.canvasGroup == null)
        {
            return;
        }

        if (currentBinding != null && currentBinding.canvasGroup != null)
        {
            currentBinding.canvasGroup.interactable = false;
            currentBinding.canvasGroup.blocksRaycasts = false;
        }

        nextBinding.panel.gameObject.SetActive(true);
        nextBinding.canvasGroup.interactable = false;
        nextBinding.canvasGroup.blocksRaycasts = false;

        bool isInitialOpen = currentBinding == null || !currentMode.HasValue;
        bool moveLeft = !isInitialOpen && GetBindingIndex(nextMode) > GetBindingIndex(currentMode.Value);
        Vector2 fromCurrent = currentBinding != null ? currentBinding.shownPosition : Vector2.zero;
        Vector2 toCurrent = currentBinding != null
            ? (moveLeft ? GetLeftHiddenPosition(currentBinding.panel, currentBinding.shownPosition) : GetRightHiddenPosition(currentBinding.panel, currentBinding.shownPosition))
            : Vector2.zero;
        Vector2 fromNext = isInitialOpen
            ? GetBottomHiddenPosition(nextBinding.panel, nextBinding.shownPosition)
            : (moveLeft
                ? GetRightHiddenPosition(nextBinding.panel, nextBinding.shownPosition)
                : GetLeftHiddenPosition(nextBinding.panel, nextBinding.shownPosition));
        Vector2 toNext = nextBinding.shownPosition;

        nextBinding.panel.anchoredPosition = fromNext;
        nextBinding.canvasGroup.alpha = 0f;

        transitionSequence = DOTween.Sequence();
        if (currentBinding != null && currentBinding.panel != null && currentBinding.canvasGroup != null)
        {
            currentBinding.panel.anchoredPosition = fromCurrent;
            transitionSequence.Join(ApplyEase(currentBinding.panel.DOAnchorPos(toCurrent, animationDuration)));
            transitionSequence.Join(ApplyEase(currentBinding.canvasGroup.DOFade(0f, animationDuration)));
        }

        transitionSequence.Join(ApplyEase(nextBinding.panel.DOAnchorPos(toNext, animationDuration)));
        transitionSequence.Join(ApplyEase(nextBinding.canvasGroup.DOFade(1f, animationDuration)));
        transitionSequence.OnComplete(() =>
        {
            if (currentBinding != null && currentBinding.panel != null && currentBinding.canvasGroup != null)
            {
                currentBinding.panel.anchoredPosition = toCurrent;
                currentBinding.canvasGroup.alpha = 0f;
                currentBinding.panel.gameObject.SetActive(false);
            }

            nextBinding.panel.anchoredPosition = nextBinding.shownPosition;
            nextBinding.canvasGroup.alpha = 1f;
            nextBinding.canvasGroup.interactable = true;
            nextBinding.canvasGroup.blocksRaycasts = true;

            currentMode = nextMode;
            UpdateButtonVisuals(nextMode);
            transitionSequence = null;
        });
    }

    private void AnimateHide(PanelMode modeToHide)
    {
        PanelBinding binding = GetBinding(modeToHide);
        if (binding == null || binding.panel == null || binding.canvasGroup == null)
        {
            return;
        }

        binding.canvasGroup.interactable = false;
        binding.canvasGroup.blocksRaycasts = false;

        Vector2 from = binding.shownPosition;
        Vector2 to = GetBottomHiddenPosition(binding.panel, binding.shownPosition);
        binding.panel.anchoredPosition = from;

        transitionSequence = DOTween.Sequence();
        transitionSequence.Join(ApplyEase(binding.panel.DOAnchorPos(to, animationDuration)));
        transitionSequence.Join(ApplyEase(binding.canvasGroup.DOFade(0f, animationDuration)));
        transitionSequence.OnComplete(() =>
        {
            binding.panel.anchoredPosition = to;
            binding.canvasGroup.alpha = 0f;
            binding.panel.gameObject.SetActive(false);

            currentMode = null;
            UpdateButtonVisuals(currentMode);
            transitionSequence = null;
        });
    }

    private Tween ApplyEase(Tween tween)
    {
        return animationCurve != null && animationCurve.length > 0
            ? tween.SetEase(animationCurve)
            : tween;
    }

    private void KillTransition()
    {
        if (transitionSequence == null)
        {
            return;
        }

        transitionSequence.Kill();
        transitionSequence = null;
    }

    private void UpdateButtonVisuals(PanelMode? activeMode)
    {
        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            PanelBinding binding = panels[i];
            if (binding?.button?.targetGraphic == null)
            {
                continue;
            }

            binding.button.targetGraphic.color = activeMode.HasValue && binding.mode == activeMode.Value ? activeButtonColor : inactiveButtonColor;
        }
    }

    private PanelBinding GetBinding(PanelMode mode)
    {
        if (panels == null)
        {
            return null;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].mode == mode)
            {
                return panels[i];
            }
        }

        return null;
    }

    private int GetBindingIndex(PanelMode mode)
    {
        if (panels == null)
        {
            return -1;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].mode == mode)
            {
                return i;
            }
        }

        return -1;
    }

    private Vector2 GetBottomHiddenPosition(RectTransform panel, Vector2 shownPosition)
    {
        Vector2 hiddenPosition = shownPosition;
        hiddenPosition.y -= panel.rect.height + hiddenMargin;
        return hiddenPosition;
    }

    private Vector2 GetLeftHiddenPosition(RectTransform panel, Vector2 shownPosition)
    {
        Vector2 hiddenPosition = shownPosition;
        hiddenPosition.x -= panel.rect.width + hiddenMargin;
        return hiddenPosition;
    }

    private Vector2 GetRightHiddenPosition(RectTransform panel, Vector2 shownPosition)
    {
        Vector2 hiddenPosition = shownPosition;
        hiddenPosition.x += panel.rect.width + hiddenMargin;
        return hiddenPosition;
    }

    private PanelMode? GetInitialMode()
    {
        switch (initialState)
        {
            case InitialState.Inventory:
                return PanelMode.Inventory;
            case InitialState.Craft:
                return PanelMode.Craft;
            default:
                return null;
        }
    }
}
