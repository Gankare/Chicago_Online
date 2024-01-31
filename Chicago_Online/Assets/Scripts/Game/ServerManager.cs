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
        DontDestroyOnLoad(gameObject);

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            if (app != null)
            {
                databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Firebase initialization successful.");
            }
            else
            {
                Debug.LogError("Firebase initialization failed.");
            }
        });
        StartCoroutine(CheckAndRemoveUserFromServer(DataSaver.instance.userId));
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MenuScene")
        {
            Destroy(gameObject);
        }
    }
    public void PlayerConnected(string userId)
    {
        StartCoroutine(UpdatePlayerStatus(userId, true));
        StartCoroutine(Player.instance.UpdatePlayerActivity());
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

    IEnumerator CheckAndRemoveUserFromServer(string userId)
    {
        // Get a reference to all servers
        var serversReference = DataSaver.instance.dbRef.Child("servers");

        // Retrieve data for all servers
        var serversTask = serversReference.GetValueAsync();
        yield return new WaitUntil(() => serversTask.IsCompleted);

        if (serversTask.IsFaulted || serversTask.IsCanceled)
        {
            Debug.LogError($"Error checking user {userId} connection to servers. Error: {serversTask.Exception}");
            yield break;
        }

        DataSnapshot serversSnapshot = serversTask.Result;

        if (serversSnapshot.Exists)
        {
            foreach (var serverSnapshot in serversSnapshot.Children)
            {
                string serverId = serverSnapshot.Key;

                // Check if the user is connected to the current server
                var userReference = serverSnapshot.Child("players").Child(userId);

                // Use an asynchronous method to retrieve user data
                yield return StartCoroutine(GetUserAsync(userReference, userId, serverId));
            }
        }
    }

    IEnumerator GetUserAsync(DataSnapshot userSnapshot, string userId, string serverId)
    {
        // Assuming userId is a direct child under the "players" node
        var userReference = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(userId);

        // Retrieve user data for the current server
        var userTask = userReference.GetValueAsync();
        yield return new WaitUntil(() => userTask.IsCompleted);

        if (userTask.IsFaulted || userTask.IsCanceled)
        {
            Debug.LogError($"Error checking user {userId} in server {serverId}. Error: {userTask.Exception}");
            yield break; // Move on to the next server
        }

        userSnapshot = userTask.Result;

        if (userSnapshot.Exists)
        {
            // User is connected to this server, remove them
            StartCoroutine(RemoveUserFromServer(userId, serverId));
        }
    }


    IEnumerator RemoveUserFromServer(string userId, string serverId)
    {
        // Remove the user from the server
        var removeUserTask = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).RemoveValueAsync();
        yield return new WaitUntil(() => removeUserTask.IsCompleted);

        if (removeUserTask.Exception != null)
        {
            Debug.LogError($"Error removing user {userId} from server {serverId}. Error: {removeUserTask.Exception}");
        }
        else
        {
            Debug.Log($"User {userId} has been removed from server {serverId}.");
        }
    }

    IEnumerator UpdatePlayerStatus(string userId, bool isConnected)
    {
        yield return new WaitForEndOfFrame();

        if (databaseReference != null)
        {
            var connectUser = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).Child("userData").Child("connected").SetValueAsync(isConnected);
            var setUserReady = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userData").Child("ready").SetValueAsync(false);
            yield return new WaitUntil(() => connectUser.IsCompleted && setUserReady.IsCompleted);

            if (!isConnected)
            {
                var removeUserId = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).RemoveValueAsync();
                yield return new WaitUntil(() => removeUserId.IsCompleted);

                /*if (userId == DataSaver.instance.userId)
                {
                    SceneManager.LoadScene("ServerScene");
                }*/
            }
        }
    }

    IEnumerator UpdatePlayerReadyStatus(string userId, bool isReady)
    {
        yield return new WaitForEndOfFrame();

        if (databaseReference != null)
        {
            var setUserReady = databaseReference.Child("servers").Child(serverId).Child("players").Child(userId).Child("userData").Child("ready").SetValueAsync(isReady);
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
                bool isConnected = bool.Parse(playerSnapshot.Child("userData").Child("connected").Value.ToString());
                bool isReady = bool.Parse(playerSnapshot.Child("userData").Child("ready").Value.ToString());

                if (isConnected && !isReady || !isConnected)
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
                    bool isConnected = bool.Parse(playerSnapshot.Child("userData").Child("connected").Value.ToString());
                    if (isConnected)
                    {
                        Debug.Log("player");
                        count++;
                    }
                }
            }

            callback.Invoke(count);
            return;
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