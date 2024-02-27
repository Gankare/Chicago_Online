using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Firebase.Database;
using Firebase;
using UnityEngine.SceneManagement;

[Serializable]
public class DataToSave
{
    public int matchesWon;
    public string userName;
    public List<String> friends;
    public List<String> friendRequests;
}
public class DataSaver : MonoBehaviour
{
    #region Singleton
    public static DataSaver instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    public DataToSave dts; 
    public string userId;
    public DatabaseReference dbRef; 

    public bool nameExists = false;

    public void Start()
    {
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
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
    public IEnumerator LoadDataEnum()
    {
        var serverData = dbRef.Child("users").Child(userId).GetValueAsync();
        yield return new WaitUntil(() => serverData.IsCompleted);

        DataSnapshot snapshot = serverData.Result;
        string jsonData = snapshot.GetRawJsonValue();

        if (jsonData != null)
        {
            Debug.Log("Server data found");
            dts = JsonUtility.FromJson<DataToSave>(jsonData);

            yield return StartCoroutine(LoadFriendRequests());
            yield return StartCoroutine(LoadFriends());
            SaveData();
        }
        else
        {
            Debug.Log("No data found");
        }
    }

    IEnumerator LoadFriendRequests()
    {
        dts.friendRequests.Clear();

        var friendRequestsData = dbRef.Child("friendRequests").Child(userId).GetValueAsync();
        yield return new WaitUntil(() => friendRequestsData.IsCompleted);

        DataSnapshot friendRequestsSnapshot = friendRequestsData.Result;

        if (friendRequestsSnapshot.Exists)
        {
            foreach (var requestSnapshot in friendRequestsSnapshot.Children)
            {
                string friendRequestId = requestSnapshot.Value.ToString();
                dts.friendRequests.Add(friendRequestId);
            }

            Debug.Log("Friend requests loaded");
        }
        else
        {
            Debug.Log("No friend requests found");
        }
    }

    IEnumerator LoadFriends()
    {
        dts.friends.Clear();

        var friendsData = dbRef.Child("userFriends").Child(userId).GetValueAsync();
        yield return new WaitUntil(() => friendsData.IsCompleted);

        DataSnapshot friendsSnapshot = friendsData.Result;

        if (friendsSnapshot.Exists)
        {
            foreach (var friendSnapshot in friendsSnapshot.Children)
            {
                string friendId = friendSnapshot.Value.ToString();
                dts.friends.Add(friendId);
                Debug.Log("Added friend " + friendId);
            }

            Debug.Log("Friends loaded");
        }
        else
        {
            Debug.Log("No friends found");
        }
    }
}