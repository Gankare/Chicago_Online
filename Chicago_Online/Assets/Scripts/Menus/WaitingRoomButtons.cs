using Firebase;
using Firebase.Database;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Threading.Tasks;

public class WaitingRoomButtons : MonoBehaviour
{
    public TMP_Text amountOfPlayersText;
    public List<GameObject> playerObjects;
    public List<TMP_Text> playerNames;
    public List<Image> readyCards;
    public GameObject buttons;
    private bool isUpdatingPlayers = false;

    private void Start()
    {
        //UpdatePlayers();
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged += HandlePlayerChanged;
    }

    void HandlePlayerChanged(object sender, ChildChangedEventArgs args)
    {
        // Check if there are changes in fields other than "lastActivity" under the "userData" node
        var userDataNode = args.Snapshot.Child("userData");

        if (userDataNode != null)
        {
            var userDataChanges = userDataNode.Children
                .Where(child => child.Key != "lastActivity")
                .Any(child => child.Value?.ToString() != args.Snapshot.Child("lastActivity")?.Value?.ToString());

            if (userDataChanges)
            {
                // Trigger the update method for any changes
                StartCoroutine(UpdatePlayers());
            }
            else
            {
                // Handle other changes if needed
                Debug.Log($"Unhandled change in player data: {args.Snapshot.GetRawJsonValue()}");
            }
        }
        else
        {
            Debug.LogError("userDataNode is null. Handle appropriately.");
        }
    }





    private void OnDisable()
    {
        // Remove the listener when the script is disabled
        RemovePlayerChangedListener();
    }
    private void OnDestroy()
    {
        // Remove the listener when the object is destroyed
        RemovePlayerChangedListener();
    }
    void RemovePlayerChangedListener()
    {
        DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").ChildChanged -= HandlePlayerChanged;
    }
    public void Ready()
    {
        ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, true);
    }
    public void Unready()
    {
        ServerManager.instance.PlayerReadyStatus(DataSaver.instance.userId, false);
    }

    IEnumerator UpdatePlayers()
    {
        //Reset values
        isUpdatingPlayers = true;
        int players = 0;
        int playersReady = 0;

        foreach (GameObject card in playerObjects)
        {
            card.SetActive(false);
        }

        var playersInServer = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => playersInServer.IsCompleted);

        if (playersInServer.Exception != null)
        {
            Debug.LogError($"Error getting players data: {playersInServer.Exception}");
            yield break;
        }

        DataSnapshot playersSnapshot = playersInServer.Result;

        if (playersSnapshot.Exists)
        {
            foreach (var requestSnapshot in playersSnapshot.Children)
            {
                playerObjects[players].SetActive(true);

                // Get userId from requestSnapshot
                string userId = requestSnapshot.Key;

                // Check if the player is ready
                var isPlayerReady = requestSnapshot.Child("userData").Child("ready").Value;
                if (isPlayerReady != null)
                {
                    bool readyValue = bool.Parse(isPlayerReady.ToString());
                    if (readyValue)
                    {
                        playersReady++;
                        readyCards[players].color = Color.green;
                    }
                    else
                    {
                        readyCards[players].color = Color.white;
                    }
                }

                // Look up username in "users" node
                var usernameTask = DataSaver.instance.dbRef.Child("users").Child(userId).Child("userName").GetValueAsync();
                yield return new WaitUntil(() => usernameTask.IsCompleted);

                if (usernameTask.Exception == null)
                {
                    DataSnapshot usernameSnapshot = usernameTask.Result;
                    if (usernameSnapshot.Exists)
                    {
                        // Set the player's name in the UI
                        playerNames[players].text = usernameSnapshot.Value.ToString();
                    }
                }

                if(requestSnapshot.Key == DataSaver.instance.userId)
                {
                    buttons.transform.position = new Vector2(playerObjects[players].transform.position.x, buttons.transform.position.y);
                }

                players++;
            }
            isUpdatingPlayers = false;
        }

        amountOfPlayersText.text = $"{playersReady}/{players} players ready";
    }

    public void LeaveServer()
    {
        SceneManager.LoadScene("ServerScene");
    }
}
