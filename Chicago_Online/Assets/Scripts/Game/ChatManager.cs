using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using TMPro;
using Unity.VisualScripting;


public class ChatManager : MonoBehaviour
{
    private string serverId;
    private int maxMessages = 5;
    public TMP_Text chatText;
    private Color playerUsernameColor = new Color(0.67f, 1, 0.57f);
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

    public void AddScoreMessageToChat(string message, string username, int ScoreAmount, bool gambit)
    {
        string scoreFor = "";
        if (ScoreAmount == 5 && gambit)
        {
            scoreFor = "gambit";
        }
        else if (!gambit)
        {
            scoreHierarchy = (ScoreHierarchy)ScoreAmount;
            scoreFor = scoreHierarchy.ToString();
            Debug.Log(scoreFor);
        }

        string newMessage = $"{username}: {message}{scoreFor}";

        DatabaseReference chatRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat");
        chatRef.Push().SetValueAsync(newMessage);
    }

    void HandleChatValueChanged(object sender, ValueChangedEventArgs args)
    {
        var dataSnapshot = args.Snapshot;
        if (dataSnapshot != null && dataSnapshot.ChildrenCount > 0)
        {
            int messageCount = 0;
            List<string> messages = new();
            foreach (var messageSnapshot in dataSnapshot.Children)
            {
                if (messageCount >= dataSnapshot.ChildrenCount - maxMessages)
                {
                    messages.Add(messageSnapshot.Value.ToString());
                }
                messageCount++;
            }
            DisplayMessages(messages);
        }
    }

    void DisplayMessages(List<string> messages)
    {
        chatText.text = "";

        foreach (string message in messages)
        {
            string coloredMessage = message.Replace(DataSaver.instance.dts.userName.ToString(), $"<color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(playerUsernameColor)}>{DataSaver.instance.dts.userName}</color>");
            chatText.text += coloredMessage + "\n";
        }
    }

    void ClearChat()
    {
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("chat").RemoveValueAsync();
    }
}
