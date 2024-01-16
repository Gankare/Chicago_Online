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
        yield return new WaitUntil(() => getUserIdTask.IsCompleted);

        string receiverId = getUserIdTask.Result;

        // Continue with the registration process if the userId is available
        if (!string.IsNullOrEmpty(receiverId))
        {
            Debug.Log("Friend Exists");


            //Check error thing if user already sent request or if they are allready friends then give warning and return else continue below

            // Save friend request in the database
            databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).SetValueAsync(senderId);
            DataSaver.instance.LoadData();
            warningText.text = "";
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
}