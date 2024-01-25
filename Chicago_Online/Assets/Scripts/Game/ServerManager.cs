using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEditor;
using System;

public class ServerManager : MonoBehaviour
{
    #region Singleton
    public static ServerManager instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    DatabaseReference databaseReference;
    public string serverId;

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "MenuScene")
        {
            Destroy(gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
            });
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

    public void PlayerReadyStatus(string userId, bool isReady)
    {
        StartCoroutine(UpdatePlayerReadyStatus(userId, isReady));

        // Check if all players are ready
        CheckAllPlayersReady();
    }

    IEnumerator UpdatePlayerStatus(string userId, bool isConnected)
    {
        yield return new WaitForEndOfFrame();

        if (databaseReference != null)
        {
            var connectUser = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).Child("connected").SetValueAsync(isConnected);
            var setUserReady = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("ready").SetValueAsync(false);
            yield return new WaitUntil(() => connectUser.IsCompleted && setUserReady.IsCompleted);

            if (!isConnected)
            {
                var removeUserId = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).RemoveValueAsync();
                yield return new WaitUntil(() => removeUserId.IsCompleted);

                if (userId == DataSaver.instance.userId)
                {
                    SceneManager.LoadScene("ServerScene");
                }
            }
        }
    }

    IEnumerator UpdatePlayerReadyStatus(string userId, bool isReady)
    {
        yield return new WaitForEndOfFrame();

        if (databaseReference != null)
        {
            var setUserReady = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).Child("ready").SetValueAsync(isReady);
            yield return new WaitUntil(() => setUserReady.IsCompleted);
        }
    }

    public IEnumerator CheckAllPlayersReady()
    {
        var playersInServer = databaseReference.Child("servers").Child(serverId).Child("players").GetValueAsync();

        yield return new WaitUntil(() => playersInServer.IsCompleted);

        DataSnapshot snapshot = playersInServer.Result;

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

    public void GetPlayerCount(System.Action<int> callback, string serverid)
    {
        int count = 0;
        var playersInServer = databaseReference.Child("servers").Child(serverid).Child("players").GetValueAsync();

        playersInServer.ContinueWithOnMainThread(task =>
        {
            DataSnapshot snapshot = task.Result;

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

            callback.Invoke(count);
        });
    }
    public async void GetGameStartedFlag(System.Action<bool> callback, string serverid)
    {
        var serverReference = databaseReference.Child("servers").Child(serverid);

        try
        {
            var serverSnapshot = await serverReference.GetValueAsync();

            if (serverSnapshot == null || !serverSnapshot.Exists)
            {
                Debug.LogWarning($"Server {serverid} does not exist.");
                callback.Invoke(false);
                return;
            }

            var gameStartedSnapshot = await serverReference.Child("gameHasStarted").GetValueAsync();

            if (gameStartedSnapshot == null || !gameStartedSnapshot.Exists || gameStartedSnapshot.Value == null)
            {
                Debug.LogWarning($"GameHasStarted flag not found for server {serverid}.");
                callback.Invoke(false);
                return;
            }

            bool gameStarted = bool.Parse(gameStartedSnapshot.Value.ToString());
            callback.Invoke(gameStarted);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting gameHasStarted flag. Error: {ex}");
            callback.Invoke(false);
        }
    }


    void StartGame()
    {
        //Set the gamehasstarted in database to true here
        StartCoroutine(SetGameStartedFlagCoroutine());
    }

    IEnumerator SetGameStartedFlagCoroutine()
    {
        yield return new WaitForEndOfFrame();

        if (databaseReference != null)
        {
            var setGameStarted = databaseReference.Child("servers").Child(serverId).Child("gameHasStarted").SetValueAsync(true);
            yield return new WaitUntil(() => setGameStarted.IsCompleted);
            SceneManager.LoadScene(serverId);
        }
    }
}