using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using System.IO;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    private static SaveManager instance;

    [Header("Default State")]
    [Tooltip("The initial JSON state when starting a new game or resetting.")]
    public TextAsset defaultStateJson;

    private string saveFilePath;
    private SaveData currentData;
    private bool isLoading = false;

    public static SaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SaveManager>();
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
        saveFilePath = Path.Combine(Application.persistentDataPath, "save.json");
    }

    public SaveData CurrentData => currentData;

    public bool HasSave()
    {
        return File.Exists(saveFilePath);
    }

    public SaveData LoadGame()
    {
        isLoading = true;

        if (HasSave())
        {
            string json = File.ReadAllText(saveFilePath);
            currentData = JsonUtility.FromJson<SaveData>(json);
        }
        else
        {
            LoadDefaultState();
        }

        ApplySaveData();

        isLoading = false;
        return currentData;
    }

    public void ResetGame()
    {
        DialogueRunner runner = FindAnyObjectByType<DialogueRunner>();
        if (runner != null && runner.IsDialogueRunning)
        {
            runner.Stop();
        }

        if (HasSave())
        {
            File.Delete(saveFilePath);
        }

        LoadDefaultState();
        ApplySaveData();
        SaveGame();

        MainDirector director = FindAnyObjectByType<MainDirector>();
        if (director != null)
        {
            director.ClearStory();
            if (currentData != null && !string.IsNullOrWhiteSpace(currentData.lastYarnNode))
            {
                director.StartDialogueFromNode(currentData.lastYarnNode);
            }
        }
    }

    private void LoadDefaultState()
    {
        if (defaultStateJson != null && !string.IsNullOrWhiteSpace(defaultStateJson.text))
        {
            currentData = JsonUtility.FromJson<SaveData>(defaultStateJson.text);
        }
        else
        {
            currentData = new SaveData();
            Debug.LogWarning("Default State JSON is not assigned! Creating empty save data.");
        }
    }

    public void SaveGame()
    {
        if (isLoading) return;

        if (currentData == null)
        {
            currentData = new SaveData();
        }

        GatherSaveData();
        string json = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(saveFilePath, json);
    }

    public void UpdateLastYarnNode(string nodeName)
    {
        if (currentData == null) return;
        currentData.lastYarnNode = nodeName;
        SaveGame();
    }

    private void GatherSaveData()
    {
        if (InventoryManager.Instance != null)
        {
            currentData.inventory = InventoryManager.Instance.GetSaveData();
        }

        if (CraftingManager.Instance != null)
        {
            currentData.unlockedRecipes = CraftingManager.Instance.GetSaveData();
        }

        DialogueRunner runner = FindAnyObjectByType<DialogueRunner>();
        if (runner != null && runner.VariableStorage is InMemoryVariableStorage memoryStorage)
        {
            currentData.yarnVariables.Clear();
            var allVariables = memoryStorage.GetAllVariables();
            foreach (var kvp in allVariables.Item1)
            {
                currentData.yarnVariables.Add(new YarnVariableData { key = kvp.Key, type = "Float", floatValue = kvp.Value });
            }
            foreach (var kvp in allVariables.Item2)
            {
                currentData.yarnVariables.Add(new YarnVariableData { key = kvp.Key, type = "String", stringValue = kvp.Value });
            }
            foreach (var kvp in allVariables.Item3)
            {
                currentData.yarnVariables.Add(new YarnVariableData { key = kvp.Key, type = "Boolean", boolValue = kvp.Value });
            }
        }
    }

    private void ApplySaveData()
    {
        if (InventoryManager.Instance != null && currentData.inventory != null)
        {
            InventoryManager.Instance.LoadSaveData(currentData.inventory);
        }

        if (CraftingManager.Instance != null && currentData.unlockedRecipes != null)
        {
            CraftingManager.Instance.LoadSaveData(currentData.unlockedRecipes);
        }

        DialogueRunner runner = FindAnyObjectByType<DialogueRunner>();
        if (runner != null && runner.VariableStorage is InMemoryVariableStorage memoryStorage && currentData.yarnVariables != null)
        {
            memoryStorage.Clear();
            foreach (var yarnVar in currentData.yarnVariables)
            {
                if (yarnVar.type == "Float") memoryStorage.SetValue(yarnVar.key, yarnVar.floatValue);
                else if (yarnVar.type == "String") memoryStorage.SetValue(yarnVar.key, yarnVar.stringValue);
                else if (yarnVar.type == "Boolean") memoryStorage.SetValue(yarnVar.key, yarnVar.boolValue);
            }
        }
    }
}
