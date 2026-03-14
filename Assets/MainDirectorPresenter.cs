using System.Threading.Tasks;
using UnityEngine;
using Yarn.Unity;

public class MainDirectorPresenter : DialoguePresenterBase
{
    [SerializeField] private MainDirector mainDirector;
    [SerializeField] private bool includeCharacterName = true;

    private void Awake()
    {
        if (mainDirector == null)
        {
            mainDirector = FindAnyObjectByType<MainDirector>();
        }
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        return YarnTask.CompletedTask;
    }

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (mainDirector == null)
        {
            Debug.LogError("MainDirectorPresenter requires a MainDirector reference.", this);
            return;
        }

        string text = line.TextWithoutCharacterName.Text;

        if (includeCharacterName && !string.IsNullOrWhiteSpace(line.CharacterName))
        {
            text = $"{line.CharacterName}: {text}";
        }

        mainDirector.AddStoryLine(text);

        Task lineTask = mainDirector.WaitForLineCompleteAsync();

        while (!lineTask.IsCompleted)
        {
            if (token.IsHurryUpRequested || token.IsNextContentRequested)
            {
                mainDirector.RequestSkipCurrentLine();
            }

            await Task.Yield();
        }

        await lineTask;
    }

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
    {
        return DialogueRunner.NoOptionSelected;
    }
}
