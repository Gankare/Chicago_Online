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
        StartCoroutine(AcceptFriendRequestCoroutine(DataSaver.instance.userId, friendId));
    }

    IEnumerator AcceptFriendRequestCoroutine(string userId, string friendId)
    {
        // Add friends to each other's friend list
        var addFriendTask1 = databaseReference.Child("userFriends").Child(userId).Child(friendId).SetValueAsync(friendId);
        var addFriendTask2 = databaseReference.Child("userFriends").Child(friendId).Child(userId).SetValueAsync(userId);

        // Wait until both friend requests are complete
        yield return new WaitUntil(() => addFriendTask1.IsCompleted && addFriendTask2.IsCompleted);

        // Remove the friend request
        var removeRequestTask = databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();

        // Wait until the friend request removal is complete
        yield return new WaitUntil(() => removeRequestTask.IsCompleted);

        // Load data and wait until it's completed
        yield return StartCoroutine(LoadDataAndWait());

        InputDataAfterLogin.instance.ShowPlayerProfile();

        yield return new WaitForSeconds(1);
        // Destroy the game object
        Destroy(gameObject);
    }

    public void Decline()
    {
        StartCoroutine(DeclineFriendRequestCoroutine(DataSaver.instance.userId, friendId));
    }

    IEnumerator DeclineFriendRequestCoroutine(string userId, string friendId)
    {
        // Remove the friend request
        var removeRequestTask = databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();

        // Wait until the friend request removal is complete
        yield return new WaitUntil(() => removeRequestTask.IsCompleted);

        // Load data and wait until it's completed
        yield return StartCoroutine(LoadDataAndWait());

        // Destroy the game object
        Destroy(gameObject);
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
