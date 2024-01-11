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
    #region Singleton
    public static DataSaver instance;

    private void Awake()
    {
        DontDestroyOnLoad(transform.gameObject);
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    public DataToSave dts; //Data to save
    public string userId;
    DatabaseReference dbRef; //Data base refrence

    public void Start()
    {
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        Debug.Log(dbRef.ToString());    
    }
    public void SaveData()
    {
        string json = JsonUtility.ToJson(dts);
        dbRef.Child("users").Child(userId).SetRawJsonValueAsync(json);
        Debug.Log("Saved data");
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
        else //First time logging in
        {
            Debug.Log("No data found");
        }
    }
}
