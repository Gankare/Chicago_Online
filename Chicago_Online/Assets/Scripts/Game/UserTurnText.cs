using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Firebase;
using Firebase.Database;

public class UserTurnText : MonoBehaviour
{
    public TMP_Text userTurn;
    private string serverId; // Assign the server ID in the Inspector or through script

    void Start()
    {
        serverId = ServerManager.instance.serverId;
        if (string.IsNullOrEmpty(serverId))
        {
            Debug.LogError("Server ID is not set. Please assign a valid server ID.");
            return;
        }

        // Reference to the path where the players information is stored
        string playersPath = "servers/" + serverId + "/players";

        // Set the reference to where the players information is stored in your database
        DataSaver.instance.dbRef = DataSaver.instance.dbRef.Child(playersPath);

        // Listen for changes in the players' data
        DataSaver.instance.dbRef.ValueChanged += PlayersDataChanged;
    }

    private void OnDestroy()
    {
        // Remove the event listener when the object is destroyed
        if (DataSaver.instance.dbRef != null)
        {
            DataSaver.instance.dbRef.ValueChanged -= PlayersDataChanged;
        }
    }

    // This method will be called whenever the players' data changes in the database
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
                    // Now, assuming you have another reference to the database where user details are stored
                    DatabaseReference userRef = DataSaver.instance.dbRef.Child("users").Child(playerId);

                    // Fetch the username from the user's details
                    userRef.Child("userName").GetValueAsync().ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            Debug.LogError("Failed to fetch username for user ID: " + playerId);
                        }
                        else if (task.IsCompleted)
                        {
                            DataSnapshot snapshot = task.Result;
                            if (snapshot.Exists)
                            {
                                // Update the text to display the username of the user whose turn it is
                                string username = snapshot.Value.ToString();
                                userTurn.text = "Turn: " + username;
                            }
                            else
                            {
                                Debug.LogError("User with ID " + playerId + " does not exist.");
                            }
                        }
                    });
                    break; // Exit loop after finding the player whose turn it is
                }
            }
        }
        else
        {
            Debug.LogError("Invalid players data.");
        }
    }
}