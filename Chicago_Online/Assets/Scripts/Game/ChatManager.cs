using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.SocialPlatforms;

public class ChatManager : MonoBehaviour
{
    private string serverId;
    private int maxMessages = 4;
    public TMP_Text chatText;
    private string username;
    public enum ScoreHierarchy
    {
        onePair	= 1,
        twoPairs = 2,
        tripple = 3,
        straight = 4,
        flush = 5,
        fullHouse = 6,
        quad = 7,
        straightFlush = 8,
        straightRoyalFlush = 52
    }
    private ScoreHierarchy scoreHierarchy;
    void Start()
    {
        serverId = ServerManager.instance.serverId;
        InitializeChat();
    }

    void InitializeChat()
    {
        ClearChat();
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat").ValueChanged += HandleChatValueChanged;
    }
    public void AddMessageToChat(string message)
    {
        DatabaseReference chatRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat");
        chatRef.Push().SetValueAsync(message);
    }
    public void AddDiscardMessageToChat(string message, string username)
    {
        DatabaseReference chatRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat");
        string newMessage = $"{username}: {message}";
        chatRef.Push().SetValueAsync(newMessage);
    }
    public IEnumerator AddScoreMessageToChat(string message, string playerId, int ScoreAmount, bool gambit)
    {
        string scoreFor;
        if (ScoreAmount == 5 && gambit)
        {
            scoreFor = "winning gambit";
        }
        else
        {
            scoreHierarchy = (ScoreHierarchy)ScoreAmount; 
            scoreFor = scoreHierarchy.ToString();
        }

        DatabaseReference chatRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat");
        yield return FetchUsername(playerId);
        string newMessage = $"{username}: {message} ({scoreFor})"; 
        chatRef.Push().SetValueAsync(newMessage);
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
            username = snapshot.Value.ToString();
        }
        else
        {
            Debug.LogError("User with ID " + playerId + " does not exist.");
        }
    }

    void HandleChatValueChanged(object sender, ValueChangedEventArgs args)
    {
        var dataSnapshot = args.Snapshot;
        if (dataSnapshot != null && dataSnapshot.ChildrenCount > 0)
        {
            // Ensure we only keep the last 'maxMessages' messages
            int messageCount = 0;
            List<string> messages = new();
            foreach (var messageSnapshot in dataSnapshot.Children)
            {
                if (messageCount >= dataSnapshot.ChildrenCount - maxMessages)
                {
                    // Add the message to the list
                    messages.Add(messageSnapshot.Value.ToString());
                }
                messageCount++;
            }

            // Update the chat display with the latest messages
            DisplayMessages(messages);
        }
    }

    void DisplayMessages(List<string> messages)
    {
        chatText.text = "";
        foreach (string message in messages)
        {
            chatText.text += message + "\n\n";
        }
    }

    void ClearChat()
    {
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat").RemoveValueAsync();
    }
}
