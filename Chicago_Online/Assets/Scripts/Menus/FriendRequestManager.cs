using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FriendRequestManager : MonoBehaviour
{
    DatabaseReference databaseReference;
    public string friendId;
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        });
    }
    private void Accept()
    {
        List<string> friendRequests = DataSaver.instance.dts.friendRequests;

        if (friendRequests.Count > 0)
        {
            // For simplicity, you can accept the first friend request in the list
            
            AcceptFriendRequest(DataSaver.instance.userId, friendId);
        }
        else
        {
            Debug.LogWarning("No friend requests to accept.");
        }
    }
    private void Decline()
    {

    }
    public void AcceptFriendRequest(string userId, string friendId)
    {
        // Add friends to each other's friend list
        databaseReference.Child("users").Child(userId).Child("friends").Child(friendId).SetValueAsync(true);
        databaseReference.Child("users").Child(friendId).Child("friends").Child(userId).SetValueAsync(true);

        // Remove the friend request
        databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
        DataSaver.instance.dts.friends.Add(friendId);
        DataSaver.instance.dts.friendRequests.Remove(friendId);
        DataSaver.instance.SaveData();
        Destroy(this.gameObject);
    }

    public void DeclineFriendRequest(string userId, string friendId)
    {
        // Remove the friend request
        databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
    }
}
