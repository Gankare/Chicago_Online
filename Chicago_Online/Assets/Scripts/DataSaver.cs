using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Firebase.Database;

[Serializable]
public class DataToSave
{
    public string userName;
    public int matchesWon;
}
public class DataSaver : MonoBehaviour
{
    public DataToSave dts; //Data to save
    public string userId;
    DatabaseReference dbRef; //Data base refrence

    public void Awake()
    {
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
    }
    public void SaveData()
    {
        string json = JsonUtility.ToJson(dts);
        dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json);
    }
    public void LoadData()
    {

    }
}
