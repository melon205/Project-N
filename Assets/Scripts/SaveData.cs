using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public string lastYarnNode = "MainStory";
    public List<ItemSaveData> inventory = new List<ItemSaveData>();
    public List<string> unlockedRecipes = new List<string>();
    public List<YarnVariableData> yarnVariables = new List<YarnVariableData>();
}

[Serializable]
public class ItemSaveData
{
    public string itemId;
    public int quantity;

    public ItemSaveData(string id, int qty)
    {
        itemId = id;
        quantity = qty;
    }
}

[Serializable]
public class YarnVariableData
{
    public string key;
    public string type; // "Float", "String", "Boolean"
    public float floatValue;
    public string stringValue;
    public bool boolValue;
}
