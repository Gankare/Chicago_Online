using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Firebase;
using Firebase.Database;

public class UserTurnText : MonoBehaviour
{
    public TMP_Text userTurn;
    private string serverId;

    void Start()
    {
        userTurn.text = "";
        serverId = ServerManager.instance.serverId;
        if (string.IsNullOrEmpty(serverId))
        {
            Debug.LogError("Server ID is not set. Please assign a valid server ID.");
            return;
        }
        Invoke(nameof(AddListeners), 1f);
    }

    private void AddListeners()
    {
        DatabaseReference playersRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players");
        playersRef.ValueChanged += PlayersDataChanged;
    }

    private void OnDestroy()
    {
        DatabaseReference playersRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players");
        playersRef.ValueChanged -= PlayersDataChanged;
    }

    private IEnumerator FetchUsername(string playerId)
    {
        DatabaseReference userRef = DataSaver.instance.dbRef.Child("users").Child(playerId);

        var fetchTask = userRef.Child("userName").GetValueAsync();
        yield return new WaitUntil(() => fetchTask.IsCompleted);

        if (fetchTask.Exception != null)
        {
            Debug.LogError("Failed to fetch username for user ID: " + playerId);
            yield break;
        }

        DataSnapshot snapshot = fetchTask.Result;
        if (snapshot.Exists)
        {
            string username = snapshot.Value.ToString();
            userTurn.text = $"Turn: {username}";
            userTurn.gameObject.SetActive(false);
            userTurn.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("User with ID " + playerId + " does not exist.");
        }
    }

    private void PlayersDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args != null && args.Snapshot != null && args.Snapshot.Value != null)
        {
            foreach (var playerSnapshot in args.Snapshot.Children)
            {
                string playerId = playerSnapshot.Key;
                var isTurn = playerSnapshot.Child("userGameData").Child("isTurn").Value;

                if (isTurn != null && (bool)isTurn)
                {
                    StartCoroutine(FetchUsername(playerId));
                    break; 
                }
            }
        }
        else
        {
            Debug.LogError("Invalid players data.");
        }
    }
}
