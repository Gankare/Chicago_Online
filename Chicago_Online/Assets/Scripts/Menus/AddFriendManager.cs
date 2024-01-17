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
    public TMP_Text confirmText;
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

        // Wait until the task is completed
        yield return new WaitUntil(() => getUserIdTask.IsCompleted);

        string receiverId = getUserIdTask.Result;

        // Continue with the registration process if the userId is available
        if (!string.IsNullOrEmpty(receiverId))
        {
            // Check if the receiver is already a friend
            if (DataSaver.instance.dts.friends.Contains(receiverUsername))
            {
                warningText.text = "User is already your friend.";
            }
            else
            {
                // Check if a friend request has already been sent
                var friendRequestSnapshot = databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).GetValueAsync();

                // Wait until the task is completed
                yield return new WaitUntil(() => friendRequestSnapshot.IsCompleted);

                DataSnapshot friendRequestSnapshotResult = friendRequestSnapshot.Result;

                if (friendRequestSnapshotResult.Exists || DataSaver.instance.dts.friendRequests.Contains(receiverUsername))
                {
                    warningText.text = "Friend request already active.";
                }
                else
                {
                    if (databaseReference != null)
                    {
                        // Save friend request in the database
                        databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).SetValueAsync(senderId);
                        warningText.text = "";
                        confirmText.text = "Friendrequest sent";

                        // Load data and wait until it's completed
                        yield return StartCoroutine(LoadDataAndWait());

                        Debug.Log("sent request");
                        addFriendMenu.SetActive(false);
                        confirmText.text = "";
                    }
                    else
                    {
                        Debug.Log("Databasereference is null");
                    }
                }
            }
        }
        else
        {
            warningText.text = "User does not exist";
        }
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

    IEnumerator LoadDataAndWait()
    {
        // Load data
        var loadDataEnumerator = DataSaver.instance.LoadDataEnum();

        // Iterate through the enumerator until it's done
        while (loadDataEnumerator.MoveNext())
        {
            yield return loadDataEnumerator.Current;
        }
    }
}