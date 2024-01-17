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
        StartCoroutine(SendFriendRequestWrapper(DataSaver.instance.userId, inputField_FriendName.text.ToLower()));
    }

    private IEnumerator SendFriendRequestWrapper(string senderId, string receiverUsername)
    {
        yield return SendFriendRequest(senderId, receiverUsername);
    }

    private IEnumerator SendFriendRequest(string senderId, string receiverUsername)
    {
        Task<string> getUserIdTask = GetUserIdByUsername(receiverUsername);

        yield return new WaitUntil(() => getUserIdTask.IsCompleted);

        string receiverId = getUserIdTask.Result;

        if (!string.IsNullOrEmpty(receiverId))
        {
            if (DataSaver.instance.dts.friends.Contains(receiverId))
            {
                warningText.text = "User is already your friend.";
            }
            else
            {
                var friendRequestSnapshot = databaseReference.Child("friendRequests").Child(senderId).Child(receiverId).GetValueAsync();

                yield return new WaitUntil(() => friendRequestSnapshot.IsCompleted);

                DataSnapshot friendRequestSnapshotResult = friendRequestSnapshot.Result;

                if (friendRequestSnapshotResult.Exists || DataSaver.instance.dts.friendRequests.Contains(receiverId))
                {
                    warningText.text = "Friend request already active.";
                }
                else
                {
                    if (databaseReference != null)
                    {
                        databaseReference.Child("friendRequests").Child(receiverId).Child(senderId).SetValueAsync(senderId);
                        warningText.text = "";
                        confirmText.text = "Friend request sent";

                        yield return StartCoroutine(LoadDataAndWait());

                        Debug.Log("Sent friend request");
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
                    return userSnapshot.Key;
                }
            }
        }

        return null;
    }

    IEnumerator LoadDataAndWait()
    {
        var loadDataEnumerator = DataSaver.instance.LoadDataEnum();

        while (loadDataEnumerator.MoveNext())
        {
            yield return loadDataEnumerator.Current;
        }
    }
}