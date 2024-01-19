using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections;

public class ServerManager : MonoBehaviour
{

    DatabaseReference databaseReference;
    public string serverId;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        });
    }

    public void PlayerConnected(string userId)
    {
        StartCoroutine(UpdatePlayerStatus(userId, true));
    }

    public void PlayerDisconnected(string userId)
    {
        StartCoroutine(UpdatePlayerStatus(userId, false));
    }

    public void PlayerReady(string userId, bool isReady)
    {
        StartCoroutine(UpdatePlayerReadyStatus(userId, isReady));

        // Check if all players are ready
        CheckAllPlayersReady();
    }

    public void PlayerUnReady(string userId, bool isReady)
    {
        StartCoroutine(UpdatePlayerReadyStatus(userId, isReady));

        // Check if all players are ready
        CheckAllPlayersReady();
    }

    IEnumerator UpdatePlayerStatus(string userId, bool isConnected)
    {
        yield return new WaitForEndOfFrame(); // Wait for the end of the frame to ensure Firebase is initialized

        if (databaseReference != null)
        {
            databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).Child("connected").SetValueAsync(isConnected);

            // Check if the player is disconnecting
            if (!isConnected)
            {
                // Remove the player's entry from the database
                databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).RemoveValueAsync();

                // Check if this is the local player (the one running this script)
                if (userId == DataSaver.instance.userId)
                {
                    // Return to the server scene
                    SceneManager.LoadScene("ServerScene");
                }
            }
        }
    }

    IEnumerator UpdatePlayerReadyStatus(string userId, bool isReady)
    {
        yield return new WaitForEndOfFrame(); // Wait for the end of the frame to ensure Firebase is initialized

        if (databaseReference != null)
        {
            databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).Child("ready").SetValueAsync(isReady);
        }
    }

    void StartGame()
    {
        SceneManager.LoadScene("GameScene"); // Replace with your game scene name
    }

    int GetPlayerCount()
    {
        // Read the player count from the database
        DataSnapshot snapshot = databaseReference.Child("servers").Child(serverId).Child("players").GetValueAsync().Result;
        int count = 0;

        if (snapshot.Exists)
        {
            foreach (var playerSnapshot in snapshot.Children)
            {
                bool isConnected = bool.Parse(playerSnapshot.Child("connected").Value.ToString());
                if (isConnected)
                {
                    count++;
                }
            }
        }

        return count;
    }

    void CheckAllPlayersReady()
    {
        DataSnapshot snapshot = databaseReference.Child("servers").Child(serverId).Child("players").GetValueAsync().Result;

        if (snapshot.Exists)
        {
            bool allPlayersReady = true;

            foreach (var playerSnapshot in snapshot.Children)
            {
                bool isConnected = bool.Parse(playerSnapshot.Child("connected").Value.ToString());
                bool isReady = bool.Parse(playerSnapshot.Child("ready").Value.ToString());

                if (isConnected && !isReady)
                {
                    allPlayersReady = false;
                    break;
                }
            }

            if (allPlayersReady)
            {
                StartGame();
            }
        }
    }
}
