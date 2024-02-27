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
        var addFriendTask1 = databaseReference.Child("userFriends").Child(userId).Child(friendId).SetValueAsync(friendId);
        var addFriendTask2 = databaseReference.Child("userFriends").Child(friendId).Child(userId).SetValueAsync(userId);
        yield return new WaitUntil(() => addFriendTask1.IsCompleted && addFriendTask2.IsCompleted);

        var removeRequestTask = databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
        yield return new WaitUntil(() => removeRequestTask.IsCompleted);

        yield return StartCoroutine(LoadDataAndWait());

        InputDataAfterLogin.instance.ShowPlayerProfile();
        yield return new WaitForSeconds(1);
        Destroy(gameObject);
    }

    public void Decline()
    {
        StartCoroutine(DeclineFriendRequestCoroutine(DataSaver.instance.userId, friendId));
    }

    IEnumerator DeclineFriendRequestCoroutine(string userId, string friendId)
    {
        var removeRequestTask = databaseReference.Child("friendRequests").Child(userId).Child(friendId).RemoveValueAsync();
        yield return new WaitUntil(() => removeRequestTask.IsCompleted);

        yield return StartCoroutine(LoadDataAndWait());
        Destroy(gameObject);
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
