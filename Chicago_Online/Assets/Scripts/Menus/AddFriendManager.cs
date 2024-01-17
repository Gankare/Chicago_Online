using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;

public class AddFriendManager : MonoBehaviour
{
    DatabaseReference databaseReference;
    public TMP_InputField inputField_FriendName;
    public TMP_Text warningText;
    public GameObject addFriendMenu;

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
        StartCoroutine(SendFriendRequest(DataSaver.instance.userId, inputField_FriendName.text.ToLower()));
    }

    public IEnumerator SendFriendRequest(string senderId, string receiverUsername)
    {
        // Check username availability
        Task<string> getUserIdTask = GetUserIdByUsername(receiverUsername);

        getUserIdTask.ContinueWith(task =>
        {
            // This block will be executed when the task is completed
            if (task.IsCompleted)
            {
                string receiverId = task.Result;

                // Continue with the registration process if the userId is available
                if (!string.IsNullOrEmpty(receiverId))
                {
                    // Check if the receiver is already a friend
                    if (DataSaver.instance.dts.friends.Contains(receiverUsername))
                    {
                        warningText.text = "User is already your friend.";
                    }
                    // Check if a friend request has already been sent
                    else
                    {
                        var friendRequestSnapshot = databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).GetValueAsync();

                        friendRequestSnapshot.ContinueWith(friendRequestTask =>
                        {
                            if (friendRequestTask.IsCompleted)
                            {
                                DataSnapshot friendRequestSnapshotResult = friendRequestTask.Result;

                                if (friendRequestSnapshotResult.Exists)
                                {
                                    warningText.text = "Friend request already sent.";
                                }
                                else
                                {
                                    Debug.Log("sent request");
                                    // Save friend request in the database
                                    addFriendMenu.SetActive(false);
                                    databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).SetValueAsync(senderId);
                                    DataSaver.instance.LoadData();
                                    warningText.text = "";
                                }
                            }
                        });
                    }
                }
                else
                {
                    warningText.text = "User does not exist";
                }
            }
        });

        // You can yield here if needed
        yield return null;
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
}