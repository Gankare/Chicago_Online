using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEditor;

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
    public bool gameHasStarted = false;

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
        gameHasStarted = false;
        StartCoroutine(UpdatePlayerStatus(userId, true));
    }

    public void PlayerDisconnected(string userId)
    {
        gameHasStarted = false;
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
            yield return new WaitUntil(() => connectUser.IsCompleted);
            yield return new WaitUntil(() => setUserReady.IsCompleted);

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

    void StartGame()
    {
        gameHasStarted = true;
        SceneManager.LoadScene(serverId);
    }

    public int GetPlayerCount()
    {
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