using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;

public class FriendManager : MonoBehaviour
{
    DatabaseReference databaseReference;
    public TMP_InputField inputField_FriendName;
    public TMP_Text warningText;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        });
    }

    public void AddFriend()
    {
        StartCoroutine(SendFriendRequest(DataSaver.instance.userId, inputField_FriendName.text));
    }

    private async Task<string> GetUserIdByUsername(string username)
    {
        DataSnapshot snapshot = await DataSaver.instance.dbRef.Child("users").GetValueAsync();

        if (snapshot.Exists)
        {
            foreach (var userSnapshot in snapshot.Children)
            {
                if (userSnapshot.Child("userName").Value.ToString() == username)
                {
                    // Username exists, return the userId
                    return userSnapshot.Key;
                }
            }
        }
        // Username does not exist
        return null;
    }

    public IEnumerator SendFriendRequest(string senderId, string receiverUsername)
    {
        // Check username availability
        Task<string> getUserIdTask = GetUserIdByUsername(receiverUsername);
        yield return new WaitUntil(() => getUserIdTask.IsCompleted);

        string receiverId = getUserIdTask.Result;

        // Continue with the registration process if the userId is available
        if (!string.IsNullOrEmpty(receiverId))
        {
            Debug.Log("Friend Exists");
            // Save friend request in the database
            databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).SetValueAsync(true);
        }
        else
        {
            warningText.text = "User does not exist";
        }
    }

    public void AcceptFriendRequest(string userId, string friendId)
    {
        // Add friends to each other's friend list
        databaseReference.Child("users").Child(userId).Child("friends").Child(friendId).SetValueAsync(true);
        databaseReference.Child("users").Child(friendId).Child("friends").Child(userId).SetValueAsync(true);

        // Remove the friend request
        databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
    }

    public void DeclineFriendRequest(string userId, string friendId)
    {
        // Remove the friend request
        databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
    }

    public void RemoveFriend(string userId, string friendId)
    {
        // Remove friends from each other's friend list
        databaseReference.Child("users").Child(userId).Child("friends").Child(friendId).RemoveValueAsync();
        databaseReference.Child("users").Child(friendId).Child("friends").Child(userId).RemoveValueAsync();
    }
}