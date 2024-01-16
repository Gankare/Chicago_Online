using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;

public class InputDataAfterLogin : MonoBehaviour
{
    #region Singleton
    public static InputDataAfterLogin instance;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    public TMP_Text profileName;
    public TMP_Text profileWins;

    public GameObject friendRequestObject;
    public Transform friendRequestList;

    public GameObject friendObject;
    public Transform friendList;

    private List<GameObject> instantiatedFriendRequests = new List<GameObject>();
    private List<GameObject> instantiatedFriends = new List<GameObject>();

    private void Start()
    {
        DataSaver.instance.LoadData();
        ShowPlayerProfile();
    }
    public void ShowPlayerProfile()
    {
        profileName.text = DataSaver.instance.dts.userName;
        profileWins.text = "Wins: " + DataSaver.instance.dts.matchesWon;
        foreach (string friendId in DataSaver.instance.dts.friendRequests)
        {
            StartCoroutine(GetRequestUsernameAndDisplay(friendId, friendRequestList, instantiatedFriendRequests));
        }
        // Display current friends
        foreach (string friendId in DataSaver.instance.dts.friends)
        {
            StartCoroutine(GetFriendUsernameAndDisplay(friendId, friendList, instantiatedFriends));
        }
    }
    IEnumerator GetRequestUsernameAndDisplay(string friendId, Transform parent, List<GameObject> instantiatedList)
    {
        Debug.Log("Fetching data for friend request: " + friendId.ToString());

        // Fetch the user data based on the friend ID
        var userData = DataSaver.instance.dbRef.Child("users").Child(friendId).GetValueAsync();
        yield return new WaitUntil(() => userData.IsCompleted);

        DataSnapshot userSnapshot = userData.Result;

        if (userSnapshot.Exists)
        {
            string requestedUsername = userSnapshot.Child("userName").Value.ToString();

            // Check if a friend request with the same ID is already instantiated
            if (!IsFriendRequestInstantiated(friendId, instantiatedList))
            {
                var friend = Instantiate(friendRequestObject, parent);
                instantiatedList.Add(friend);

                var requestButton = friend.GetComponent<FriendRequestButton>();
                if (requestButton != null)
                {
                    requestButton.friendId = friendId;
                    requestButton.friendName.text = requestedUsername;
                }
                else
                {
                    Debug.LogError("FriendRequestButton component not found on instantiated object.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"User with ID {friendId} not found in friend requests.");
        }
    }

    IEnumerator GetFriendUsernameAndDisplay(string friendId, Transform parent, List<GameObject> instantiatedList)
    {
        Debug.Log("Fetching data for friend: " + friendId);
        // Fetch the user data based on the friend ID
        var userData = DataSaver.instance.dbRef.Child("users").Child(friendId).GetValueAsync();
        yield return new WaitUntil(() => userData.IsCompleted);

        DataSnapshot userSnapshot = userData.Result;

        if (userSnapshot.Exists)
        {
            string friendUsername = userSnapshot.Child("userName").Value.ToString();

            // Check if a friend with the same ID is already instantiated
            if (!IsFriendInstantiated(friendId, instantiatedList))
            {
                var friend = Instantiate(friendObject, parent);
                instantiatedList.Add(friend);

                var friendButton = friend.GetComponent<FriendButton>();
                if (friendButton != null)
                {
                    friendButton.friendId = friendId;
                    friendButton.friendName.text = friendUsername;
                }
                else
                {
                    Debug.LogError("FriendButton component not found on instantiated object.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"User with ID {friendId} not found.");
        }
    }

    // Helper function to check if a friend request with the same ID is already instantiated
    private bool IsFriendRequestInstantiated(string friendId, List<GameObject> instantiatedList)
    {
        return instantiatedList.Exists(obj => obj.GetComponent<FriendRequestButton>().friendId == friendId);
    }

    // Helper function to check if a friend with the same ID is already instantiated
    private bool IsFriendInstantiated(string friendId, List<GameObject> instantiatedList)
    {
        return instantiatedList.Exists(obj => obj.GetComponent<FriendButton>().friendId == friendId);
    }
}

