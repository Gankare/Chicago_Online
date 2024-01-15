using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Firebase.Database;
using Firebase;
using Firebase.Extensions;

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
        DontDestroyOnLoad(transform.gameObject);
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    public DataToSave dts; //Data to save
    public string userId;
    public DatabaseReference dbRef; //Data base refrence

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

            // Load friend requests
            yield return StartCoroutine(LoadFriendRequests());

            // Load current friends
            yield return StartCoroutine(LoadCurrentFriends());

            // Save data after loading
            SaveData();
        }
        else
        {
            Debug.Log("No data found");
        }
    }

    IEnumerator LoadFriendRequests()
    {
        var friendRequestsData = dbRef.Child("friendRequests").Child(userId).GetValueAsync();
        yield return new WaitUntil(() => friendRequestsData.IsCompleted);

        DataSnapshot friendRequestsSnapshot = friendRequestsData.Result;

        if (friendRequestsSnapshot.Exists)
        {
            // Clear the existing friend requests
            dts.friendRequests.Clear();

            foreach (var requestSnapshot in friendRequestsSnapshot.Children)
            {
                string friendId = requestSnapshot.Key;
                dts.friendRequests.Add(friendId);
            }

            Debug.Log("Friend requests loaded");
        }
        else
        {
            Debug.Log("No friend requests found");
        }
    }
    IEnumerator LoadCurrentFriends()
    {
        // Clear the existing friends
        dts.friends.Clear();

        foreach (string friendId in dts.friends)
        {
            // Fetch the user data based on the friend ID
            var userData = dbRef.Child("users").Child(friendId).GetValueAsync();
            yield return new WaitUntil(() => userData.IsCompleted);

            DataSnapshot userSnapshot = userData.Result;

            if (userSnapshot.Exists)
            {
                string friendUsername = userSnapshot.Child("userName").Value.ToString();
                dts.friends.Add(friendUsername);
            }
            else
            {
                Debug.LogWarning($"User with ID {friendId} not found.");
            }
        }
        Debug.Log("Current friends loaded");
    }
}