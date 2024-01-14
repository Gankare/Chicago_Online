using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public void LogOut()
    {
        DataSaver.instance.SaveData();
        DataSaver.instance.userId = null;
        SceneHandler.instance.ReturnToMenu();
    }
}
