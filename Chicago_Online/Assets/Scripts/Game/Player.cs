using Firebase.Extensions;
using UnityEngine;
using Firebase;
using Firebase.Database;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    #region Singleton
    public static Player instance;

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

    void Start()
    {
        CheckIfUserExistsInServer(DataSaver.instance.userId);
    }
    private void OnDestroy()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
    } 
    private void OnDisable()
    {
        ServerManager.instance.PlayerDisconnected(DataSaver.instance.userId);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MenuScene" || scene.name == "ServerScene")
        {
            Destroy(gameObject);
        }
    }

    void CheckIfUserExistsInServer(string userId)
    {
        var serverReference = DataSaver.instance.dbRef.Child("servers").Child(ServerManager.instance.serverId).Child("players").Child(userId);

        serverReference.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"Error checking if user {userId} exists in the server. Error: {task.Exception}");
                return;
            }

            DataSnapshot snapshot = task.Result;

            if (!snapshot.Exists)
            {
                // Player does not exist, connect the player
                ServerManager.instance.PlayerConnected(userId);
            }
            else
            {
                Debug.Log($"User {userId} already exists in the server.");
                // You can add additional logic here if needed
            }
        });
    }
}
