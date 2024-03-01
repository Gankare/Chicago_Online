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

    public string serverId;
    void Start()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(CheckAndRemoveUserFromServer(DataSaver.instance.userId));
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerDisconnected(DataSaver.instance.userId);
    }
    private void OnDestroy()
    {
        PlayerDisconnected(DataSaver.instance.userId);
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MenuScene")
        {
            Destroy(gameObject);
        }
        if(scene.name == "ServerScene")
        {
            StartCoroutine(CheckAndRemoveUserFromServer(DataSaver.instance.userId));
        }
    }
    public void PlayerConnected(string userId)
    {
        StartCoroutine(UpdatePlayerStatus(userId, true));
    }

    public void PlayerDisconnected(string userId)
    {
        if (this == null)
            return;
        StartCoroutine(UpdatePlayerStatus(userId, false));
    }

    public IEnumerator PlayerReadyStatus(string userId, bool isReady)
    {
        yield return StartCoroutine(UpdatePlayerReadyStatus(userId, isReady));
       
        if(isReady)
        {
            StartCoroutine(CheckAllPlayersReady());
        }
    }

    IEnumerator CheckAndRemoveUserFromServer(string userId)
    {
        var serversReference = DataSaver.instance.dbRef.Child("servers");
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
                var userReference = serverSnapshot.Child("players").Child(userId);
                yield return StartCoroutine(GetUserAsync(userReference, userId, serverId));
            }
        }
    }

    IEnumerator GetUserAsync(DataSnapshot userSnapshot, string userId, string serverId)
    {
        var userReference = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(userId);
        var userTask = userReference.GetValueAsync();
        yield return new WaitUntil(() => userTask.IsCompleted);

        if (userTask.IsFaulted || userTask.IsCanceled)
        {
            Debug.LogError($"Error checking user {userId} in server {serverId}. Error: {userTask.Exception}");
            yield break;
        }

        userSnapshot = userTask.Result;

        if (userSnapshot.Exists)
        {
            StartCoroutine(RemoveUserFromServer(userId, serverId));
        }
    }

    IEnumerator RemoveUserFromServer(string userId, string serverId)
    {
        var removeUserTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(userId).RemoveValueAsync();
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

        if (DataSaver.instance.dbRef != null && userId != null)
        {
            var setCountdownStartFlagTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("countdownStartFlag").SetValueAsync(false);
            var connectUser = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(userId).Child("userData").Child("connected").SetValueAsync(isConnected);
            var setUserReady = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userData").Child("ready").SetValueAsync(false);

            yield return new WaitUntil(() => connectUser.IsCompleted && setUserReady.IsCompleted && setCountdownStartFlagTask.IsCompleted);

            if (!isConnected)
            {
                var removeUserId = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(userId).RemoveValueAsync();
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

        if (DataSaver.instance.dbRef != null)
        {
            var setUserReady = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(userId).Child("userData").Child("ready").SetValueAsync(isReady);
            yield return new WaitUntil(() => setUserReady.IsCompleted);

            if (!isReady && userId != null)
            {
                // Clear the countdown start flag if any player becomes unready
                var countdownStartFlagTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("countdownStartFlag").SetValueAsync(false);
                yield return new WaitUntil(() => countdownStartFlagTask.IsCompleted);
            }
        }
    }


    public void GetPlayerCount(System.Action<int> callback, string serverid)
    {
        int count = 0;
        var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(serverid).Child("players").GetValueAsync();

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
        var serverReference = DataSaver.instance.dbRef.Child("servers").Child(serverid);

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
    public IEnumerator CheckAllPlayersReady()
    {
        while (SceneManager.GetActiveScene().name != serverId)
        {
            var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
            yield return new WaitUntil(() => playersInServer.IsCompleted);

            DataSnapshot snapshot = playersInServer.Result;

            if (snapshot.Exists && snapshot.ChildrenCount > 1)
            {
                bool allPlayersReady = true;

                foreach (var playerSnapshot in snapshot.Children)
                {
                    bool isConnected = bool.Parse(playerSnapshot.Child("userData").Child("connected").Value.ToString());
                    bool isReady = bool.Parse(playerSnapshot.Child("userData").Child("ready").Value.ToString());

                    if (isConnected && !isReady)
                    {
                        allPlayersReady = false;
                        break;
                    }
                }

                if (allPlayersReady)
                {
                    var countdownStartFlagTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("countdownStartFlag").SetValueAsync(true);
                    yield return new WaitUntil(() => countdownStartFlagTask.IsCompleted);

                    if (countdownStartFlagTask.Exception == null)
                    {
                        WaitingRoomButtons waitingRoom = FindObjectOfType<WaitingRoomButtons>();
                        StartCoroutine(waitingRoom.CountDownBeforeStart());
                    }
                    break;
                }
            }
            else
            {
                Debug.Log("Not enough players to start");
            }
            yield return new WaitForSeconds(1);
        }
    }

    public IEnumerator SetGameStartedFlagCoroutine()
    {
        yield return new WaitForEndOfFrame();

        if (DataSaver.instance.dbRef != null)
        {
            var setGameStarted = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameHasStarted").SetValueAsync(true);
            yield return new WaitUntil(() => setGameStarted.IsCompleted);
            SceneManager.LoadScene(serverId);
            yield break;
        }
    }
}