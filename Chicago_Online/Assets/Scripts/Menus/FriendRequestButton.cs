using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FriendRequestButton : MonoBehaviour
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
    public void Accept()
    {
        AcceptFriendRequest(DataSaver.instance.userId, friendId);
    }
    public void AcceptFriendRequest(string userId, string friendId)
    {
        // Add friends to each other's friend list
        databaseReference.Child("users").Child(userId).Child("friends").Child(friendId).SetValueAsync(true);
        databaseReference.Child("users").Child(friendId).Child("friends").Child(userId).SetValueAsync(true);

        // Remove the friend request
        databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
        DataSaver.instance.LoadData();
        Destroy(this.gameObject);
    }

    public void Decline()
    {
        DeclineFriendRequest(DataSaver.instance.userId, friendId);
    }

    public void DeclineFriendRequest(string userId, string friendId)
    {
        // Remove the friend request
        databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
        DataSaver.instance.LoadData();
        Destroy(this.gameObject);
    }
}
