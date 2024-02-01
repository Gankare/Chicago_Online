using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneHandler : MonoBehaviour
{
    #region Singleton
    public static SceneHandler instance;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(this);
    }
    #endregion

    public GameObject loginMenu;
    public GameObject registerMenu;

    public void LoginScreen()
    {
        loginMenu.SetActive(true);
        registerMenu.SetActive(false);
    }
    public void RegisterMenu()
    {
        registerMenu.SetActive(true);
        loginMenu.SetActive(false);
    }
    public void LoadServerScene()
    {
        SceneManager.LoadScene("ServerScene");
    }
    public void ReturnToMenu()
    {
        SceneManager.LoadSceneAsync("MenuScene");
    }
    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
