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
    public DataSaver dataSaver;

    public void ShowPlayerProfile()
    {
        profileName.text = dataSaver.dts.userName;
        profileWins.text = "Wins: " + dataSaver.dts.matchesWon;
    }
}
