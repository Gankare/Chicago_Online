using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FriendButton : MonoBehaviour
{
    DatabaseReference databaseReference;
    public string friendId;
    public TMP_Text friendName;
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        });
    }

    public void Remove()
    {
        RemoveFriend(DataSaver.instance.userId, friendId);
    }
    public void RemoveFriend(string userId, string friendId)
    {
        // Remove friends from each other's friend list
        databaseReference.Child("users").Child(userId).Child("friends").Child(friendId).RemoveValueAsync();
        databaseReference.Child("users").Child(friendId).Child("friends").Child(userId).RemoveValueAsync();
        DataSaver.instance.LoadData();
        Destroy(this.gameObject);
    }
}
