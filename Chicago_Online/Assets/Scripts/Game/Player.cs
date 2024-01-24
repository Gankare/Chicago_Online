using Firebase.Extensions;
using UnityEngine;
using Firebase;
using Firebase.Database;

public class Player : MonoBehaviour
{
    void Start()
    {
        CheckIfUserExistsInServer(DataSaver.instance.userId);
    }
    private void OnDestroy()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
    }
    private void OnDisable()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
    }

    void CheckIfUserExistsInServer(string userId)
    {
        var serverReference = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").Child(userId);

        serverReference.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"Error checking if user {userId} exists in the server. Error: {task.Exception}");
                return;
            }

            DataSnapshot snapshot = task.Result;

            if (!snapshot.Exists)
            {
                // Player does not exist, connect the player
                ServerManager.instance.PlayerConnected(userId);
            }
            else
            {
                Debug.Log($"User {userId} already exists in the server.");
                // You can add additional logic here if needed
            }
        });
    }
}
