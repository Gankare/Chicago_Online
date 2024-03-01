using Firebase;
using Firebase.Database;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using UnityEngine.SceneManagement;

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    public TMP_Text countDownText;
    public List<GameObject> playerObjects;
    public List<TMP_Text> playerNames;
    public List<Image> readyCards;
    public GameObject buttons;
    public bool countDownActive = false;
    private Dictionary<string, bool> previousReadyValues = new();
    private int currentAmountOfPlayers;

    private void Start()
    {
        StartCoroutine(InitializePreviousReadyValues());
    }

    private IEnumerator InitializePreviousReadyValues()
    {
        var getGameStartedTask = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("gameHasStarted").GetValueAsync();

        yield return new WaitUntil(() => getGameStartedTask.IsCompleted);

        if (getGameStartedTask.Exception != null)
        {
            Debug.LogError($"Error fetching gameHasStarted data: {getGameStartedTask.Exception}");
            yield break;
        }

        DataSnapshot gameStartedSnapshot = getGameStartedTask.Result;

        if (!gameStartedSnapshot.Exists || gameStartedSnapshot.Value == null)
        {
            var setGameStartedTask = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("gameHasStarted").SetValueAsync(false);

            yield return new WaitUntil(() => setGameStartedTask.IsCompleted);

            if (setGameStartedTask.Exception != null)
            {
                Debug.LogError($"Error setting gameHasStarted data: {setGameStartedTask.Exception}");
                yield break;
            }
        }

        var playersInServerTask = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServerTask.IsCompleted);

        if (playersInServerTask.IsFaulted)
        {
            Debug.LogError($"Error fetching initial player data: {playersInServerTask.Exception}");
            yield break;
        }

        DataSnapshot playersSnapshot = playersInServerTask.Result;
        foreach (var playerSnapshot in playersSnapshot.Children)
        {
            var userId = playerSnapshot.Key;
            var readyValue = playerSnapshot.Child("userData").Child("ready").Exists ? (bool)playerSnapshot.Child("userData").Child("ready").Value : false;
            previousReadyValues.Add(userId, readyValue);
        }
        SubscribeToDatabaseEvents();
        StartCoroutine(InitLoadPlayers());
    }

    void SubscribeToDatabaseEvents()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded += HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved += HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
    }

    public void Ready()
    {
        StartCoroutine(ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, true));
    }
    public void Unready()
    {
        StartCoroutine(ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, false));
    }

    private void OnDisable()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded -= HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved -= HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged -= HandlePlayerChanged;
    }

    private void OnDestroy()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildAdded -= HandlePlayerAdded;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildRemoved -= HandlePlayerRemoved;
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged -= HandlePlayerChanged;
    }

    void HandlePlayerAdded(object sender, ChildChangedEventArgs args)
    {
        StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerRemoved(object sender, ChildChangedEventArgs args)
    {
        StartCoroutine(UpdatePlayers());
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        var currentUserId = args.Snapshot.Key;
        var currentReadyValue = args.Snapshot.Child("userData").Child("ready").Exists ? (bool)args.Snapshot.Child("userData").Child("ready").Value : false;

        // Check if the "ready" field has changed
        if (HasReadyStatusChanged(currentUserId, currentReadyValue) && this != null)
        {
            // Trigger the update method for any changes in ready status
            StartCoroutine(UpdatePlayers());
            StartCoroutine(ServerManager.instance.CheckAllPlayersReady());
        }
    }

    bool HasReadyStatusChanged(string userId, bool currentReadyValue)
    {
        // Check if the ready status has changed for the specific player
        if (previousReadyValues.TryGetValue(userId, out var previousReadyValue))
        {
            if (currentReadyValue != previousReadyValue)
            {
                // Update the dictionary with the new ready value
                previousReadyValues[userId] = currentReadyValue;
                return true;
            }
        }
        else
        {
            // If the player is not in the dictionary, add them with the current ready value
            previousReadyValues.Add(userId, currentReadyValue);
        }

        // Check if any player has readied up
        foreach (var readyValue in previousReadyValues.Values)
        {
            if (readyValue)
            {
                return true;
            }
        }

        return false;
    }

    IEnumerator InitLoadPlayers()
    {
        yield return new WaitForSeconds(1);
        StartCoroutine(UpdatePlayers());
    }

    IEnumerator UpdatePlayers()
    {
        int players = 0;
        int playersReady = 0;

        foreach (GameObject card in playerObjects)
        {
            card.SetActive(false);
        }

        var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServer.IsCompleted);

        DataSnapshot playersSnapshot = playersInServer.Result;

        if (playersSnapshot.Exists)
        {
            foreach (var requestSnapshot in playersSnapshot.Children)
            {
                playerObjects[players].SetActive(true);
                string userId = requestSnapshot.Key;

                var isPlayerReady = requestSnapshot.Child("userData").Child("ready").Value;
                if (isPlayerReady != null)
                {
                    bool readyValue = (bool)isPlayerReady;
                    if (readyValue)
                    {
                        playersReady++;
                        readyCards[players].color = new Color(108f / 255f, 166f / 255f, 65f / 255f); // Green
                    }
                    else
                    {
                        readyCards[players].color = new Color(70f / 255f, 61f / 255f, 79f / 255f); // Purple
                    }
                }
                var usernameTask = DataSaver.instance.dbRef.Child("users").Child(userId).Child("userName").GetValueAsync();
                yield return new WaitUntil(() => usernameTask.IsCompleted);

                DataSnapshot usernameSnapshot = usernameTask.Result;
                if (usernameSnapshot.Exists)
                {
                    playerNames[players].text = usernameSnapshot.Value.ToString();
                }

                if (requestSnapshot.Key == DataSaver.instance.userId)
                {
                    buttons.transform.position = new Vector2(playerObjects[players].transform.position.x, buttons.transform.position.y);
                }

                players++;
            }
        }
        currentAmountOfPlayers = players;
        amountOfPlayersText.text = $"{playersReady}/{players} players ready";
    }

    public void LeaveServer()
    {
        SceneManager.LoadScene("ServerScene");
    }

    public IEnumerator CountDownBeforeStart()
    {
        countDownActive = true;

        var countdownStartFlagRef = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("countdownStartFlag");

        while (countDownActive)
        {
            yield return new WaitForEndOfFrame();

            var countdownStartFlagTask = countdownStartFlagRef.GetValueAsync();
            yield return new WaitUntil(() => countdownStartFlagTask.IsCompleted);

            if (countdownStartFlagTask.Exception == null)
            {
                bool countdownStartFlag = bool.Parse(countdownStartFlagTask.Result.Value.ToString());
                if (!countdownStartFlag)
                {
                    countDownText.text = "";
                    Debug.Log("Countdown stopped.");
                    yield break;
                }
            }

            for (int i = 3; i > -1; i--)
            {
                countDownText.text = i.ToString();
                var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
                yield return new WaitUntil(() => playersInServer.IsCompleted);

                DataSnapshot snapshot = playersInServer.Result;

                bool anyPlayerUnready = false;

                if (snapshot.Exists)
                {
                    foreach (var playerSnapshot in snapshot.Children)
                    {
                        bool isReady = bool.Parse(playerSnapshot.Child("userData").Child("ready").Value.ToString());
                      
                        if (!isReady)
                        {
                            anyPlayerUnready = true;
                            break;
                        }
                        else
                        {
                            foreach (Image card in readyCards)
                            {
                                card.color = new Color(108f / 255f, 166f / 255f, 65f / 255f); // Green
                            }
                            amountOfPlayersText.text = $"{currentAmountOfPlayers}/{currentAmountOfPlayers} players ready";
                        }
                    }
                }

                if (anyPlayerUnready)
                {
                    Debug.Log("A player became unready. Countdown stopped.");
                    countDownText.text = "";
                    StartCoroutine(UpdatePlayers());
                    countDownActive = false;
                    break;
                }

                if (i == 0 && countDownActive && this != null)
                {

                    yield return StartCoroutine(ServerManager.instance.SetGameStartedFlagCoroutine());
                    yield break;
                }
                else
                yield return new WaitForSeconds(1);
            }
        }
    }
}
