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
        StartCoroutine(RemoveFriend(DataSaver.instance.userId, friendId));
    }
    IEnumerator RemoveFriend(string userId, string friendId)
    {
        // Wait until both friend removals are complete
        yield return StartCoroutine(Delete(userId, friendId));

        // Load data and wait until it's completed
        yield return StartCoroutine(LoadDataAndWait());

        // Destroy the game object
        Destroy(gameObject);
    }
    IEnumerator Delete(string userId, string friendId)
    {
        // Remove friends from each other's friend list
        databaseReference.Child("users").Child(userId).Child("friends").Child(friendId).RemoveValueAsync();
        databaseReference.Child("users").Child(friendId).Child("friends").Child(userId).RemoveValueAsync();
        DataSaver.instance.SaveData();
        yield return new WaitForSeconds(0);
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
