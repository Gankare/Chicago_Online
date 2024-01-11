using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Firebase.Database;
using Google.MiniJSON;

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
        userId = AuthManager.instance.user.Email;
        Debug.Log(userId);
        LoadData();
        InputDataAfterLogin.instance.ShowPlayerProfile();
        //if (userId == null || userId == "")
    }
    public void SaveData()
    {
        dts.userName = AuthManager.instance.user.DisplayName;
        string json = JsonUtility.ToJson(dts);
        dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json);
    }
    public void LoadData()
    {
        StartCoroutine(LoadDataEnum());
    }
    IEnumerator LoadDataEnum()
    {
        var serverData = dbRef.Child("users").Child(userId).GetValueAsync();
        yield return new WaitUntil(predicate: () => serverData.IsCompleted);
        Debug.Log("load complete");

        DataSnapshot snapshot = serverData.Result;
        string jsonData = snapshot.GetRawJsonValue();

        if(jsonData != null)
        {
            Debug.Log("Server data found");
            dts = JsonUtility.FromJson<DataToSave>(jsonData);   
        }
        else
            Debug.Log("No data found");
    }
}
