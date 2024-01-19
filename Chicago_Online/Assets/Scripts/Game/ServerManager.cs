using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections;

public class ServerManager : MonoBehaviour
{
    #region Singleton
    public static ServerManager instance;

    private void Awake()
    {
        DontDestroyOnLoad(transform.gameObject);
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    DatabaseReference databaseReference;
    string serverId = "yourServerId"; // Replace with your server ID

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
        });
    }

    void Update()
    {
        // Check for players and start the game if conditions are met
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartGame();
        }
    }

    public void PlayerConnected(string userId)
    {
        StartCoroutine(UpdatePlayerStatus(userId, true));
    }

    public void PlayerDisconnected(string userId)
    {
        StartCoroutine(UpdatePlayerStatus(userId, false));
    }

    IEnumerator UpdatePlayerStatus(string userId, bool isConnected)
    {
        yield return new WaitForEndOfFrame(); // Wait for the end of the frame to ensure Firebase is initialized

        if (databaseReference != null)
        {
            databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).SetValueAsync(isConnected);
        }
    }

    void StartGame()
    {
        // Get the player count
        int playerCount = GetPlayerCount();

        if (playerCount >= 2)
        {
            SceneManager.LoadScene("GameScene"); // Replace with your game scene name
        }
        else
        {
            Debug.Log("Waiting for more players to join...");
        }
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
                bool isConnected = bool.Parse(playerSnapshot.Value.ToString());
                if (isConnected)
                {
                    count++;
                }
            }
        }

        return count;
    }
}