using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour
{
    #region Singleton
    public static SceneManager instance;

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
}
