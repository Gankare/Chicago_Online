using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private void Start()
    {
        DataSaver.instance.LoadData();
        ShowPlayerProfile();
    }
    public void ShowPlayerProfile()
    {
        profileName.text = DataSaver.instance.dts.userName;
        //profileName.text = AuthManager.instance.user.DisplayName;
        profileWins.text = "Wins: " + DataSaver.instance.dts.matchesWon;
    }
}
