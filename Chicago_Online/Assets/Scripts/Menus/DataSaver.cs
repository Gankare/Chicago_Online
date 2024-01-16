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
            yield return StartCoroutine(LoadFriends());

            // Save data after loading
            SaveData();
            //if(SceneManager.GetActiveScene().name == "ServerScene")
            //InputDataAfterLogin.instance.ShowPlayerProfile();
        }
        else
        {
            Debug.Log("No data found");
        }
    }

    IEnumerator LoadFriendRequests()
    {
        // Clear the existing friend requests
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
        // Clear the existing friends
        dts.friends.Clear();

        var friendsData = dbRef.Child("users").Child(userId).Child("friends").GetValueAsync();
        yield return new WaitUntil(() => friendsData.IsCompleted);

        DataSnapshot friendsSnapshot = friendsData.Result;

        if (friendsSnapshot.Exists)
        {
            foreach (var friendSnapshot in friendsSnapshot.Children)
            {
                string friendId = friendSnapshot.Key;
                dts.friends.Add(friendId);
            }

            Debug.Log("Friends loaded");
        }
        else
        {
            Debug.Log("No friends found");
        }
    }
}